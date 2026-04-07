using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Extensions.Logging;
using DeltaGetResponse = Microsoft.Graph.Sites.Item.Lists.Item.Items.Delta.DeltaGetResponse;

using Azure;
using Azure.Data.Tables;

using FoundrySharePointKnowledge.Common;
using FoundrySharePointKnowledge.Domain.Settings;
using FoundrySharePointKnowledge.Domain.Contracts;
using FoundrySharePointKnowledge.Domain.SharePoint;

namespace FoundrySharePointKnowledge.Infrastructure.Services
{
    /// <summary>
    /// This implements all PnP Functionality.
    /// </summary>
    public class SharePointService : ISharePointService
    {
        #region Members
        private readonly TableServiceClient _tableClient;
        private readonly GraphServiceClient _graphClient;
        private readonly ILogger<SharePointService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly SharePointSettings _sharePointSettings;
        #endregion
        #region Initialization
        public SharePointService(TableServiceClient tableClient,
                                 GraphServiceClient graphClient,
                                 ILogger<SharePointService> logger,
                                 IHttpClientFactory httpClientFactory,
                                 SharePointSettings sharePointSettings)
        {
            //initialization
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this._tableClient = tableClient ?? throw new ArgumentNullException(nameof(tableClient));
            this._graphClient = graphClient ?? throw new ArgumentNullException(nameof(graphClient));
            this._httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            this._sharePointSettings = sharePointSettings ?? throw new ArgumentNullException(nameof(sharePointSettings));
        }
        #endregion
        #region Public Methods
        /// <summary>
        /// Uses the PnP changes API to process webhook items.
        /// </summary>
        public async Task<string> HandleWebhookAsync(SPWebhookPayload webhook)
        {
            try
            {
                //initialization
                if (!webhook?.Value?.Any() ?? true)
                {
                    //empty request
                    string warning = $"Webhook {webhook?.ToString() ?? "N/A"} has no subscriptions.";
                    this._logger.LogWarning(warning);
                    return warning;
                }

                //check secret
                if (webhook.Value.Select(v => v.WebhookSecret).Any(s => !s.Equals(this._sharePointSettings.WebhookSecret)))
                    throw new Exception($"At least one value for webhook {webhook} has the incorrect secret.");

                //ensure delta token table
                TableClient listItemEntities = this._tableClient.GetTableClient(FSPKConstants.AzureStorage.Tables.SharePointListItems);
                TableClient deltaTokens = this._tableClient.GetTableClient(FSPKConstants.AzureStorage.Tables.SharePointDeltaTokens);
                List<SPListItemTableEntity> listItemEntitiesToUpsert = new List<SPListItemTableEntity>();

                //get all subscriptions
                foreach (SPWebhookSubscription subscription in webhook.Value)
                {
                    //process each subscription
                    using (this._logger.BeginScope(new Dictionary<string, object>
                    {
                        //assemble dictionary
                        { $"Webhook {nameof(subscription.ListId)}", subscription.ListId },
                        { $"Webhook {nameof(subscription.SiteURL)}", subscription.SiteURL },
                        { $"Webhook {nameof(subscription.SubscriptionId)}", subscription.SubscriptionId }
                    }))
                    {
                        //get each site id
                        DeltaGetResponse deltaResponse = null;
                        string siteId = await this.GetSiteIdAsync(FSPKConstants.SharePoint.SiteCollectionURL.CombineURL(subscription.SiteURL));

                        //check delta token
                        NullableResponse<SPDeltaTokenTableEntity> existingDeltaToken = await deltaTokens.GetEntityIfExistsAsync<SPDeltaTokenTableEntity>(siteId, subscription.ListId);
                        if (existingDeltaToken.HasValue)
                        {
                            //get deltas
                            deltaResponse = await this._graphClient.Sites[siteId]
                                                                   .Lists[subscription.ListId]
                                                                   .Items
                                                                   .Delta
                                                                   .WithUrl(existingDeltaToken.Value.Token)
                                                                   .GetAsDeltaGetResponseAsync();

                            //update delta token
                            existingDeltaToken.Value.Token = deltaResponse.OdataDeltaLink;
                            await deltaTokens.UpsertEntityAsync<SPDeltaTokenTableEntity>(existingDeltaToken.Value);
                            this._logger.LogInformation($"Updated existing SharePoint webhook delta token {deltaResponse.OdataDeltaLink}.");
                        }
                        else
                        {
                            //no delta for this list
                            deltaResponse = await this._graphClient.Sites[siteId]
                                                                   .Lists[subscription.ListId]
                                                                   .Items
                                                                   .Delta
                                                                   .GetAsDeltaGetResponseAsync();

                            //create new delta token
                            SPDeltaTokenTableEntity newDeltaToken = new SPDeltaTokenTableEntity();
                            newDeltaToken.Token = deltaResponse.OdataDeltaLink;
                            newDeltaToken.RowKey = subscription.ListId;
                            newDeltaToken.PartitionKey = siteId;

                            //save new delta token
                            await deltaTokens.AddEntityAsync<SPDeltaTokenTableEntity>(newDeltaToken);
                            this._logger.LogInformation($"Created new SharePoint webhook delta token {deltaResponse.OdataDeltaLink}.");
                        }

                        do
                        {
                            //process results sequentially
                            foreach (ListItem listItem in deltaResponse.Value.OrderBy(d => d.LastModifiedDateTime))
                            {
                                //create entity
                                this._logger.LogInformation($"Processing SharePoint list item {(string.IsNullOrWhiteSpace(listItem.WebUrl) ? listItem.Id : listItem.WebUrl)}.");
                                SPListItemTableEntity listItemEntity = new SPListItemTableEntity();
                                listItemEntity.PartitionKey = subscription.ListId;
                                listItemEntity.RowKey = listItem.Id;
                                listItemEntity.SiteId = siteId;

                                //check deletion
                                if (listItem.Deleted != null)
                                {
                                    //mark item for deletion
                                    listItemEntity.IsDeleted = true;
                                }
                                else
                                {
                                    //list item created/updated
                                    listItemEntity.URL = listItem.WebUrl;

                                    //capture title
                                    if (listItem.Fields.AdditionalData.ContainsKey(FSPKConstants.SharePoint.Fields.Title))
                                        listItemEntity.Title = listItem.Fields.AdditionalData[FSPKConstants.SharePoint.Fields.Title]?.ToString();

                                    //capture case description
                                    if (listItem.Fields.AdditionalData.ContainsKey(FSPKConstants.SharePoint.Fields.Description))
                                        listItemEntity.Description = listItem.Fields.AdditionalData[FSPKConstants.SharePoint.Fields.Description]?.ToString();
                                }

                                //collect entities
                                listItemEntitiesToUpsert.Add(listItemEntity);
                            }

                            //get next page of results
                            if (!string.IsNullOrWhiteSpace(deltaResponse.OdataNextLink))
                                deltaResponse = await this._graphClient.Sites[siteId]
                                                                       .Lists[subscription.ListId]
                                                                       .Items
                                                                       .Delta
                                                                       .WithUrl(deltaResponse.OdataNextLink)
                                                                       .GetAsDeltaGetResponseAsync();
                        }
                        while (!string.IsNullOrWhiteSpace(deltaResponse.OdataNextLink));
                    }
                }

                //upsert list item entities
                this._logger.LogInformation($"Persisting {listItemEntitiesToUpsert.Count} SharePoint list items.");
                await listItemEntities.PerformBulkTableTansactionAsync(listItemEntitiesToUpsert);

                //return
                this._logger.LogInformation($"Processed {listItemEntitiesToUpsert.Count} SharePoint list items.");
                return string.Empty;
            }
            catch (Exception ex)
            {
                //error
                this._logger.LogError(ex, $"Failed to process webhook {webhook}.");
                return ex.Message;
            }
        }

        /// <summary>
        /// Downloads a file's contents from SharePoint via Graph with an app having Files.Read.All.
        /// </summary>
        public async Task<byte[]> GetFileContentsMostPrivilegedAsync(SPFile file)
        {
            //initialization
            using (this._logger.BeginScope(new Dictionary<string, object>
            {
                //assemble dictionary
                { $"{nameof(this.GetFileContentsMostPrivilegedAsync)}", file?.URL ?? "N/A" }
            }))
            {
                try
                {
                    //initialization                
                    this.EnsureFile(file);
                    this._logger.LogInformation($"Downloading {file.Name} from SharePoint via item {file.ItemId} and drive {file.DriveId}.");

                    //get file contents
                    using (Stream contentStream = await this._graphClient.Drives[file.DriveId].Items[file.ItemId].Content.GetAsync())
                    {
                        //convert to byte array
                        using (MemoryStream memoryStream = new MemoryStream())
                        {
                            //return
                            this._logger.LogInformation($"Acquired {contentStream.Length} bytes for SharePoint file {file.URL}.");
                            await contentStream.CopyToAsync(memoryStream);
                            return memoryStream.ToArray();
                        }
                    }
                }
                catch (Exception ex)
                {
                    //error
                    this._logger.LogError(ex, $"Unable to download {file.Name} from SharePoint.");
                    throw;
                }
            }
        }

        /// <summary>
        /// Downloads a file's contents from SharePoint via Graph with an app having Files.Read.All.
        /// </summary>
        public async Task<byte[]> GetFileContentsLeastPrivilegedAsync(SPFile file)
        {
            //initialization
            using (this._logger.BeginScope(new Dictionary<string, object>
            {
                //assemble dictionary
                { $"{nameof(this.GetFileContentsLeastPrivilegedAsync)}", file?.URL ?? "N/A" }
            }))
            {
                try
                {
                    //check file                
                    this.EnsureFile(file);
                    this._logger.LogInformation($"Downloading {file.Name} from SharePoint.");

                    //download file
                    string siteId = await this.GetSiteIdAsync(file.URL);
                    this._logger.LogInformation($"Acquiring item {file.ItemId} of drive {file.DriveId} from site {siteId}.");
                    using HttpResponseMessage response = await this._httpClientFactory.CreateClient(FSPKConstants.SharePoint.Client).GetAsync(string.Format(FSPKConstants.SharePoint.FileDownloadURLFormat, siteId, file.DriveId, file.ItemId));

                    //get file contents
                    response.EnsureSuccessStatusCode();
                    byte[] contents = await response.Content.ReadAsByteArrayAsync();
                    this._logger.LogInformation($"Acquired {contents.Length} bytes for {file.Name} from {file.URL}.");

                    //return
                    return contents;
                }
                catch (Exception ex)
                {
                    //error
                    this._logger.LogError(ex, $"Unable to download {file.Name} from SharePoint.");
                    throw;
                }
            }
        }
        #endregion
        #region Private Methods
        /// <summary>
        /// Gets a site collection id from its URL.
        /// </summary>
        private async Task<string> GetSiteIdAsync(string siteURL)
        {
            //initialization
            Uri uri = new Uri(siteURL);
            string[] uriParts = uri.LocalPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            //build the URL form of the site id graph expects
            string managedPath = uriParts[0];
            string siteCollectionURL = uriParts[1];
            string siteCollectionPath = $"{uri.Host}:/{managedPath}/{siteCollectionURL}";
            this._logger.LogInformation($"Loading site collection {siteCollectionPath} ({nameof(managedPath)}={managedPath}; {nameof(siteCollectionURL)}={siteCollectionURL}; {nameof(siteCollectionPath)}={siteCollectionPath})");

            //convert site URL to site id
            Site site = await this._graphClient.Sites[siteCollectionPath].GetAsync();
            return Guid.Parse(site.Id.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[1]).ToString();
        }

        /// <summary>
        /// Throws an excpetion if any required fields are missing from the SPFile.
        /// </summary>
        private void EnsureFile(SPFile file)
        {
            //return
            if (file == null)
                throw new ArgumentNullException(nameof(file));
            if (string.IsNullOrWhiteSpace(file.DriveId))
                throw new ArgumentException("DriveId is required.", nameof(file));
            if (string.IsNullOrWhiteSpace(file.ItemId))
                throw new ArgumentException("ItemId is required.", nameof(file));
            if (string.IsNullOrWhiteSpace(file.Name))
                throw new ArgumentException("Name is required.", nameof(file));
            if (string.IsNullOrWhiteSpace(file.URL))
                throw new ArgumentException("URL is required.", nameof(file));
        }
        #endregion
    }
}
