using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

using Azure.Data.Tables;
using Azure.Storage.Sas;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

using Microsoft.Extensions.Logging;

using FoundrySharePointKnowledge.Common;
using FoundrySharePointKnowledge.Domain.Tranches;
using FoundrySharePointKnowledge.Domain.Contracts;

namespace FoundrySharePointKnowledge.Infrastructure.Services
{
    /// <summary>
    /// This manages bulk upload tranches: their blob containers and their Azure Storage table records.
    /// </summary>
    public class TrancheService : ITrancheService
    {
        #region Members
        private readonly BlobServiceClient _blobClient;
        private readonly TableServiceClient _tableClient;
        private readonly ILogger<TrancheService> _logger;
        #endregion
        #region Initialization
        public TrancheService(BlobServiceClient blobClient,
                              TableServiceClient tableClient,
                              ILogger<TrancheService> logger)
        {
            //initialization
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this._blobClient = blobClient ?? throw new ArgumentNullException(nameof(blobClient));
            this._tableClient = tableClient ?? throw new ArgumentNullException(nameof(tableClient));
        }
        #endregion
        #region Public Methods
        /// <summary>
        /// Creates a private blob container for a bulk upload and returns a short-lived SAS URL scoped to it.
        /// </summary>
        public async Task<UploadSession> CreateTrancheAsync(string userName, string trancheName)
        {
            //initialization
            string name = $"{this.SanitizeContainerName(userName)}-{Guid.NewGuid():N}";
            this._logger.LogInformation($"Creating upload container {name}.");

            //create a private container
            BlobContainerClient container = this._blobClient.GetBlobContainerClient(name);
            await container.CreateAsync(PublicAccessType.None);

            //ensure a sas url can be generated
            if (!container.CanGenerateSasUri)
                throw new InvalidOperationException($"Unable to generate a SAS URL for container {name} because the blob client is not configured with a shared-key credential.");

            //generate a scoped, short-lived sas url
            DateTimeOffset expiresOn = DateTimeOffset.UtcNow.AddHours(FSPKConstants.AzureStorage.Blobs.SasExpiryHours);
            Uri sasURI = container.GenerateSasUri(BlobContainerSasPermissions.Create | BlobContainerSasPermissions.Write, expiresOn);

            //track a new tranche for this upload
            Guid trancheId = Guid.NewGuid();
            TableClient tranches = this._tableClient.GetTableClient(FSPKConstants.AzureStorage.Tables.Tranches);
            await tranches.CreateIfNotExistsAsync();
            TrancheTableEntity tranche = new TrancheTableEntity()
            {
                //assemble object
                RowKey = trancheId.ToString(),
                PartitionKey = userName,
                ContainerName = name
            };

            //apply a caller-supplied name, otherwise keep the entity's default
            if (!string.IsNullOrWhiteSpace(trancheName))
                tranche.Name = trancheName;

            //return
            await tranches.UpsertEntityAsync(tranche);
            this._logger.LogInformation($"Created upload container {name} with a SAS URL expiring on {expiresOn:o} for tranche {trancheId}.");
            return new UploadSession(name, sasURI.ToString(), expiresOn, trancheId);
        }

        /// <summary>
        /// Records the files uploaded for a bulk upload tranche and updates its file count and total size.
        /// </summary>
        public async Task TrackTrancheFilesAsync(Guid trancheId, string containerName, string[] fileNames)
        {
            //initialization
            ArgumentNullException.ThrowIfNull(fileNames);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(containerName);
            this._logger.LogInformation($"Tracking {fileNames.Pluralize("file")} for tranche {trancheId}.");
            TableClient trancheFiles = this._tableClient.GetTableClient(FSPKConstants.AzureStorage.Tables.TrancheFiles);

            //list the container's blobs once to get every file's size, since each container is exclusive to a single tranche
            BlobContainerClient container = this._blobClient.GetBlobContainerClient(containerName);
            Dictionary<string, long> blobSizes = new Dictionary<string, long>();
            await foreach (BlobItem blob in container.GetBlobsAsync())
                blobSizes[blob.Name] = blob.Properties.ContentLength ?? 0;

            //build one entity per file
            List<TrancheFileTableEntity> entities = fileNames.Select(fileName => new TrancheFileTableEntity()
            {
                //assemble object
                RowKey = fileName.ToTableRowKey(),
                PartitionKey = trancheId.ToString(),
                ContentType = fileName.GetContentType(),
                FileSize = blobSizes.GetValueOrDefault(fileName)
            }).ToList();

            //write files
            await trancheFiles.CreateIfNotExistsAsync();
            await trancheFiles.PerformBulkTableTansactionAsync(entities);

            //update the tranche's file count and total size
            long totalSize = entities.Sum(entity => entity.FileSize);
            TableClient tranches = this._tableClient.GetTableClient(FSPKConstants.AzureStorage.Tables.Tranches);
            await foreach (TrancheTableEntity tranche in tranches.QueryAsync<TrancheTableEntity>(t => t.RowKey == trancheId.ToString()))
            {
                //update and save the match, which should be the only one
                tranche.TotalSize = totalSize;
                tranche.FileCount = entities.Count;
                await tranches.UpsertEntityAsync(tranche);
            }

            //return
            this._logger.LogInformation($"Tracked {entities.Pluralize("file")} for tranche {trancheId}.");
        }

        /// <summary>
        /// Cancels a bulk upload by deleting its blob container if it exists.
        /// </summary>
        public async Task CancelUploadAsync(string containerName)
        {
            //initialization
            ArgumentNullException.ThrowIfNullOrWhiteSpace(containerName);

            try
            {
                //delete the container if it exists
                await this._blobClient.GetBlobContainerClient(containerName).DeleteIfExistsAsync();
                this._logger.LogInformation($"Cancelled upload and deleted container {containerName}.");
            }
            catch (Exception ex)
            {
                //error
                this._logger.LogError(ex, $"Failed to cancel upload for container {containerName}.");
            }
        }

        /// <summary>
        /// Updates a tranche's editable metadata.
        /// </summary>
        public async Task EditTrancheAsync(EditTrancheRequest request)
        {
            //initialization
            ArgumentNullException.ThrowIfNull(request);
            this._logger.LogInformation($"Editing tranche {request.TrancheId}.");

            //update the matching record, which should be the only one
            TableClient tranches = this._tableClient.GetTableClient(FSPKConstants.AzureStorage.Tables.Tranches);
            await foreach (TrancheTableEntity tranche in tranches.QueryAsync<TrancheTableEntity>(t => t.RowKey == request.TrancheId.ToString()))
            {
                //apply and save the edit
                tranche.Name = request.Name;
                await tranches.UpsertEntityAsync(tranche);
            }

            //return
            this._logger.LogInformation($"Edited tranche {request.TrancheId}.");
        }

        /// <summary>
        /// Loads all bulk upload tranches tracked for a user.
        /// </summary>
        public async Task<TrancheTableEntity[]> LoadTranchesAsync(string userName)
        {
            //initialization
            ArgumentNullException.ThrowIfNullOrWhiteSpace(userName);
            TableClient tranches = this._tableClient.GetTableClient(FSPKConstants.AzureStorage.Tables.Tranches);
            await tranches.CreateIfNotExistsAsync();

            //collect every tranche owned by this user
            List<TrancheTableEntity> results = new List<TrancheTableEntity>();
            await foreach (TrancheTableEntity tranche in tranches.QueryAsync<TrancheTableEntity>(t => t.PartitionKey == userName))
                results.Add(tranche);

            //return
            this._logger.LogInformation($"Loaded {results.Pluralize("tranche")} for {userName}.");
            return results.ToArray();
        }

        /// <summary>
        /// Deletes a tranche's blob container and every partition tracking it in the Tranches and TrancheFiles tables.
        /// </summary>
        public async Task DeleteTrancheAsync(string containerName, Guid trancheId)
        {
            //initialization
            ArgumentNullException.ThrowIfNullOrWhiteSpace(containerName);
            this._logger.LogInformation($"Deleting tranche {trancheId} and container {containerName}.");

            //delete the blob container if it exists
            await this._blobClient.GetBlobContainerClient(containerName).DeleteIfExistsAsync();

            //delete every tracked file for this tranche
            TableClient trancheFiles = this._tableClient.GetTableClient(FSPKConstants.AzureStorage.Tables.TrancheFiles);
            List<TrancheFileTableEntity> files = new List<TrancheFileTableEntity>();
            await foreach (TrancheFileTableEntity file in trancheFiles.QueryAsync<TrancheFileTableEntity>(f => f.PartitionKey == trancheId.ToString()))
                files.Add(file);
            if (files.Count > 0)
                await trancheFiles.PerformBulkTableTansactionAsync(files, TableTransactionActionType.Delete);

            //delete the tranche record itself, which should be the only match
            TableClient tranches = this._tableClient.GetTableClient(FSPKConstants.AzureStorage.Tables.Tranches);
            await foreach (TrancheTableEntity tranche in tranches.QueryAsync<TrancheTableEntity>(t => t.RowKey == trancheId.ToString()))
                await tranches.DeleteEntityAsync(tranche.PartitionKey, tranche.RowKey);

            //return
            this._logger.LogInformation($"Deleted tranche {trancheId} and container {containerName}.");
        }
        #endregion
        #region Private Methods
        /// <summary>
        /// Produces a valid Azure blob container name fragment from a user name.
        /// </summary>
        private string SanitizeContainerName(string userName)
        {
            //initialization
            string defaultUserName = "anonymous";
            StringBuilder builder = new StringBuilder();

            //default user name
            if (string.IsNullOrWhiteSpace(userName))
                userName = defaultUserName;

            //process user name
            userName = userName.ToLowerInvariant();
            string loweredUserName = (userName ?? defaultUserName).ToLowerInvariant();

            //remove domains from user name
            if (loweredUserName.Contains("@"))
                loweredUserName = loweredUserName.Split('@').First();
            if (loweredUserName.Contains("\\"))
                loweredUserName = loweredUserName.Split('\\').Last();

            //replace every run of invalid characters with a single hyphen
            foreach (char character in loweredUserName)
            {
                //keep lowercase alphanumerics, otherwise collapse invalid runs into a single hyphen
                if ((character >= 'a' && character <= 'z') || (character >= '0' && character <= '9'))
                    builder.Append(character);
                else if (builder.Length > 0 && builder[builder.Length - 1] != '-')
                    builder.Append('-');
            }

            //trim leading and trailing hyphens
            string sanitized = builder.ToString().Trim('-');

            //fall back to a default when nothing usable remains
            if (string.IsNullOrWhiteSpace(sanitized))
                sanitized = defaultUserName;

            //truncate the user portion so the full "name-{guid}" fits within the container name limit (a "N" guid is 32 characters plus a hyphen)
            int maxUserLength = FSPKConstants.AzureStorage.Blobs.MaxContainerNameLength - Guid.Empty.ToString("N").Length - 1;
            if (sanitized.Length > maxUserLength)
                sanitized = sanitized.Substring(0, maxUserLength).TrimEnd('-');

            //guard against truncation leaving an empty fragment
            if (string.IsNullOrWhiteSpace(sanitized))
                sanitized = defaultUserName;

            //return
            return sanitized;
        }
        #endregion
    }
}
