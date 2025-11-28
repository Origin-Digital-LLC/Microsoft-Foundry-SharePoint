using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

using Azure;
using Azure.AI.DocumentIntelligence;

using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Extensions.Logging;

using FoundrySharePointKnowledge.Common;
using FoundrySharePointKnowledge.Domain.Search;
using FoundrySharePointKnowledge.Domain.Settings;
using FoundrySharePointKnowledge.Domain.Contracts;
using FoundrySharePointKnowledge.Domain.SharePoint;

namespace FoundrySharePointKnowledge.Infrastructure.Services
{
    /// <summary>
    /// This interacts with Azure Foundry for document processing.
    /// </summary>
    public class FoundryService : IFoundryService
    {
        #region Members
        private readonly ILogger<FoundryService> _logger;
        private readonly GraphServiceClient _graphClient;
        private readonly AzureFoundrySettings _foundrySettings;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly DocumentIntelligenceClient _documentIntelligenceClient;
        #endregion
        #region Initialization
        public FoundryService(ILogger<FoundryService> logger,
                              GraphServiceClient graphClient,
                              AzureFoundrySettings foundrySettings,
                              IHttpClientFactory httpClientFactory,
                              DocumentIntelligenceClient documentIntelligenceClient)
        {
            //initialization
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this._graphClient = graphClient ?? throw new ArgumentNullException(nameof(graphClient));
            this._foundrySettings = foundrySettings ?? throw new ArgumentNullException(nameof(foundrySettings));
            this._httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            this._documentIntelligenceClient = documentIntelligenceClient ?? throw new ArgumentNullException(nameof(documentIntelligenceClient));
        }
        #endregion
        #region Public Methods
        /// <summary>
        /// Prepares a raw file for search ingestion by chunking and vectorizing its content.
        /// </summary>
        [Obsolete("This method was part of an abandoned approach and has not been fully tested.")]
        public async Task<SPFileChunk[]> ChunkFileAsync(SPFile file)
        {
            //initialization
            if (string.IsNullOrWhiteSpace(file?.ItemId) || string.IsNullOrWhiteSpace(file?.DriveId) || string.IsNullOrWhiteSpace(file?.Name) || string.IsNullOrWhiteSpace(file?.URL))
            {
                //error
                this._logger.LogError($"Cannot chunk file {file?.URL ?? "N/A"}: all SharePoint fields are requied.");
                return null;
            }

            //vectorize file name
            List<SPFileChunk> fileChunks = new List<SPFileChunk>();
            double[] titleVector = await this.VectorizeTextAsync(file.Title);

            //send file to Foundry for analysis
            byte[] fileContents = await this.GetFileContentsMostPriviledgedAsync(file);
            this._logger.LogInformation($"Analyzing file {file.Name} using {FSPKConstants.Foundry.ModelId}.");
            AnalyzeDocumentOptions options = new AnalyzeDocumentOptions(FSPKConstants.Foundry.ModelId, new BinaryData(fileContents));
            Operation<AnalyzeResult> result = await this._documentIntelligenceClient.AnalyzeDocumentAsync(WaitUntil.Completed, options);

            //check result
            if (result.HasValue)
            {
                //extract text content for each page
                foreach (DocumentPage page in result.Value.Pages)
                {
                    //collect each line
                    this._logger.LogInformation($"Vectorizing page {page.PageNumber} of file {file.Name} using {FSPKConstants.Foundry.ModelId}.");
                    StringBuilder contentBuilder = new StringBuilder();
                    foreach (DocumentLine line in page.Lines)
                        contentBuilder.AppendLine(line.Content);

                    //vectorize content
                    string content = contentBuilder.ToString();
                    double[] contentVector = await this.VectorizeTextAsync(content);

                    //collect document
                    fileChunks.Add(new SPFileChunk(file, page.PageNumber, content, titleVector, contentVector));    
                }

                //return
                return fileChunks.ToArray();
            }
            else
            {
                //error
                this._logger.LogError($"Document analysis failed for file {file.Name}.");
                return null;
            }
        }

        /// <summary>
        /// Downloads a file's contents from SharePoint via Graph with an app having Files.Read.All.
        /// </summary>
        public async Task<byte[]> GetFileContentsMostPriviledgedAsync(SPFile file)
        {
            try
            {
                //initialization                
                this.EnsureFile(file);
                this._logger.LogInformation($"Downloading {file.Name} from SharePoint...");

                //get file contents
                using (Stream contentStream = await this._graphClient.Drives[file.DriveId].Items[file.ItemId].Content.GetAsync())
                {
                    //convert to byte array
                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        //return
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

        /// <summary>
        /// Downloads a file's contents from SharePoint via Graph with an app having Files.Read.All.
        /// </summary>
        public async Task<byte[]> GetFileContentsLeastPriviledgedAsync(SPFile file)
        {
            try
            {
                //initialization                
                this.EnsureFile(file);
                this._logger.LogInformation($"Downloading {file.Name} from SharePoint...");

                //parse the raw URL provided from the power automate flow
                Uri uri = new Uri(file.URL);
                string[] uriParts = uri.LocalPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

                //build the URL form of the site id graph expects
                string managedPath = uriParts[0];
                string siteCollectionURL = uriParts[1];
                string siteCollectionPath = $"{uri.Host}:/{managedPath}/{siteCollectionURL}";

                //convert site URL to site id
                Site site = await this._graphClient.Sites[siteCollectionPath].GetAsync();
                Guid siteId = Guid.Parse(site.Id.Split(',')[1]);

                //download file
                using HttpResponseMessage response = await this._httpClientFactory.CreateClient(FSPKConstants.SharePoint.Client).GetAsync(string.Format(FSPKConstants.SharePoint.FileDownloadURLFormat, siteId, file.DriveId, file.ItemId));
                response.EnsureSuccessStatusCode();

                //get file contents
                byte[] contents = await response.Content.ReadAsByteArrayAsync();
                this._logger.LogInformation($"Acquired {contents.Length} bytes for {file.Name} from {siteCollectionPath}/{uriParts[2]}.");

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
        #endregion
        #region Private Methods
        /// <summary>
        /// Uses Azure Foundry to vectorize SharePoint content.
        /// </summary>
        private async Task<double[]> VectorizeTextAsync(string content)
        {
            //initialization
            string result = string.Empty;
            this._logger.LogInformation($"Vectorizing content using model {this._foundrySettings.EmbeddingModel}: {content}");

            try
            {
                //build request
                HttpClient client = this._httpClientFactory.CreateClient(FSPKConstants.Foundry.Client);
                var requestBody = new
                {
                    //assemble object
                    input = content
                };

                //call open ai                
                using HttpResponseMessage response = await client.PostAsync($"{FSPKConstants.Routing.Foundry.OpenAIDeployments.CombineURL(this._foundrySettings.EmbeddingModel).CombineURL(FSPKConstants.Routing.Foundry.Embedddings)}{this._foundrySettings.EmbeddingAPIVersion}",
                                                                            new StringContent(JsonSerializer.Serialize(requestBody),
                                                                            Encoding.UTF8,
                                                                            FSPKConstants.ContentTypes.JSON));

                //get result
                result = await response.Content.ReadAsStringAsync();
                response.EnsureSuccessStatusCode();

                //return
                VectorizedContent vector = JsonSerializer.Deserialize<VectorizedContent>(result);
                return vector?.Data.FirstOrDefault()?.Embedding ?? Array.Empty<double>();
            }
            catch (Exception ex)
            {
                //error
                this._logger.LogError(ex, $"Unable to vectorize content: {result}");
                throw;
            }
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
