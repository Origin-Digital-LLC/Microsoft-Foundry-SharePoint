using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
using FoundrySharePointKnowledge.Domain.Foundry.VectorStores;

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
        private readonly IFoundryService _foundryService;
        #endregion
        #region Initialization
        public TrancheService(BlobServiceClient blobClient,
                              TableServiceClient tableClient,
                              ILogger<TrancheService> logger,
                              IFoundryService foundryService)
        {
            //initialization
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this._blobClient = blobClient ?? throw new ArgumentNullException(nameof(blobClient));
            this._tableClient = tableClient ?? throw new ArgumentNullException(nameof(tableClient));
            this._foundryService = foundryService ?? throw new ArgumentNullException(nameof(foundryService));
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
                tranche.BlobTotalSize = totalSize;
                tranche.BlobFileCount = entities.Count;
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
                //apply the edit
                tranche.Name = request.Name;
                tranche.Status = request.Status;
                tranche.IndexBatchIds = request.IndexBatchIds;
                tranche.VectorStoreId = request.VectorStoreId;
                tranche.BlobFileCount = request.BlobFileCount;
                tranche.BlobTotalSize = request.BlobTotalSize;
                tranche.UploadedFileSize = request.UploadedFileSize;
                tranche.UploadedFileCount = request.UploadedFileCount;
                tranche.UploadingFileCount = request.UploadingFileCount;
                tranche.IndexedFileProgress = request.IndexedFileProgress;
                tranche.UploadedFileProgress = request.UploadedFileProgress;

                //save
                await tranches.UpsertEntityAsync(tranche);
            }

            //return
            this._logger.LogInformation($"Edited tranche {request.TrancheId}.");
        }

        /// <summary>
        /// Uploads a tranche's blobs into the Foundry project, recording its progress against the tranche as
        /// it goes; this is meant to be run in the background, so it reports failure through the tranche's
        /// progress rather than by throwing.
        /// </summary>
        public async Task UploadTrancheFilesAsync(UploadFilesRequest request)
        {
            //initialization
            ArgumentNullException.ThrowIfNull(request);
            int lastReportedPercentage = -1;
            this._logger.LogInformation($"Starting the background upload of tranche {request.TrancheId}.");

            //this records whole percentage points only, since a per-file write would hammer the table for
            //progress the polling UI cannot resolve anyway
            async Task reportProgressAsync(int completedFiles, int totalFiles)
            {
                //guard
                if (totalFiles <= 0)
                    return;

                //files upload in parallel, so readings arrive out of order; claim this percentage point only
                //if it beats every point already recorded, otherwise the bar would jump backwards
                double progress = (double)completedFiles / totalFiles;
                int percentage = (int)(progress * 100);
                int previous = lastReportedPercentage;

                while (percentage > previous)
                {
                    //take the point unless another file claimed a higher one first
                    int claimed = Interlocked.CompareExchange(ref lastReportedPercentage, percentage, previous);
                    if (claimed == previous)
                    {
                        //record the total alongside the progress, since it is the only thing telling a caller
                        //how many files this upload is working through once a prefix has narrowed them down
                        await this.UpdateTrancheAsync(request.TrancheId, tranche =>
                        {
                            //never record an in-flight upload as completed, since the tranche only advances
                            //once every file identifier has been persisted for it
                            tranche.UploadingFileCount = totalFiles;
                            tranche.UploadedFileProgress = Math.Min(progress, FSPKConstants.Blazor.Synchronization.MaxInFlightProgress);
                        });

                        return;
                    }

                    //another file moved the needle, so weigh this reading against where it landed
                    previous = claimed;
                }
            }

            try
            {
                //upload the tranche's blobs into the foundry project
                UploadFilesResponse response = await this._foundryService.UploadVectorStoreFilesAsync(request, reportProgressAsync);
                if (response == null || !string.IsNullOrWhiteSpace(response.Error))
                {
                    //error
                    this._logger.LogError($"Failed the background upload of tranche {request.TrancheId}: {response?.Error}.");
                    await this.UpdateTrancheAsync(request.TrancheId, tranche => tranche.UploadedFileProgress = FSPKConstants.Blazor.Synchronization.FailedProgress);
                    return;
                }

                //stamp every uploaded file identifier onto the tranche's tracked files, since indexing reads
                //them back from there rather than from this operation's result
                Dictionary<string, string> fileIds = response.FileIds ?? new Dictionary<string, string>();
                await this.UpdateFilesAsync(new UpdateFilesRequest(request.TrancheId, fileIds));

                //advance the tranche only once its files can actually be indexed
                await this.UpdateTrancheAsync(request.TrancheId, tranche =>
                {
                    //apply the upload
                    tranche.Status = TrancheStatus.FilesUploaded;
                    tranche.UploadedFileCount = fileIds.Count;
                    tranche.UploadedFileSize = response.TotalSize;
                    tranche.UploadedFileProgress = FSPKConstants.Blazor.Synchronization.CompletedProgress;
                });

                //return
                this._logger.LogInformation($"Finished the background upload of {fileIds.Pluralize("file")} for tranche {request.TrancheId}.");
            }
            catch (Exception ex)
            {
                //error
                this._logger.LogError(ex, $"Failed the background upload of tranche {request.TrancheId}.");
                await this.UpdateTrancheAsync(request.TrancheId, tranche => tranche.UploadedFileProgress = FSPKConstants.Blazor.Synchronization.FailedProgress);
            }
        }

        /// <summary>
        /// Indexes a tranche's uploaded files into its vector store, one batch at a time: each batch is started
        /// only once the batch before it has settled, so a tranche of any size never has more than a single
        /// batch in flight. This is meant to be run in the background, so it reports failure through the
        /// tranche's progress rather than by throwing.
        /// </summary>
        public async Task IndexTrancheFilesAsync(IndexFilesRequest request)
        {
            //initialization
            ArgumentNullException.ThrowIfNull(request);
            this._logger.LogInformation($"Starting the background indexing of tranche {request.TrancheId}.");

            try
            {
                //the identifiers were recorded when the tranche's files were uploaded
                Dictionary<string, string> fileIds = await this.LoadFileIdsAsync(request.TrancheId);
                if (fileIds.Count == 0)
                {
                    //error
                    this._logger.LogError($"Tranche {request.TrancheId} has no uploaded files to index.");
                    await this.UpdateTrancheAsync(request.TrancheId, tranche => tranche.IndexedFileProgress = FSPKConstants.Blazor.Synchronization.FailedProgress);
                    return;
                }

                //split the files across as many batches as the vector store's per-batch cap requires
                int settledFiles = 0;
                List<string> batchIds = new List<string>();
                string[][] chunks = fileIds.Values.Chunk(FSPKConstants.Foundry.VectorStores.MaxIndexBatchSize).ToArray();
                this._logger.LogInformation($"Indexing {fileIds.Pluralize("file")} for tranche {request.TrancheId} across {chunks.Pluralize("batch", "es")}.");

                //work through the batches in series
                foreach (string[] chunk in chunks)
                {
                    //start this batch, recording it against the tranche so what ran stays auditable
                    string batchId = await this._foundryService.AddIndexBatchAsync(request.VectorStoreId, chunk);
                    batchIds.Add(batchId);
                    await this.UpdateTrancheAsync(request.TrancheId, tranche => tranche.IndexBatches = batchIds.ToArray());

                    //poll this batch alone until it settles
                    IndexProgressRequest progressRequest = new IndexProgressRequest(request.VectorStoreId, new string[] { batchId });
                    IndexStatus status = await this.PollIndexBatchAsync(request.TrancheId, progressRequest, settledFiles, chunk.Length, fileIds.Count);

                    //stop the whole operation on the first batch that does not complete, since the batches
                    //after it would only be indexing into a vector store the caller is about to reset
                    if (status != IndexStatus.Completed)
                    {
                        //error
                        double failedProgress = status == IndexStatus.Cancelled ? FSPKConstants.Blazor.Synchronization.CancelledProgress : FSPKConstants.Blazor.Synchronization.FailedProgress;
                        this._logger.LogError($"Batch {batchId} of tranche {request.TrancheId} finished with status {status}, so the remaining batches were abandoned.");

                        //return
                        await this.UpdateTrancheAsync(request.TrancheId, tranche => tranche.IndexedFileProgress = failedProgress);
                        return;
                    }

                    //carry this batch's files into the progress reported by the batches after it
                    settledFiles += chunk.Length;
                }

                //advance the tranche now that every batch has completed
                await this.UpdateTrancheAsync(request.TrancheId, tranche =>
                {
                    //apply the indexing
                    tranche.Status = TrancheStatus.FilesIndexed;
                    tranche.IndexedFileProgress = FSPKConstants.Blazor.Synchronization.CompletedProgress;
                });

                //return
                this._logger.LogInformation($"Finished the background indexing of {fileIds.Pluralize("file")} for tranche {request.TrancheId}.");
            }
            catch (Exception ex)
            {
                //error
                this._logger.LogError(ex, $"Failed the background indexing of tranche {request.TrancheId}.");
                await this.UpdateTrancheAsync(request.TrancheId, tranche => tranche.IndexedFileProgress = FSPKConstants.Blazor.Synchronization.FailedProgress);
            }
        }

        /// <summary>
        /// Loads a single bulk upload tranche.
        /// </summary>
        public async Task<TrancheTableEntity> LoadTrancheAsync(Guid trancheId)
        {
            //return the matching record, which should be the only one
            TableClient tranches = this._tableClient.GetTableClient(FSPKConstants.AzureStorage.Tables.Tranches);
            await foreach (TrancheTableEntity tranche in tranches.QueryAsync<TrancheTableEntity>(t => t.RowKey == trancheId.ToString()))
                return tranche;

            //return
            return null;
        }

        /// <summary>
        /// Records the vector store file identifiers produced by an upload against a tranche's tracked files.
        /// </summary>
        public async Task<UpdateFilesResponse> UpdateFilesAsync(UpdateFilesRequest request)
        {
            //initialization
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.FileIds);
            this._logger.LogInformation($"Updating {request.FileIds.Pluralize("file")} for tranche {request.TrancheId}.");

            //this mirrors the file name transformations applied when files are uploaded to a vector store
            string toUploadedFileName(string rowKey)
            {
                //undo the row key sanitization, since uploads replace path separators with hyphens
                string fileName = rowKey.Replace('!', '-');

                //plaintext files are uploaded with an explicit TXT extension
                switch (Path.GetExtension(fileName).ToLowerInvariant())
                {
                    //represent all plaintext files explicitly as TXT
                    case FSPKConstants.Extensions.CSV:
                    case FSPKConstants.Extensions.XML:
                    case FSPKConstants.Extensions.JSON:
                        fileName = $"{fileName}{FSPKConstants.Extensions.TXT}";
                        break;
                }

                //return
                return fileName;
            }

            //collect every file tracked for this tranche
            TableClient trancheFiles = this._tableClient.GetTableClient(FSPKConstants.AzureStorage.Tables.TrancheFiles);
            Dictionary<string, TrancheFileTableEntity> files = new Dictionary<string, TrancheFileTableEntity>();
            await foreach (TrancheFileTableEntity file in trancheFiles.QueryAsync<TrancheFileTableEntity>(f => f.PartitionKey == request.TrancheId.ToString()))
            {
                //index each file by its row key and by the name it was uploaded under
                files[file.RowKey] = file;
                files[toUploadedFileName(file.RowKey)] = file;
            }

            //match every uploaded file to its tracked record, keyed by row key so no record is updated twice
            Dictionary<string, TrancheFileTableEntity> matches = new Dictionary<string, TrancheFileTableEntity>();
            foreach (KeyValuePair<string, string> fileId in request.FileIds)
            {
                //find the tracked record for this uploaded file
                if (!files.TryGetValue(fileId.Key, out TrancheFileTableEntity file) && !files.TryGetValue(fileId.Key.ToTableRowKey(), out file))
                {
                    //no match
                    this._logger.LogWarning($"Unable to match uploaded file {fileId.Key} to a tracked file in tranche {request.TrancheId}.");
                    continue;
                }

                //collect the match
                file.FileId = fileId.Value;
                matches[file.RowKey] = file;
            }

            //save every match
            if (matches.Count > 0)
                await trancheFiles.PerformBulkTableTansactionAsync(matches.Values.ToList(), TableTransactionActionType.UpsertReplace);

            //return
            this._logger.LogInformation($"Updated {matches.Pluralize("file")} of {request.FileIds.Pluralize("uploaded file")} for tranche {request.TrancheId}.");
            return new UpdateFilesResponse();
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
            {
                //delete vector store
                if (!string.IsNullOrWhiteSpace(tranche.VectorStoreId))
                    await this._foundryService.DeleteVectorStoreAsync(tranche.VectorStoreId);

                //delete trache
                await tranches.DeleteEntityAsync(tranche.PartitionKey, tranche.RowKey);
            }

            //return
            this._logger.LogInformation($"Deleted tranche {trancheId} and container {containerName}.");
        }
        #endregion
        #region Private Methods
        /// <summary>
        /// Loads the Foundry file identifiers recorded against a tranche's tracked files, keyed by the name
        /// each file was uploaded under.
        /// </summary>
        private async Task<Dictionary<string, string>> LoadFileIdsAsync(Guid trancheId)
        {
            //collect every tracked file that has been uploaded
            TableClient trancheFiles = this._tableClient.GetTableClient(FSPKConstants.AzureStorage.Tables.TrancheFiles);
            Dictionary<string, string> fileIds = new Dictionary<string, string>();
            await foreach (TrancheFileTableEntity file in trancheFiles.QueryAsync<TrancheFileTableEntity>(f => f.PartitionKey == trancheId.ToString()))
                if (!string.IsNullOrWhiteSpace(file.FileId))
                    fileIds[file.RowKey] = file.FileId;

            //return
            this._logger.LogInformation($"Loaded {fileIds.Pluralize("file identifier")} for tranche {trancheId}.");
            return fileIds;
        }

        /// <summary>
        /// Polls one indexing batch until it settles, recording the whole operation's progress against the
        /// tranche as it goes so a caller watching the tranche sees one continuous climb across every batch.
        /// </summary>
        private async Task<IndexStatus> PollIndexBatchAsync(Guid trancheId, IndexProgressRequest request, int settledFiles, int batchFiles, int totalFiles)
        {
            //initialization
            int checks = 0;

            //poll until the batch settles
            while (true)
            {
                //a request that could not be read at all is indistinguishable from a failed batch
                IndexProgressResponse progress = await this._foundryService.GetIndexOperationProgressAsync(request);
                if (progress == null || progress.IsError)
                    return IndexStatus.Failed;

                //scale this batch's own progress into the share of the tranche's files it accounts for, so the
                //batches already behind it are never rolled back; the first batch reports nothing terminal yet,
                //so hold the floor at the value marking an operation as started yet unsettled, which a reader
                //would otherwise take to mean nothing had been asked of the tranche at all
                double overallProgress = (settledFiles + (progress.TotalProgress * batchFiles)) / totalFiles;
                double reportedProgress = Math.Clamp(overallProgress, FSPKConstants.Blazor.Synchronization.IndexingStarted, FSPKConstants.Blazor.Synchronization.MaxInFlightProgress);
                await this.UpdateTrancheAsync(trancheId, tranche => tranche.IndexedFileProgress = reportedProgress);

                //return
                if (progress.Status != IndexStatus.InProgress)
                    return progress.Status;

                //a batch that never settles must not hold the queue forever
                checks++;
                if (checks >= FSPKConstants.Foundry.VectorStores.MaxIndexingChecks)
                {
                    //error
                    this._logger.LogError($"Batch {request} of tranche {trancheId} timed out after {checks.Pluralize("check")}.");
                    return IndexStatus.Failed;
                }

                //wait
                await Task.Delay(FSPKConstants.Foundry.VectorStores.BatchPollingWaitMilliseconds);
            }
        }

        /// <summary>
        /// Applies a change to a single tranche, reading the record back first so a caller owning only some of
        /// its fields never overwrites the rest with stale values.
        /// </summary>
        private async Task UpdateTrancheAsync(Guid trancheId, Action<TrancheTableEntity> apply)
        {
            //update the matching record, which should be the only one
            TableClient tranches = this._tableClient.GetTableClient(FSPKConstants.AzureStorage.Tables.Tranches);
            await foreach (TrancheTableEntity tranche in tranches.QueryAsync<TrancheTableEntity>(t => t.RowKey == trancheId.ToString()))
            {
                //apply the change
                apply(tranche);

                //save
                await tranches.UpsertEntityAsync(tranche);
            }
        }

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
