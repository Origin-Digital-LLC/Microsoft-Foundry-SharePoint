using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

using Azure;
using Azure.Core;
using Azure.Data.Tables;
using Azure.AI.Inference;
using Azure.Storage.Blobs;
using Azure.Search.Documents;
using Azure.Storage.Blobs.Models;
using Azure.AI.DocumentIntelligence;
using Azure.Search.Documents.Models;
using Azure.Search.Documents.Indexes;
using Azure.Security.KeyVault.Secrets;
using Azure.Search.Documents.Indexes.Models;

using FoundrySharePointKnowledge.Common;
using FoundrySharePointKnowledge.Domain.Search;
using FoundrySharePointKnowledge.Domain.Settings;
using FoundrySharePointKnowledge.Domain.Contracts;
using FoundrySharePointKnowledge.Domain.SharePoint;
using FoundrySharePointKnowledge.Domain.Search.WebAPISkill;
using FoundrySharePointKnowledge.Domain.Search.WebAPISkill.ProperCase;
using FoundrySharePointKnowledge.Domain.Search.WebAPISkill.ImageVectorization;

namespace FoundrySharePointKnowledge.Infrastructure.Services
{
    /// <summary>
    /// This deploys the Azure Search index and uploads documents to it.
    /// </summary>
    public class SearchService : ISearchService
    {
        #region Members
        private readonly BlobServiceClient _blobClient;
        private readonly ILogger<SearchService> _logger;
        private readonly TableServiceClient _tableClient;
        private readonly FoundrySettings _foundrySettings;
        private readonly EntraIDSettings _entraIDSettings;
        private readonly AzureSearchSettings _searchSettings;
        private readonly SearchIndexClient _searchIndexClient;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ISharePointService _sharePointService;
        private readonly SearchIndexerClient _searchIndexerClient;
        private readonly ImageEmbeddingsClient _imageEmbeddingsClient;
        private readonly ILogger<KeyVaultService> _sourceKeyVaultLogger;
        private readonly Dictionary<string, SearchClient> _searchClients;
        private readonly DocumentIntelligenceClient _documentIntelligenceClient;
        #endregion
        #region Initialization
        public SearchService(BlobServiceClient blobClient,
                             ILogger<SearchService> logger,
                             TableServiceClient tableClient,
                             FoundrySettings foundrySettings,
                             EntraIDSettings entraIDSettings,
                             AzureSearchSettings searchSettings,
                             SearchIndexClient searchIndexClient,
                             ISharePointService sharePointService,
                             IHttpClientFactory httpClientFactory,
                             SearchIndexerClient searchIndexerClient,
                             ImageEmbeddingsClient imageEmbeddingsClient,
                             ILogger<KeyVaultService> sourceKeyVaultLogger,
                             Dictionary<string, SearchClient> searchClients,
                             DocumentIntelligenceClient documentIntelligenceClient)
        {
            //initialization
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this._blobClient = blobClient ?? throw new ArgumentNullException(nameof(blobClient));
            this._tableClient = tableClient ?? throw new ArgumentNullException(nameof(tableClient));
            this._searchClients = searchClients ?? throw new ArgumentNullException(nameof(searchClients));
            this._searchSettings = searchSettings ?? throw new ArgumentNullException(nameof(searchSettings));
            this._foundrySettings = foundrySettings ?? throw new ArgumentNullException(nameof(foundrySettings));
            this._entraIDSettings = entraIDSettings ?? throw new ArgumentNullException(nameof(entraIDSettings));
            this._searchIndexClient = searchIndexClient ?? throw new ArgumentNullException(nameof(searchIndexClient));
            this._sharePointService = sharePointService ?? throw new ArgumentNullException(nameof(sharePointService));
            this._httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            this._searchIndexerClient = searchIndexerClient ?? throw new ArgumentNullException(nameof(searchIndexerClient));
            this._sourceKeyVaultLogger = sourceKeyVaultLogger ?? throw new ArgumentNullException(nameof(sourceKeyVaultLogger));
            this._imageEmbeddingsClient = imageEmbeddingsClient ?? throw new ArgumentNullException(nameof(imageEmbeddingsClient));
            this._documentIntelligenceClient = documentIntelligenceClient ?? throw new ArgumentNullException(nameof(documentIntelligenceClient));
        }
        #endregion
        #region Public Methods
        /// <summary>
        /// Issues a simple query against the foundry index.
        /// </summary>
        public async Task<VectorizedChunk[]> SearchAsync(string query)
        {
            //initialization
            this._logger.LogInformation($"Querying search for: {query}");
            List<VectorizedChunk> results = new List<VectorizedChunk>();
            SearchOptions options = new SearchOptions()
            {
                //assemble object
                IncludeTotalCount = true
            };

            //execute search
            Response<SearchResults<VectorizedChunk>> response = await this._searchClients[FSPKConstants.Search.Indexes.Documents].SearchAsync<VectorizedChunk>(query, options);
            string searchError = await response.GetResponseErrorAsync<SearchResults<VectorizedChunk>>($"search query: {query}");
            if (!string.IsNullOrWhiteSpace(searchError))
                throw new Exception($"Search query {query} failed: {searchError}");

            //process results
            this._logger.LogInformation($"Found {response.Value.TotalCount} result(s) for {query}.");
            await foreach (SearchResult<VectorizedChunk> result in response.Value.GetResultsAsync())
                results.Add(result.Document);

            //return
            return results.ToArray();
        }

        /// <summary>
        /// Vectorizes and indexes a SharePoint file.
        /// </summary>
        [Obsolete("This method was part of an abandoned approach and has not been fully tested.")]
        public async Task<bool> InjestFileAsync(SPFile file)
        {
            try
            {
                //initialization
                this._logger.LogInformation($"Acquiring file {file?.ToString() ?? "N/A"}.");

                //chunk file
                this._logger.LogInformation($"Acquiring file {file}.");
                SPFileChunk[] fileChunks = await this.ChunkFileAsync(file);
                IndexDocumentsAction<SPFileChunk>[] indexedDocuments = new IndexDocumentsAction<SPFileChunk>[fileChunks.Length];

                //convert chunks to azure search documents
                for (int c = 0; c < fileChunks.Length; c++)
                {
                    //batch each chunk
                    SPFileChunk chunkedDocument = fileChunks[c];
                    indexedDocuments[c] = IndexDocumentsAction.Upload(chunkedDocument);
                }

                //index chunked documents
                IndexDocumentsBatch<SPFileChunk> batch = IndexDocumentsBatch.Create(indexedDocuments);
                Response<IndexDocumentsResult> response = await this._searchClients[FSPKConstants.Search.Indexes.Vectorized].IndexDocumentsAsync(batch);

                //return
                return await this.ProcessIndexResponseAsync(response, "index chunked document");
            }
            catch (Exception ex)
            {
                //error
                this._logger.LogCritical(ex, $"Unable to index file {file?.Name ?? "N/A"} to search index {FSPKConstants.Search.Indexes.Vectorized}.");
                return false;
            }
        }

        /// <summary>
        /// Uploads a SharePoint file to Azure blob storage.
        /// </summary>
        public async Task<bool> UploadFileAsync(SPFile file)
        {
            try
            {
                //initialization
                this._logger.LogInformation($"Acquiring file {file?.ToString() ?? "N/A"}.");

                //download file from sharepoint
                byte[] fileContents = await this._sharePointService.GetFileContentsLeastPrivilegedAsync(file);
                if (!fileContents?.Any() ?? true)
                {
                    //error
                    this._logger.LogError($"File {file} has no content.");
                    return false;
                }

                //upload file to blob storage
                this._logger.LogInformation($"Uploading file {file} to azure storage blob container {FSPKConstants.AzureStorage.Blobs.SourceContainer}.");
                BlobContainerClient container = this._blobClient.GetBlobContainerClient(FSPKConstants.AzureStorage.Blobs.SourceContainer);
                BlobClient blob = container.GetBlobClient(file.Name);

                //build blob metadata
                Dictionary<string, string> metadata = new Dictionary<string, string>()
                {
                    //assemble dictionary
                    { nameof(file.ItemId), file.ItemId},
                    { nameof(file.DriveId), file.DriveId },
                    { nameof(file.URL), this.NormalizeURL(file.URL) }
                };

                //determine content type
                string contentType = FSPKConstants.ContentTypes.OctetStream;
                switch (Path.GetExtension(file.Name).ToLowerInvariant())
                {
                    //pdf
                    case FSPKConstants.Extensions.PDF:
                        contentType = FSPKConstants.ContentTypes.PDF;
                        break;

                    //csv
                    case FSPKConstants.Extensions.CSV:
                        contentType = FSPKConstants.ContentTypes.CSV;
                        break;

                    //json
                    case FSPKConstants.Extensions.JSON:
                        contentType = FSPKConstants.ContentTypes.JSON;
                        break;

                    //word
                    case FSPKConstants.Extensions.DOC:
                    case FSPKConstants.Extensions.DOCX:
                        contentType = FSPKConstants.ContentTypes.Word;
                        break;

                    //excel
                    case FSPKConstants.Extensions.XLS:
                    case FSPKConstants.Extensions.XLSX:
                        contentType = FSPKConstants.ContentTypes.Excel;
                        break;

                    //text
                    case FSPKConstants.Extensions.TXT:
                        contentType = FSPKConstants.ContentTypes.PlainText;
                        break;

                    //power point
                    case FSPKConstants.Extensions.PPT:
                    case FSPKConstants.Extensions.PPTX:
                        contentType = FSPKConstants.ContentTypes.PowerPoint;
                        break;
                }

                //upsert blob
                await container.CreateIfNotExistsAsync();
                Response<BlobContentInfo> blobResult = await blob.UploadAsync(new MemoryStream(fileContents), new BlobHttpHeaders() { ContentType = contentType }, metadata);

                //check result
                string blobError = await blobResult.GetResponseErrorAsync<BlobContentInfo>($"upsert blob {file}");
                if (!string.IsNullOrWhiteSpace(blobError))
                    throw new Exception($"Failed to upsert blob {file}: {blobError}");

                //return
                this._logger.LogInformation($"Successfully uploaded file {file} to azure storage blob container {FSPKConstants.AzureStorage.Blobs.SourceContainer}.");
                return true;
            }
            catch (Exception ex)
            {
                //error
                this._logger.LogCritical(ex, $"Unable to upload file {file?.URL ?? "N/A"} to azure storage blob container {FSPKConstants.AzureStorage.Blobs.SourceContainer}.");
                return false;
            }
        }

        /// <summary>
        /// Deletes a file from Azure Search and blob storage.
        /// </summary>
        public async Task<bool> DeleteFileAsync(SPFile file)
        {
            try
            {
                //initialization
                if (string.IsNullOrWhiteSpace(file?.URL))
                {
                    //error
                    this._logger.LogError("Cannot delete a null file.");
                    return false;
                }

                //open logging scope
                using (this._logger.BeginScope(new Dictionary<string, object>
                {
                    //assemble dictionary
                    { "SharePoint File", file.URL }
                }))
                {
                    //begin deletion
                    string documentErrorMessage = $"Failed to delete search documents from index {FSPKConstants.Search.Indexes.Documents}: ";
                    this._logger.LogInformation($"Deleting source file from azure storage blob container {FSPKConstants.AzureStorage.Blobs.SourceContainer}.");

                    //get clients                
                    TableClient metadataTable = this._tableClient.GetTableClient(FSPKConstants.AzureStorage.Tables.ExtractedImageMetadata);
                    BlobContainerClient metadataContainer = this._blobClient.GetBlobContainerClient(FSPKConstants.AzureStorage.Blobs.ImageContainer);
                    BlobContainerClient ingestionContainer = this._blobClient.GetBlobContainerClient(FSPKConstants.AzureStorage.Blobs.SourceContainer);

                    //get blob
                    BlobClient blob = ingestionContainer.GetBlobClient(file.Name);
                    await ingestionContainer.CreateIfNotExistsAsync();
                    await metadataContainer.CreateIfNotExistsAsync();

                    //delete blob
                    Response<bool> deleteBlobResult = await blob.DeleteIfExistsAsync();
                    string deleteBlobError = await deleteBlobResult.GetResponseErrorAsync<bool>($"delete blob {file}");
                    if (!string.IsNullOrWhiteSpace(deleteBlobError))
                        throw new Exception($"{documentErrorMessage}blob delete: {deleteBlobError}");
                    else
                        this._logger.LogInformation($"Deleted blob {blob.Uri}.");

                    //check result
                    if (deleteBlobResult.Value)
                    {
                        //create an exact match non-vectorized keyword query against the normalized url
                        string keyField = nameof(VectorizedChunk.ContentId);
                        SearchOptions documentSearchOptions = new SearchOptions()
                        {
                            //assemble object
                            VectorSearch = null,
                            SearchMode = SearchMode.All,
                            QueryType = SearchQueryType.Full,
                            Filter = $"{nameof(SPFile.URL)}{FSPKConstants.Search.Queries.Equal}'{this.NormalizeURL(file.URL).Replace(" ", FSPKConstants.Search.Queries.Space)}'"
                        };

                        //run query for documents keys with the given URL
                        documentSearchOptions.Select.Add(keyField);
                        documentSearchOptions.Select.Add(nameof(VectorizedChunk.DocumentId));
                        this._logger.LogInformation($"Searching for chunks via {documentSearchOptions.Filter}.");
                        Response<SearchResults<VectorizedChunk>> documentsSearchResult = await this._searchClients[FSPKConstants.Search.Indexes.Documents].SearchAsync<VectorizedChunk>(null, documentSearchOptions);

                        //check result
                        string documentSearchError = await documentsSearchResult.GetResponseErrorAsync<SearchResults<VectorizedChunk>>($"search for file {file} in index {FSPKConstants.Search.Indexes.Documents}");
                        if (!string.IsNullOrWhiteSpace(documentSearchError))
                            throw new Exception($"{documentErrorMessage}search: {documentSearchError}");

                        //get document keys
                        HashSet<string> documentIds = new HashSet<string>();
                        List<string> documentsToDelete = new List<string>();
                        await foreach (SearchResult<VectorizedChunk> result in documentsSearchResult.Value.GetResultsAsync())
                        {
                            //collect ids
                            documentIds.Add(result.Document.DocumentId);
                            documentsToDelete.Add(result.Document.ContentId);
                        }

                        //check documents (the search API will throw if the collection is empty)
                        if (!documentsToDelete.Any())
                            throw new Exception($"{documentErrorMessage}search results: file not found");
                        else
                            this._logger.LogInformation($"Found {documentsToDelete.Count} document chunk(s) and {documentIds.Count} image chunk(s) to delete.");

                        //delete documents
                        Response<IndexDocumentsResult> deleteDocumentsResult = await this._searchClients[FSPKConstants.Search.Indexes.Documents].DeleteDocumentsAsync(keyField, documentsToDelete);
                        string deleteDocumentsError = await deleteDocumentsResult.GetResponseErrorAsync<IndexDocumentsResult>($"delete documents for {file} from index {FSPKConstants.Search.Indexes.Documents}");
                        if (!string.IsNullOrWhiteSpace(deleteDocumentsError))
                            throw new Exception($"{documentErrorMessage}index delete: {deleteDocumentsError}");
                        else
                            this._logger.LogInformation($"Deleted {documentsToDelete.Count} document chunk(s) from {FSPKConstants.Search.Indexes.Documents}.");

                        //delete images
                        foreach (string documentId in documentIds)
                        {
                            //get table representation of each image
                            string tableQuery = $"{nameof(ImageTableEntity.PartitionKey)}{FSPKConstants.Search.Queries.Equal}'{documentId}'";
                            AsyncPageable<ImageTableEntity> imageTableEntities = metadataTable.QueryAsync<ImageTableEntity>(tableQuery);
                            this._logger.LogInformation($"Searching for images from document chunk to delete via {tableQuery}.");

                            //the partition key is the file chuck od; the row key is the image id
                            await foreach (ImageTableEntity imageTableEntity in imageTableEntities)
                            {
                                //create query
                                SearchOptions imageSearchOptions = new SearchOptions()
                                {
                                    //assemble object
                                    VectorSearch = null,
                                    SearchMode = SearchMode.All,
                                    QueryType = SearchQueryType.Full,
                                    Filter = $"{keyField}{FSPKConstants.Search.Queries.Equal}'{imageTableEntity.RowKey}'"
                                };

                                //run query for images with keys starting with each deleted document id
                                imageSearchOptions.Select.Add(keyField);
                                this._logger.LogInformation($"Searching for extracted images via {imageSearchOptions.Filter}.");
                                string imageErrorMessage = $"Failed to delete search documents for image {documentId} from index {FSPKConstants.Search.Indexes.Images}: ";
                                Response<SearchResults<VectorizedImage>> imageSearchResult = await this._searchClients[FSPKConstants.Search.Indexes.Images].SearchAsync<VectorizedImage>(null, imageSearchOptions);

                                //check result
                                string imageSearchError = await imageSearchResult.GetResponseErrorAsync<SearchResults<VectorizedImage>>($"search for image {documentId} in index {FSPKConstants.Search.Indexes.Images}");
                                if (!string.IsNullOrWhiteSpace(imageSearchError))
                                    throw new Exception($"{imageErrorMessage}search: {imageSearchError}");

                                //get image keys
                                List<string> imagesToDelete = new List<string>();
                                await foreach (SearchResult<VectorizedImage> result in imageSearchResult.Value.GetResultsAsync())
                                    imagesToDelete.Add(result.Document.ContentId);

                                //check images (the search API will throw if the collection is empty)
                                if (!imagesToDelete.Any())
                                    throw new Exception($"{imageSearchError}search results: file not found");
                                else
                                    this._logger.LogInformation($"Found {imagesToDelete.Count} extracted image(s) to delete.");

                                //delete image documents
                                Response<IndexDocumentsResult> deleteImagesResult = await this._searchClients[FSPKConstants.Search.Indexes.Images].DeleteDocumentsAsync(keyField, imagesToDelete);
                                string deleteImagesError = await deleteImagesResult.GetResponseErrorAsync<IndexDocumentsResult>($"delete images for document {documentId} from index {FSPKConstants.Search.Indexes.Images}");
                                if (!string.IsNullOrWhiteSpace(deleteImagesError))
                                    throw new Exception($"{imageErrorMessage}index delete: {deleteImagesError}");
                                else
                                    this._logger.LogInformation($"Deleted {imagesToDelete.Count} extracted image(s) from {FSPKConstants.Search.Indexes.Images}.");

                                //delete table entity
                                Response deleteImageResult = await metadataTable.DeleteEntityAsync(imageTableEntity.PartitionKey, imageTableEntity.RowKey);
                                if (deleteImageResult.IsError)
                                    throw new Exception($"{imageErrorMessage}table delete: {deleteImageResult.ReasonPhrase}");
                                else
                                    this._logger.LogInformation($"Deleted parition {imageTableEntity.PartitionKey} and row {imageTableEntity.RowKey} from {metadataTable.Uri}.");

                                //get extracted images
                                AsyncPageable<BlobHierarchyItem> extractedImages = metadataContainer.GetBlobsByHierarchyAsync(new GetBlobsByHierarchyOptions()
                                {
                                    //assemble object
                                    Prefix = $"{imageTableEntity.PartitionKey.TrimEnd('/')}/"
                                });

                                //delete extracted images
                                this._logger.LogInformation($"Getting extracted image blobs to delete under {imageTableEntity.PartitionKey} from {metadataContainer.Uri}.");
                                await foreach (BlobHierarchyItem extractedImage in extractedImages)
                                {
                                    //get extracted image
                                    string extractedImageName = extractedImage.Blob.Name;
                                    BlobClient extractedImageClient = metadataContainer.GetBlobClient(extractedImageName);

                                    //delete extracted image
                                    Response deleteExtractedImageResult = await extractedImageClient.DeleteAsync();
                                    if (deleteExtractedImageResult.IsError)
                                        throw new Exception($"{imageErrorMessage}blob {extractedImageName} delete: {deleteExtractedImageResult.ReasonPhrase}");
                                    else
                                        this._logger.LogInformation($"Deleted extracted image blob {extractedImageName} from {metadataContainer.Uri}.");
                                }
                            }
                        }

                        //return
                        this._logger.LogInformation($"Successfully deleted indexed documents.");
                        return true;
                    }
                    else
                    {
                        //error
                        this._logger.LogWarning($"File {file} was not found in azure storage blob container {FSPKConstants.AzureStorage.Blobs.SourceContainer}.");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                //error
                this._logger.LogCritical(ex, $"Unable to delete file {file?.ToString() ?? "N/A"}.");
                return false;
            }
        }       

        /// <summary>
        /// Creates a search index that expects "pre-vectorized" content. If there is an existing index with the same name, it will be deleted first.
        /// </summary>
        [Obsolete("This is no longer used. Leaving it here for posterity.")]
        public async Task<string> EnsureVectorizedIndexAsync(string indexName)
        {
            try
            {
                //initialization                
                SearchIndex index = await this.EnsureIndexAsync(indexName);

                //configure vector search compression
                this.AddSearchIndexBinaryCompression(index, FSPKConstants.Search.Compression.Binary, true, FSPKConstants.Search.Compression.DefaultOversampling, VectorSearchCompressionRescoreStorageMethod.DiscardOriginals);
                this.AddSearchIndexScalarCompression(index, FSPKConstants.Search.Compression.Scalar, true, FSPKConstants.Search.Compression.DefaultOversampling, VectorSearchCompressionRescoreStorageMethod.PreserveOriginals);

                //configure vector search algorithms
                this.AddSearchIndexVectorExhaustiveKnnAlgorithm(index, FSPKConstants.Search.Algorithms.ExhaustiveKnn, VectorSearchAlgorithmMetric.Euclidean);
                this.AddSearchIndexVectorHNSWAlgorithm(index, FSPKConstants.Search.Algorithms.Cosine.Name, VectorSearchAlgorithmMetric.Cosine, FSPKConstants.Search.Algorithms.M, FSPKConstants.Search.Algorithms.Cosine.EFSearch, FSPKConstants.Search.Algorithms.Cosine.EFConstruction);
                this.AddSearchIndexVectorHNSWAlgorithm(index, FSPKConstants.Search.Algorithms.Hamming.Name, VectorSearchAlgorithmMetric.Hamming, FSPKConstants.Search.Algorithms.M, FSPKConstants.Search.Algorithms.Hamming.EFSearch, FSPKConstants.Search.Algorithms.Hamming.EFConstruction);

                //configure vector search profile
                this.AddSearchIndexVectorProfile(index, FSPKConstants.Search.Profiles.Compressed, FSPKConstants.Search.Algorithms.Cosine.Name, FSPKConstants.Search.Compression.Scalar, null);

                //add standard fields
                this.AddStandardField(index, nameof(SPFileChunk.Id), true, true, true, false, null, null, SearchFieldDataType.String);
                this.AddStandardField(index, nameof(SPFileChunk.URL), false, false, true, false, true, null, SearchFieldDataType.String);
                this.AddStandardField(index, nameof(SPFileChunk.Name), false, false, true, false, true, null, SearchFieldDataType.String);
                this.AddStandardField(index, nameof(SPFileChunk.ItemId), false, true, true, true, true, null, SearchFieldDataType.String);
                this.AddStandardField(index, nameof(SPFileChunk.Title), false, false, true, false, true, null, SearchFieldDataType.String);
                this.AddStandardField(index, nameof(SPFileChunk.DriveId), false, true, true, true, true, null, SearchFieldDataType.String);
                this.AddStandardField(index, nameof(SPFileChunk.PageNumber), false, true, true, true, false, null, SearchFieldDataType.Int32);
                this.AddStandardField(index, nameof(SPFileChunk.SecurityData), false, false, false, false, true, null, SearchFieldDataType.String);
                this.AddStandardField(index, nameof(SPFileChunk.Content), false, false, false, false, true, LexicalAnalyzerName.EnMicrosoft, SearchFieldDataType.String);

                //add vector fields
                this.AddVectorField(index, nameof(SPFileChunk.TitleVector), FSPKConstants.Search.Vectorization.TextDimensions, FSPKConstants.Search.Profiles.Compressed);
                this.AddVectorField(index, nameof(SPFileChunk.ContentVector), FSPKConstants.Search.Vectorization.TextDimensions, FSPKConstants.Search.Profiles.Compressed);

                //return
                await this._searchIndexClient.CreateIndexAsync(index);
                return string.Empty;
            }
            catch (Exception ex)
            {
                //error
                this._logger.LogCritical(ex, "Unable to deploy search infrastructure.");
                return ex.Message;
            }
        }

        /// <summary>
        /// Creates a search index that holds SharePoint list content from an Azure table. If there is an existing index with the same name, it will be deleted first.
        /// </summary>
        public async Task<string> EnsureSharePointListItemsSearchTopographyAsync(bool deleteExisting = true)
        {
            try
            {
                //initialization
                if (!deleteExisting && await this.IndexerExistsAsync(FSPKConstants.Search.Indexers.ListItems))
                {
                    //return
                    this._logger.LogInformation($"Not deploying {FSPKConstants.Search.Indexers.ListItems} because the indexer already exists and '${nameof(deleteExisting)}' is false.");
                    return string.Empty;
                }

                //ensure index
                SearchIndex index = await this.EnsureIndexAsync(FSPKConstants.Search.Indexes.ListItems);

                //configure vector search algorithm
                this.AddSearchIndexVectorHNSWAlgorithm(index, FSPKConstants.Search.Algorithms.Cosine.Name, VectorSearchAlgorithmMetric.Cosine, FSPKConstants.Search.Algorithms.M, FSPKConstants.Search.Algorithms.Cosine.EFSearch, FSPKConstants.Search.Algorithms.Cosine.EFConstruction);

                //configure vector search vectorizer
                this.AddAzureOpenAIVectorizer(index, FSPKConstants.Search.Vectorization.OpenAI);

                //configure vector search profile
                this.AddSearchIndexVectorProfile(index, FSPKConstants.Search.Profiles.OpenAI, FSPKConstants.Search.Algorithms.Cosine.Name, null, FSPKConstants.Search.Vectorization.OpenAI);

                //get field names
                string url = nameof(SPListItemTableEntity.URL);
                string title = nameof(SPListItemTableEntity.Title);
                string rowKey = nameof(SPListItemTableEntity.RowKey);
                string siteId = nameof(SPListItemTableEntity.SiteId);
                string uniqueId = nameof(SPListItemTableEntity.UniqueId);
                string isDeleted = nameof(SPListItemTableEntity.IsDeleted);
                string timestamp = nameof(SPListItemTableEntity.Timestamp);
                string titleVector = FSPKConstants.Search.Fields.TitleVector;
                string description = nameof(SPListItemTableEntity.Description);
                string partitionKey = nameof(SPListItemTableEntity.PartitionKey);
                string descriptionVector = FSPKConstants.Search.Fields.DescriptionVector;

                //get field abstractions
                string urlAbstraction = FSPKConstants.Search.Abstration.Document.CombineURL(url);
                string titleAbstraction = FSPKConstants.Search.Abstration.Document.CombineURL(title);
                string siteIdAbstraction = FSPKConstants.Search.Abstration.Document.CombineURL(siteId);
                string timestampAbstraction = FSPKConstants.Search.Abstration.Document.CombineURL(timestamp);
                string titleVectorAbstraction = FSPKConstants.Search.Abstration.Document.CombineURL(titleVector);
                string descriptionAbstraction = FSPKConstants.Search.Abstration.Document.CombineURL(description);
                string partitionKeyAbstraction = FSPKConstants.Search.Abstration.Document.CombineURL(partitionKey);
                string descriptionVectorAbstraction = FSPKConstants.Search.Abstration.Document.CombineURL(descriptionVector);

                //add fields                
                this.AddStandardField(index, url, false, true, true, true, true, null, SearchFieldDataType.String);
                this.AddStandardField(index, title, false, false, true, false, true, null, SearchFieldDataType.String);
                this.AddStandardField(index, description, false, false, true, false, true, null, SearchFieldDataType.String);
                this.AddStandardField(index, timestamp, false, false, false, false, false, null, SearchFieldDataType.DateTimeOffset);
                this.AddStandardField(index, rowKey, false, true, true, true, true, LexicalAnalyzerName.Keyword, SearchFieldDataType.String);
                this.AddStandardField(index, siteId, false, true, true, true, true, LexicalAnalyzerName.Keyword, SearchFieldDataType.String);
                this.AddStandardField(index, uniqueId, true, true, true, false, null, LexicalAnalyzerName.Keyword, SearchFieldDataType.String);
                this.AddVectorField(index, titleVector, FSPKConstants.Search.Vectorization.TextDimensions, FSPKConstants.Search.Profiles.OpenAI);
                this.AddStandardField(index, partitionKey, false, true, true, true, true, LexicalAnalyzerName.Keyword, SearchFieldDataType.String);
                this.AddVectorField(index, descriptionVector, FSPKConstants.Search.Vectorization.TextDimensions, FSPKConstants.Search.Profiles.OpenAI);

                //rebuild index
                await this.CreateIndexAsync(index);
                await this.DeleteIndexerAsync(FSPKConstants.Search.Indexers.ListItems);
                await this.DeleteSkillsetAsync(FSPKConstants.Search.Skillsets.ListItems);
                await this.DeleteDataSourceAsync(FSPKConstants.Search.DataSources.ListItems);

                //configure text skillset
                SearchIndexerSkillset skillset = new SearchIndexerSkillset(FSPKConstants.Search.Skillsets.ListItems, new SearchIndexerSkill[]
                { 
                    //assemble array
                    this.CreateOpenAIEmbeddingSkill(FSPKConstants.Search.Skills.ListItemTitleSkill, FSPKConstants.Search.Abstration.Document, titleAbstraction, titleVector),
                    this.CreateOpenAIEmbeddingSkill(FSPKConstants.Search.Skills.ListItemDescriptionSkill, FSPKConstants.Search.Abstration.Document, descriptionAbstraction, descriptionVector),
                })
                {
                    //assemble object
                    IndexProjection = new SearchIndexerIndexProjection(
                    [
                        //assemble array
                        new SearchIndexerIndexProjectionSelector(index.Name, rowKey, FSPKConstants.Search.Abstration.Document, new InputFieldMappingEntry[]
                        {
                            //assemble object
                            new InputFieldMappingEntry(url)
                            {
                                //assemble object
                                Source = urlAbstraction
                            },
                            new InputFieldMappingEntry(title)
                            {
                                //assemble object
                                Source = titleAbstraction
                            },
                             new InputFieldMappingEntry(siteId)
                            {
                                //assemble object
                                Source = siteIdAbstraction
                            },
                            new InputFieldMappingEntry(timestamp)
                            {
                                //assemble object
                                Source = timestampAbstraction
                            },
                            new InputFieldMappingEntry(description)
                            {
                                //assemble object
                                Source = descriptionAbstraction
                            },
                            new InputFieldMappingEntry(titleVector)
                            {
                                //assemble object
                                Source = titleVectorAbstraction
                            },
                            new InputFieldMappingEntry(partitionKey)
                            {
                                //assemble object
                                Source = partitionKeyAbstraction
                            },
                            new InputFieldMappingEntry(descriptionVector)
                            {
                                //assemble object
                                Source = descriptionVectorAbstraction
                            }
                        }),
                    ])
                    {
                        //assemble object
                        Parameters = new SearchIndexerIndexProjectionsParameters()
                        {
                            //assemble object
                            ProjectionMode = IndexProjectionMode.SkipIndexingParentDocuments
                        }
                    },
                };

                //ensure azure tables
                TableClient sharePointListItemTable = this._tableClient.GetTableClient(FSPKConstants.AzureStorage.Tables.SharePointListItems);
                TableClient deltaTokens = this._tableClient.GetTableClient(FSPKConstants.AzureStorage.Tables.SharePointDeltaTokens);
                await sharePointListItemTable.CreateIfNotExistsAsync();
                await deltaTokens.CreateIfNotExistsAsync();

                //ensure last time check for webhook
                NullableResponse<SPListItemTableEntity> existingPlaceholder = await sharePointListItemTable.GetEntityIfExistsAsync<SPListItemTableEntity>(FSPKConstants.SharePoint.Placeholder, FSPKConstants.SharePoint.Placeholder);
                if (!existingPlaceholder.HasValue)
                {
                    //create a placeholder webhook delta token
                    SPListItemTableEntity placeholder = new SPListItemTableEntity();
                    placeholder.PartitionKey = FSPKConstants.SharePoint.Placeholder;
                    placeholder.Description = FSPKConstants.SharePoint.Placeholder;
                    placeholder.SiteId = FSPKConstants.SharePoint.Placeholder;
                    placeholder.RowKey = FSPKConstants.SharePoint.Placeholder;
                    placeholder.Title = FSPKConstants.SharePoint.Placeholder;
                    placeholder.URL = FSPKConstants.SharePoint.Placeholder;
                    placeholder.IsDeleted = true;

                    //this ensures the table columns are created prior to the indexer's first run (and since it starts out already deleted, it won't ever get into the index)
                    await sharePointListItemTable.AddEntityAsync<SPListItemTableEntity>(placeholder);
                }

                //create skillset
                await this.CreateSkillSetAsync(skillset);

                //create data source
                SearchIndexerDataSourceConnection dataSourceConnection = await this.CreateAzureStorageDatasourceAsync(FSPKConstants.Search.DataSources.ListItems, sharePointListItemTable.Name, false, isDeleted);

                //create indexer
                await this.CreateIndexerAsync(FSPKConstants.Search.Indexers.ListItems, dataSourceConnection, index.Name, skillset.Name);

                //return
                return string.Empty;
            }
            catch (Exception ex)
            {
                //error
                this._logger.LogCritical(ex, "Unable to deploy search infrastructure.");
                return ex.Message;
            }
        }

        /// <summary>
        /// Creates a search index that holds SharePoint documents content from a blob container. If there is an existing index with the same name, it will be deleted first.
        /// </summary>
        public async Task<string> EnsureSharePointDocumentsSearchTopographyAsync(bool deleteExisting = true)
        {
            try
            {
                //initialization
                if (!deleteExisting && await this.IndexerExistsAsync(FSPKConstants.Search.Indexers.Documents))
                {
                    //return
                    this._logger.LogInformation($"Not deploying {FSPKConstants.Search.Indexers.Documents} because the indexer already exists and '${nameof(deleteExisting)}' is false.");
                    return string.Empty;
                }

                //get documents field names
                string url = nameof(VectorizedChunk.URL);
                string text = nameof(VectorizedChunk.Text);
                string email = nameof(VectorizedChunk.Email);
                string emails = nameof(VectorizedChunk.Emails);
                string fullName = nameof(VectorizedChunk.FullName);
                string fullNames = nameof(VectorizedChunk.FullNames);
                string contentId = nameof(VectorizedChunk.ContentId);
                string documentId = nameof(VectorizedChunk.DocumentId);
                string textVector = nameof(VectorizedChunk.TextVector);
                string fullNameVector = nameof(VectorizedChunk.FullNameVector);

                //get documents field abstractions
                string allExtractedImages = FSPKConstants.Search.Abstration.Document.CombineURL(FSPKConstants.Search.Abstration.ExtractedImages).CombineURL(FSPKConstants.Search.Abstration.Star);
                string allDocumentPages = FSPKConstants.Search.Abstration.Document.CombineURL(FSPKConstants.Search.Abstration.Pages).CombineURL(FSPKConstants.Search.Abstration.Star);
                string fullNameVectorAbstraction = FSPKConstants.Search.Abstration.Document.CombineURL(FSPKConstants.Search.Abstration.FullNameVector);
                string timestampAbstraction = FSPKConstants.Search.Abstration.Document.CombineURL(FSPKConstants.Search.Fields.Timestamp);
                string verbalizedImagesAbsreaction = allExtractedImages.CombineURL(FSPKConstants.Search.Abstration.VerbalizedImage);
                string newImagesAbstraction = allExtractedImages.CombineURL(FSPKConstants.Search.Abstration.NewImages);
                string fullNamesAbstraction = FSPKConstants.Search.Abstration.Document.CombineURL(fullNames);
                string emailsAbstration = FSPKConstants.Search.Abstration.Document.CombineURL(emails);
                string urlAbstraction = FSPKConstants.Search.Abstration.Document.CombineURL(url);

                //get first field collections
                string firstEmail = emailsAbstration.CombineURL(0.ToString());
                string firstFullName = fullNamesAbstraction.CombineURL(0.ToString());

                //get image field names
                string imageVector = nameof(VectorizedImage.ImageVector);
                string imageURLVector = nameof(VectorizedImage.ImageURLVector);

                //ensure blob storage containers
                BlobContainerClient sourceContainer = this._blobClient.GetBlobContainerClient(FSPKConstants.AzureStorage.Blobs.SourceContainer);
                BlobContainerClient imageContainer = this._blobClient.GetBlobContainerClient(FSPKConstants.AzureStorage.Blobs.ImageContainer);
                TableClient metadataTable = this._tableClient.GetTableClient(FSPKConstants.AzureStorage.Tables.ExtractedImageMetadata);
                await sourceContainer.CreateIfNotExistsAsync();
                await metadataTable.CreateIfNotExistsAsync();
                await imageContainer.DeleteIfExistsAsync();

                //let the indexer create the metadata table to get the correct columns
                await metadataTable.DeleteAsync();
                await this.WaitForAdminOperationAsync(120);

                //ensure extracted images are public
                await imageContainer.CreateAsync();
                await imageContainer.SetAccessPolicyAsync(PublicAccessType.Blob);

                //configure semantic fields
                SemanticPrioritizedFields semanticFields = new SemanticPrioritizedFields();
                semanticFields.ContentFields.Add(new SemanticField(text));
                semanticFields.TitleField = new SemanticField(fullName);

                //ensure indexes
                SearchIndex imageIndex = await this.EnsureIndexAsync(FSPKConstants.Search.Indexes.Images, semanticFields);
                SearchIndex documentsIndex = await this.EnsureIndexAsync(FSPKConstants.Search.Indexes.Documents, semanticFields);

                //configure vector search algorithms
                this.AddSearchIndexVectorHNSWAlgorithm(imageIndex, FSPKConstants.Search.Algorithms.Cosine.Name, VectorSearchAlgorithmMetric.Cosine, FSPKConstants.Search.Algorithms.M, FSPKConstants.Search.Algorithms.Cosine.EFSearch, FSPKConstants.Search.Algorithms.Cosine.EFConstruction);
                this.AddSearchIndexVectorHNSWAlgorithm(documentsIndex, FSPKConstants.Search.Algorithms.Cosine.Name, VectorSearchAlgorithmMetric.Cosine, FSPKConstants.Search.Algorithms.M, FSPKConstants.Search.Algorithms.Cosine.EFSearch, FSPKConstants.Search.Algorithms.Cosine.EFConstruction);

                //configure vectorizers
                this.AddAzureOpenAIVectorizer(imageIndex, FSPKConstants.Search.Vectorization.OpenAI);
                this.AddAzureOpenAIVectorizer(documentsIndex, FSPKConstants.Search.Vectorization.OpenAI);

                //configure vector search profiles
                this.AddSearchIndexVectorProfile(imageIndex, FSPKConstants.Search.Profiles.OpenAI, FSPKConstants.Search.Algorithms.Cosine.Name, null, FSPKConstants.Search.Vectorization.OpenAI);
                this.AddSearchIndexVectorProfile(documentsIndex, FSPKConstants.Search.Profiles.OpenAI, FSPKConstants.Search.Algorithms.Cosine.Name, null, FSPKConstants.Search.Vectorization.OpenAI);

                //add documents search fields
                this.AddStandardField(documentsIndex, email, false, true, true, true, true, null, SearchFieldDataType.String);
                this.AddStandardField(documentsIndex, text, false, false, false, false, true, null, SearchFieldDataType.String);
                this.AddStandardField(documentsIndex, fullName, false, true, true, true, true, null, SearchFieldDataType.String);
                this.AddStandardField(documentsIndex, documentId, false, true, false, false, false, null, SearchFieldDataType.String);
                this.AddStandardField(documentsIndex, url, false, true, false, false, true, LexicalAnalyzerName.Keyword, SearchFieldDataType.String);
                this.AddStandardField(documentsIndex, contentId, true, true, true, false, true, LexicalAnalyzerName.Keyword, SearchFieldDataType.String);
                this.AddVectorField(documentsIndex, textVector, FSPKConstants.Search.Vectorization.TextDimensions, FSPKConstants.Search.Profiles.OpenAI);
                this.AddVectorField(documentsIndex, fullNameVector, FSPKConstants.Search.Vectorization.TextDimensions, FSPKConstants.Search.Profiles.OpenAI);
                this.AddStandardField(documentsIndex, emails, false, false, false, false, false, null, SearchFieldDataType.Collection(SearchFieldDataType.String));
                this.AddStandardField(documentsIndex, fullNames, false, false, false, false, false, null, SearchFieldDataType.Collection(SearchFieldDataType.String));
                this.AddStandardField(documentsIndex, FSPKConstants.Search.Fields.Timestamp, false, false, false, false, false, null, SearchFieldDataType.DateTimeOffset);

                //rebuild documents index
                await this.CreateIndexAsync(documentsIndex);
                await this.DeleteIndexerAsync(FSPKConstants.Search.Indexers.Documents);
                await this.DeleteSkillsetAsync(FSPKConstants.Search.Skillsets.Documents);
                await this.DeleteDataSourceAsync(FSPKConstants.Search.DataSources.Documents);

                //add image search fields
                this.AddStandardField(imageIndex, email, false, true, true, true, true, null, SearchFieldDataType.String);
                this.AddStandardField(imageIndex, text, false, false, false, false, true, null, SearchFieldDataType.String);
                this.AddStandardField(imageIndex, fullName, false, true, true, true, true, null, SearchFieldDataType.String);
                this.AddStandardField(imageIndex, url, false, true, false, false, true, LexicalAnalyzerName.Keyword, SearchFieldDataType.String);
                this.AddStandardField(imageIndex, contentId, true, true, true, false, true, LexicalAnalyzerName.Keyword, SearchFieldDataType.String);
                this.AddVectorField(imageIndex, textVector, FSPKConstants.Search.Vectorization.TextDimensions, FSPKConstants.Search.Profiles.OpenAI);
                this.AddVectorField(imageIndex, imageVector, FSPKConstants.Search.Vectorization.TextDimensions, FSPKConstants.Search.Profiles.OpenAI);
                this.AddVectorField(imageIndex, fullNameVector, FSPKConstants.Search.Vectorization.TextDimensions, FSPKConstants.Search.Profiles.OpenAI);
                this.AddVectorField(imageIndex, imageURLVector, FSPKConstants.Search.Vectorization.TextDimensions, FSPKConstants.Search.Profiles.OpenAI);
                this.AddStandardField(imageIndex, emails, false, false, false, false, false, null, SearchFieldDataType.Collection(SearchFieldDataType.String));
                this.AddStandardField(imageIndex, fullNames, false, false, false, false, false, null, SearchFieldDataType.Collection(SearchFieldDataType.String));
                this.AddStandardField(imageIndex, FSPKConstants.Search.Fields.Timestamp, false, false, false, false, false, null, SearchFieldDataType.DateTimeOffset);

                //rebuild image index
                await this.CreateIndexAsync(imageIndex);
                await this.DeleteIndexerAsync(FSPKConstants.Search.Indexers.Images);
                await this.DeleteSkillsetAsync(FSPKConstants.Search.Skillsets.Images);
                await this.DeleteDataSourceAsync(FSPKConstants.Search.DataSources.Images);

                //create data sources
                SearchIndexerDataSourceConnection imageDataSourceConnection = await this.CreateAzureStorageDatasourceAsync(FSPKConstants.Search.DataSources.Images, metadataTable.Name, false);
                SearchIndexerDataSourceConnection documentsDataSourceConnection = await this.CreateAzureStorageDatasourceAsync(FSPKConstants.Search.DataSources.Documents, sourceContainer.Name, true);

                //build entity extraction matada
                Dictionary<EntityCategory, string> entityCategories = new Dictionary<EntityCategory, string>()
                {
                    //assemble dictionary
                    { EntityCategory.Email, emails },
                    { EntityCategory.Person, fullNames }
                };

                //build image extraction metadata
                Dictionary<string, string> imageExtractionMetadata = new Dictionary<string, string>()
                {
                    //assemble dictionary
                    { email, firstEmail },
                    { fullName, firstFullName },
                    { emails, emailsAbstration },
                    { fullNames, fullNamesAbstraction },
                    { text, verbalizedImagesAbsreaction },
                    { fullNameVector, fullNameVectorAbstraction },
                    { FSPKConstants.Search.Fields.Timestamp, timestampAbstraction },
                    { url, newImagesAbstraction.CombineURL(FSPKConstants.Search.Abstration.ImagePath) },
                    { imageURLVector, allExtractedImages.CombineURL(FSPKConstants.Search.Abstration.ImagePathVector) },
                    { textVector, allExtractedImages.CombineURL(FSPKConstants.Search.Abstration.VerbalizedImageVector) }
                };

                //configure documents skillset
                SearchIndexerSkillset documentsSkillset = new SearchIndexerSkillset(FSPKConstants.Search.Skillsets.Documents, new SearchIndexerSkill[]
                { 
                    //assemble array
                    this.CreateSplitSkill(),
                    this.CreateImageExtrationSkill(),
                    this.CreateImageExtractionShaperSkill(allExtractedImages, imageContainer.Uri),
                    this.CreateEntityRecognitionSkill(EntityRecognitionSkill.SkillVersion.Latest, entityCategories),
                    this.CreateOpenAIEmbeddingSkill(FSPKConstants.Search.Skills.ContentEmbeddingSkill, allDocumentPages, allDocumentPages, textVector),
                    this.CreateImageVerbalizationSkill(allExtractedImages, FSPKConstants.Search.ImageVerbialization.SystemPrompt, FSPKConstants.Search.ImageVerbialization.UserPrompt),
                    this.CreateOpenAIEmbeddingSkill(FSPKConstants.Search.Skills.FullNameEmbeddingSkill, FSPKConstants.Search.Abstration.Document, firstFullName, FSPKConstants.Search.Abstration.FullNameVector),
                    this.CreateOpenAIEmbeddingSkill(FSPKConstants.Search.Skills.ImageVerbalizationEmbeddingSkill, allExtractedImages, verbalizedImagesAbsreaction, FSPKConstants.Search.Abstration.VerbalizedImageVector),
                    this.CreateOpenAIEmbeddingSkill(FSPKConstants.Search.Skills.ImageURLEmbeddingSkill, allExtractedImages, allExtractedImages.CombineURL(FSPKConstants.Search.Abstration.ImagePath), FSPKConstants.Search.Abstration.ImagePathVector)
                })
                {
                    //assemble object
                    IndexProjection = new SearchIndexerIndexProjection(
                    [
                        //assemble array
                        new SearchIndexerIndexProjectionSelector(documentsIndex.Name, documentId, allDocumentPages, new InputFieldMappingEntry[]
                        {
                            //assemble object                           
                            new InputFieldMappingEntry(text)
                            {
                                //assemble object
                                Source = allDocumentPages
                            },                           
                            new InputFieldMappingEntry(url)
                            {
                                //assemble object
                                Source = urlAbstraction
                            },
                            new InputFieldMappingEntry(email)
                            {
                                //assemble object
                                Source = firstEmail
                            },
                            new InputFieldMappingEntry(emails)
                            {
                                //assemble object
                                Source = emailsAbstration
                            },
                            new InputFieldMappingEntry(fullName)
                            {
                                //assemble object
                                Source = firstFullName
                            },
                            new InputFieldMappingEntry(fullNames)
                            {
                                //assemble object
                                Source = fullNamesAbstraction
                            },
                            new InputFieldMappingEntry(textVector)
                            {
                                //assemble object
                                Source = allDocumentPages.CombineURL(textVector)
                            },
                            new InputFieldMappingEntry(fullNameVector)
                            {
                                //assemble object
                                Source = fullNameVectorAbstraction
                            },
                            new InputFieldMappingEntry(FSPKConstants.Search.Fields.Timestamp)
                            {
                                //assemble object
                                Source = timestampAbstraction
                            }
                        }),
                    ])
                    {
                        //assemble object
                        Parameters = new SearchIndexerIndexProjectionsParameters()
                        {
                            //assemble object
                            ProjectionMode = IndexProjectionMode.SkipIndexingParentDocuments
                        }
                    },
                    KnowledgeStore = new KnowledgeStore(this._searchSettings.AzureStorageResourceId, new KnowledgeStoreProjection[] { this.CreateImageKnowledgeStoreProjection(contentId, allExtractedImages, imageExtractionMetadata) })
                };

                //configure image skillset
                SearchIndexerSkillset imageSkillset = new SearchIndexerSkillset(FSPKConstants.Search.Skillsets.Images, new SearchIndexerSkill[]
                {
                    //assemble array
                    this.CreateWebAPISkill(FSPKConstants.Search.Skills.ImageVectorizationSkill, FSPKConstants.Search.Abstration.Document, nameof(ImageVectorizationInput.URL), urlAbstraction, FSPKConstants.Search.Abstration.Vector, imageVector, this._searchSettings.WebAPISkillEndpoint.CombineURL(FSPKConstants.Routing.API.VectorizeImage))
                });

                //create skillsets
                await this.CreateSkillSetAsync(imageSkillset);
                await this.CreateSkillSetAsync(documentsSkillset);

                //create documents indexer
                await this.CreateIndexerAsync(FSPKConstants.Search.Indexers.Documents, documentsDataSourceConnection, documentsIndex.Name, documentsSkillset.Name, FSPKConstants.Search.Fields.MetadataStorageLastModified);

                //wait for the source indexer to run and generate metadata table columns before creating the image indexer
                await this.WaitForAdminOperationAsync(60);
                await this.CreateIndexerAsync(FSPKConstants.Search.Indexers.Images, imageDataSourceConnection, imageIndex.Name, imageSkillset.Name, null, new Dictionary<string, string>() { { FSPKConstants.Search.Abstration.Document.CombineURL(imageVector), imageVector } });

                //return
                return string.Empty;
            }
            catch (Exception ex)
            {
                //error
                this._logger.LogCritical(ex, "Unable to deploy SharePoint search topography.");
                return ex.Message;
            }
        }

        /// <summary>
        /// Vectorizes an image with Azure Vision.
        /// </summary>
        public async Task<WebAPISkillPayload<ImageVectorizationOutput>> VectorizeImagesAsync(WebAPISkillPayload<ImageVectorizationInput> payload)
        {
            //initialization
            Dictionary<string, string> urls = new Dictionary<string, string>();
            List<ImageEmbeddingInput> inputs = new List<ImageEmbeddingInput>();
            List<WebAPISkillData<ImageVectorizationOutput>> vectors = new List<WebAPISkillData<ImageVectorizationOutput>>();
            BlobContainerClient container = this._blobClient.GetBlobContainerClient(FSPKConstants.AzureStorage.Blobs.ImageContainer);
            Dictionary<string, Task<Response<BlobDownloadResult>>> blobs = new Dictionary<string, Task<Response<BlobDownloadResult>>>();

            //download all blobs
            foreach (WebAPISkillData<ImageVectorizationInput> skillRecord in payload.Values)
            {
                //get blob name
                string[] segments = new Uri(skillRecord.Payload.URL).Segments;
                string blobName = string.Join(string.Empty, segments.Skip(2));

                //start blob download
                this._logger.LogInformation($"Downloading image to vectorize {skillRecord.RecordId}: {skillRecord.Payload.URL}");
                blobs.Add(skillRecord.RecordId, container.GetBlobClient(blobName).DownloadContentAsync());
                urls.Add(skillRecord.RecordId, skillRecord.Payload.URL);
            }

            //wait for work to finish
            AggregateException downloadError = await blobs.Values.WhenAllAsync();
            if (downloadError != null)
                throw downloadError;

            //prepare vectorization inputs
            foreach (string recordId in blobs.Keys)
            {
                //get blob
                string url = urls[recordId];
                Response<BlobDownloadResult> blob = blobs[recordId].Result;
                this._logger.LogInformation($"Preparing image for vectorization {recordId}: {url}");

                //check blob
                string blobError = await blob.GetResponseErrorAsync($"download {url}");
                if (!string.IsNullOrWhiteSpace(blobError))
                    throw new Exception(blobError);

                //vectorize blob
                inputs.Add(new ImageEmbeddingInput($"{FSPKConstants.HTTP.Base64URL}{Convert.ToBase64String(blob.Value.Content.ToArray())}"));
            }

            //configure image model
            ImageEmbeddingsOptions options = new ImageEmbeddingsOptions(inputs);
            options.EncodingFormat = FSPKConstants.Foundry.EncodingFormat;
            options.Model = this._foundrySettings.ImageModel;

            //vectorize images
            Response<EmbeddingsResult> vectorizationResult = await this._imageEmbeddingsClient.EmbedAsync(options);
            string vectorizationError = await vectorizationResult.GetResponseErrorAsync("vectorize images");
            if (!string.IsNullOrWhiteSpace(vectorizationError))
                throw new Exception(vectorizationError);

            //get results
            foreach (EmbeddingItem embedding in vectorizationResult.Value.Data)
            {
                //convert to azure search skill format
                vectors.Add(new WebAPISkillData<ImageVectorizationOutput>()
                {
                    //assemble object
                    RecordId = payload.Values[embedding.Index].RecordId,
                    Payload = new ImageVectorizationOutput()
                    {
                        //convert result to a float array for azure search vector indexing
                        Vector = JsonSerializer.Deserialize<float[]>(embedding.Embedding.ToString())
                    }
                });
            }

            //return
            return new WebAPISkillPayload<ImageVectorizationOutput>()
            {
                //assemble array
                Values = vectors.ToArray()
            };
        }

        /// <summary>
        /// Converts raw text to proper case.    
        /// </summary>
        public async Task<WebAPISkillPayload<ProperCaseOutput>> ToProperCaseAsync(WebAPISkillPayload<ProperCaseInput> payload)
        {
            //initialization
            if (!payload?.Values?.Any() ?? true)
                throw new ArgumentNullException(nameof(payload));

            //build resuls
            this._logger.LogInformation($"Proper casing {payload.Values.Length} value(s).");
            List<WebAPISkillData<ProperCaseOutput>> results = new List<WebAPISkillData<ProperCaseOutput>>();

            //this ensures a string has proper casing and spacing
            string toProperCase(string text)
            {
                //initialization
                if (string.IsNullOrWhiteSpace(text))
                    return text;

                //parse string
                StringBuilder sb = new StringBuilder();
                foreach (string word in text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    //format each word
                    sb.Append(char.ToUpper(word.First()));
                    sb.Append(new string(word.Skip(1).ToArray()));

                    //put space back in
                    sb.Append(" ");
                }

                //build result
                string result = sb.ToString().Trim();
                this._logger.LogInformation($"Transformed raw text '{text}' to proper case '{result}'.");

                //return
                return result;
            }

            //get all inputs
            foreach (WebAPISkillData<ProperCaseInput> inputValue in payload.Values)
            {
                //process each input
                WebAPISkillData<ProperCaseOutput> output = new WebAPISkillData<ProperCaseOutput>()
                {
                    //assemble object
                    RecordId = inputValue.RecordId,
                    Payload = new ProperCaseOutput()
                };

                //check value
                if (string.IsNullOrWhiteSpace(inputValue?.Payload?.RawText))
                {
                    //invalid value
                    this._logger.LogWarning($"Invalid proper case input for record {inputValue?.RecordId ?? "N/A"}.");
                }
                else
                {
                    //convert text                
                    this._logger.LogInformation($"Proper casing: {inputValue}.");
                    output.Payload.ProperText = toProperCase(inputValue.Payload.RawText);
                }

                //map to output
                results.Add(output);
            }

            //return
            this._logger.LogInformation($"Proper cased {results.Count} value(s).");
            return new WebAPISkillPayload<ProperCaseOutput>()
            {
                //assemble array
                Values = results.ToArray()
            };
        }

        /// <summary>
        /// Migrates blobs and tables from one Azure Storage Account to another.
        /// </summary>
        public async Task<MigrateStorageAccountResult> MigrateStorageAccountAsync(MigrateStorageAccountRequest migrateStorageAccountsRequest)
        {
            //initialization
            List<string> errors = new List<string>();

            try
            {
                //parse source key vault uri
                if (!Uri.TryCreate(migrateStorageAccountsRequest.SourceKeyVaultURL, UriKind.Absolute, out Uri sourceKeyVaultURI))
                    throw new InvalidOperationException(nameof(migrateStorageAccountsRequest.SourceKeyVaultURL));

                //get source storage account settings
                KeyVaultService sourceKeyVaultService = new KeyVaultService(new SecretClient(sourceKeyVaultURI, this._entraIDSettings.ToCredential()), this._sourceKeyVaultLogger);
                BlobStorageSettings blobStorageSettings = await sourceKeyVaultService.GetBlobStorageSettingsAsync();

                //create source storage client options
                TableClientOptions sourceTableOptions = new TableClientOptions();
                BlobClientOptions sourceBlobOptions = new BlobClientOptions();
                sourceTableOptions.ConfigureAzureStorageOptions();
                sourceBlobOptions.ConfigureAzureStorageOptions();

                //create source storage clients
                TableServiceClient sourceTableClient = new TableServiceClient(blobStorageSettings.ConnectionString, sourceTableOptions);
                BlobServiceClient sourceBlobClient = new BlobServiceClient(blobStorageSettings.ConnectionString, sourceBlobOptions);
                string destinationBlobURL = this._blobClient.Uri.ToString().ToLowerInvariant();
                string sourceBlobURL = sourceBlobClient.Uri.ToString().ToLowerInvariant();

                //migrate tables
                foreach (string tableName in migrateStorageAccountsRequest.TableNames)
                {
                    try
                    {
                        //ensure each destination table
                        TableClient destinationTable = this._tableClient.GetTableClient(tableName);
                        TableClient sourceTable = sourceTableClient.GetTableClient(tableName);
                        List<TableEntity> destinationEntities = new List<TableEntity>();
                        await destinationTable.CreateIfNotExistsAsync();

                        //get all source entities
                        await foreach (TableEntity sourceEntity in sourceTable.QueryAsync<TableEntity>())
                        {
                            //get each entity's values
                            Dictionary<string, object> destinationValues = new Dictionary<string, object>();
                            foreach (string key in sourceEntity.Keys)
                            {
                                //check URLs
                                if (key == FSPKConstants.AzureStorage.Tables.URL)
                                {
                                    //fix blob URLs
                                    string updatedURL = sourceEntity[key]?.ToString()?.ToLowerInvariant();
                                    if (string.IsNullOrWhiteSpace(updatedURL))
                                        destinationValues.Add(key, null);
                                    else
                                        destinationValues.Add(key, updatedURL.Replace(sourceBlobURL, destinationBlobURL));
                                }
                                else
                                {
                                    //copy all other columns directly
                                    destinationValues.Add(key, sourceEntity[key]);
                                }
                            }

                            //collect entities
                            destinationEntities.Add(new TableEntity(destinationValues));
                        }

                        //bulk upsert list item entities                
                        this._logger.LogInformation($"Persisting {destinationEntities.Count} destination {tableName} entities.");
                        await destinationTable.PerformBulkTableTansactionAsync(destinationEntities);
                    }
                    catch (Exception ex)
                    {
                        //error
                        string error = $"Failed to bulk upload desination {tableName} entities";
                        this._logger.LogError(ex, $"{error}.");
                        errors.Add($"{error}: {ex.Message}.");
                    }
                }

                //migrate containers
                foreach (string containerName in migrateStorageAccountsRequest.ContainerNames)
                {
                    try
                    {
                        //get container clients
                        BlobContainerClient sourceContainer = sourceBlobClient.GetBlobContainerClient(containerName);
                        BlobContainerClient destinationContainer = this._blobClient.GetBlobContainerClient(containerName);
                        Dictionary<string, BlobDownloadResult> destinationBlobs = new Dictionary<string, BlobDownloadResult>();

                        //ensure each destination container
                        await destinationContainer.CreateIfNotExistsAsync(containerName == FSPKConstants.AzureStorage.Blobs.ImageContainer ? PublicAccessType.Blob : PublicAccessType.None);

                        //get all source blobs
                        await foreach (BlobItem blob in sourceContainer.GetBlobsAsync(new GetBlobsOptions()))
                        {
                            //download source blob
                            BlobClient sourceBlob = sourceContainer.GetBlobClient(blob.Name);
                            destinationBlobs.Add(blob.Name, await sourceBlob.DownloadContentAsync());
                        }

                        //bulk upload destination blobs
                        await Parallel.ForEachAsync(destinationBlobs, new ParallelOptions { MaxDegreeOfParallelism = FSPKConstants.AzureStorage.Blobs.Parallelism }, async (blob, _) =>
                        {
                            //get source blob
                            BlobClient destinationBlob = destinationContainer.GetBlobClient(blob.Key);
                            Dictionary<string, string> destinationMetadata = blob.Value.Details.Metadata?.ToDictionary() ?? new Dictionary<string, string>();

                            //fix URLs
                            if (destinationMetadata.TryGetValue(FSPKConstants.AzureStorage.Tables.URL, out string url) && !string.IsNullOrWhiteSpace(url))
                                destinationMetadata[FSPKConstants.AzureStorage.Tables.URL] = url.ToLowerInvariant().Replace(sourceBlobURL, destinationBlobURL);

                            //upload destination blob
                            Response<BlobContentInfo> blobResult = await destinationBlob.UploadAsync(blob.Value.Content.ToStream(), new BlobHttpHeaders() { ContentType = blob.Value.Details.ContentType }, destinationMetadata);

                            //check result
                            string blobError = await blobResult.GetResponseErrorAsync<BlobContentInfo>($"upsert blob {blob.Key}");
                            if (!string.IsNullOrWhiteSpace(blobError))
                            {
                                //error
                                string error = $"Failed to upsert blob {blob.Key}";
                                errors.Add($"{error}: {blobError}");
                                this._logger.LogError($"{error}.");
                            }
                            else
                            {
                                //success
                                this._logger.LogInformation($"Migrated blob {blob.Key} from {sourceBlobURL}.");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        //error
                        string error = $"Failed to bulk upload desination {containerName} blobs";
                        this._logger.LogError(ex, $"{error}.");
                        errors.Add($"{error}: {ex.Message}.");
                    }
                }

                //done
                this._logger.LogInformation($"Successfully migrated storage account {blobStorageSettings.Name}.");
            }
            catch (Exception ex)
            {
                //error
                string error = $"Failed to migrate storage account from {migrateStorageAccountsRequest.SourceKeyVaultURL}.";
                this._logger.LogError(ex, error);
                errors.Add(error);
            }

            //return
            return new MigrateStorageAccountResult(errors);
        }
        #endregion
        #region Private Methods
        /// <summary>
        /// Prepares a raw file for search ingestion by chunking and vectorizing its content.
        /// </summary>
        [Obsolete("This method was part of an abandoned approach and has not been fully tested.")]
        private async Task<SPFileChunk[]> ChunkFileAsync(SPFile file)
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
            byte[] fileContents = await this._sharePointService.GetFileContentsMostPrivilegedAsync(file);
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
        /// Uses Microsoft Foundry to vectorize SharePoint content.
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
                using HttpResponseMessage response = await client.PostAsync($"{FSPKConstants.Routing.Foundry.OpenAIDeployments.CombineURL(this._foundrySettings.EmbeddingDeploymentName).CombineURL(FSPKConstants.Routing.Foundry.Embedddings)}{this._foundrySettings.EmbeddingAPIVersion}",
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
        /// Deletes an existing index by name if found.
        /// </summary>
        private async Task<SearchIndex> EnsureIndexAsync(string indexName, SemanticPrioritizedFields semanticFields = null)
        {
            //initialization
            SearchIndex index = await this.GetIndexByNameAsync(indexName);

            //check existing
            if (index != null)
            {
                //delete existing index
                this._logger.LogWarning($"Deleting existing index {indexName}.");
                await this._searchIndexClient.DeleteIndexAsync(indexName);
                await this.WaitForAdminOperationAsync();
            }

            //create new index
            index = new SearchIndex(indexName);
            index.VectorSearch = new VectorSearch();

            //check semantics
            if (semanticFields != null)
            {
                //configure index semantics
                index.Similarity = new BM25Similarity();
                index.SemanticSearch = new SemanticSearch();
                index.SemanticSearch.DefaultConfigurationName = FSPKConstants.Search.Semantics.Name;
                index.SemanticSearch.Configurations.Add(new SemanticConfiguration(FSPKConstants.Search.Semantics.Name, semanticFields));
            }

            //return
            return index;
        }

        /// <summary>
        /// Deletes an existing indexer by name if found.
        /// </summary>
        private async Task DeleteIndexerAsync(string indexerName)
        {
            //initialization
            SearchIndexer indexer = await this.GetIndexerByNameAsync(indexerName);

            //check existing
            if (indexer != null)
            {
                //delete existing indexer
                this._logger.LogWarning($"Deleting existing indexer {indexerName}.");
                await this._searchIndexerClient.DeleteIndexerAsync(indexerName);

                //return
                await this.WaitForAdminOperationAsync();
            }
        }

        /// <summary>
        /// Deletes an existing data source by name if found.
        /// </summary>
        private async Task DeleteDataSourceAsync(string dataSourceName)
        {
            //initialization
            SearchIndexerDataSourceConnection dataSource = await this.GetDataSourceByNameAsync(dataSourceName);

            //check existing
            if (dataSource != null)
            {
                //delete existing data source
                this._logger.LogWarning($"Deleting existing data source {dataSourceName}.");
                await this._searchIndexerClient.DeleteDataSourceConnectionAsync(dataSourceName);

                //return
                await this.WaitForAdminOperationAsync();
            }
        }

        /// <summary>
        /// Deletes an existing skillset by name if found.
        /// </summary>
        private async Task DeleteSkillsetAsync(string skillsetName)
        {
            //initialization
            SearchIndexerSkillset skillset = await this.GetSkillsetByNameAsync(skillsetName);

            //check existing
            if (skillset != null)
            {
                //delete existing skillset
                this._logger.LogWarning($"Deleting existing skillset {skillsetName}.");
                await this._searchIndexerClient.DeleteSkillsetAsync(skillsetName);

                //return
                await this.WaitForAdminOperationAsync();
            }
        }

        /// <summary>
        /// Gets a search index by name.
        /// </summary>
        private async Task<SearchIndex> GetIndexByNameAsync(string indexName)
        {
            try
            {
                //initialization
                Response<SearchIndex> indexResponse = await this._searchIndexClient.GetIndexAsync(indexName);
                string error = await indexResponse.GetResponseErrorAsync($"Get index {indexName}");

                //return
                if (string.IsNullOrWhiteSpace(error))
                    return indexResponse.Value;
                else
                    throw new Exception(error);
            }
            catch (Exception ex)
            {
                //error
                this._logger.LogWarning(ex, $"Search index {indexName} was not found.");
                return null;
            }
        }

        /// <summary>
        /// Gets a search indexer by name.
        /// </summary>
        private async Task<SearchIndexer> GetIndexerByNameAsync(string indexerName)
        {
            try
            {
                //initialization
                Response<SearchIndexer> indexerResponse = await this._searchIndexerClient.GetIndexerAsync(indexerName);
                string error = await indexerResponse.GetResponseErrorAsync($"Get indexer {indexerName}");

                //return
                if (string.IsNullOrWhiteSpace(error))
                    return indexerResponse.Value;
                else
                    throw new Exception(error);
            }
            catch (Exception ex)
            {
                //error
                this._logger.LogWarning(ex, $"Search indexer {indexerName} was not found.");
                return null;
            }
        }

        /// <summary>
        /// Determines if a search indexer exists.
        /// </summary>
        private async Task<bool> IndexerExistsAsync(string indexerName)
        {
            //initialization
            SearchIndexer indexer = await this.GetIndexerByNameAsync(indexerName);

            //return
            return indexer != null;
        }

        /// <summary>
        /// Gets a search data source by name.
        /// </summary>
        private async Task<SearchIndexerDataSourceConnection> GetDataSourceByNameAsync(string dataSourceName)
        {
            try
            {
                //initialization
                Response<SearchIndexerDataSourceConnection> dataSourceResponse = await this._searchIndexerClient.GetDataSourceConnectionAsync(dataSourceName);
                string error = await dataSourceResponse.GetResponseErrorAsync($"Get data source {dataSourceName}");

                //return
                if (string.IsNullOrWhiteSpace(error))
                    return dataSourceResponse.Value;
                else
                    throw new Exception(error);
            }
            catch (Exception ex)
            {
                //error
                this._logger.LogWarning(ex, $"Search data source {dataSourceName} was not found.");
                return null;
            }
        }

        /// <summary>
        /// Gets a search skill set by name.
        /// </summary>
        private async Task<SearchIndexerSkillset> GetSkillsetByNameAsync(string skillsetName)
        {
            try
            {
                //initialization
                Response<SearchIndexerSkillset> skillsetResponse = await this._searchIndexerClient.GetSkillsetAsync(skillsetName);
                string error = await skillsetResponse.GetResponseErrorAsync($"Get skillset {skillsetName}");

                //return
                if (string.IsNullOrWhiteSpace(error))
                    return skillsetResponse.Value;
                else
                    throw new Exception(error);
            }
            catch (Exception ex)
            {
                //error
                this._logger.LogWarning(ex, $"Search skillset {skillsetName} was not found.");
                return null;
            }
        }

        /// <summary>
        /// Creates an index.
        /// </summary>
        private async Task CreateIndexAsync(SearchIndex index)
        {
            //initialization
            Response<SearchIndex> result = await this._searchIndexClient.CreateIndexAsync(index);
            string error = await result.GetResponseErrorAsync<SearchIndex>($"create index {index.Name}");

            //return
            if (!string.IsNullOrWhiteSpace(error))
                throw new Exception($"Failed to create index {index.Name}: {error}");
        }

        /// <summary>
        /// Creates a data source.
        /// </summary>
        private async Task CreateDatasourceAsync(SearchIndexerDataSourceConnection dataSource)
        {
            //initialization
            Response<SearchIndexerDataSourceConnection> result = await this._searchIndexerClient.CreateDataSourceConnectionAsync(dataSource);
            string error = await result.GetResponseErrorAsync<SearchIndexerDataSourceConnection>($"create data source {dataSource.Name}");

            //return
            if (!string.IsNullOrWhiteSpace(error))
                throw new Exception($"Failed to create data source {dataSource.Name}: {error}");
        }

        /// <summary>
        /// Creates a blob data source. 
        /// </summary>
        private async Task<SearchIndexerDataSourceConnection> CreateAzureStorageDatasourceAsync(string dataSourceName, string containerName, bool trueForBlobFalseForTable, string softDeleteBooleanColumnName = null)
        {
            //initialization
            SearchIndexerDataSourceType dataSourceType = trueForBlobFalseForTable ? SearchIndexerDataSourceType.AzureBlob : SearchIndexerDataSourceType.AzureTable;
            SearchIndexerDataSourceConnection dataSourceConnection = new SearchIndexerDataSourceConnection(dataSourceName, dataSourceType, this._searchSettings.AzureStorageResourceId, new SearchIndexerDataContainer(containerName));

            //configure soft delete detection
            if (!string.IsNullOrWhiteSpace(softDeleteBooleanColumnName))
                dataSourceConnection.DataDeletionDetectionPolicy = new SoftDeleteColumnDeletionDetectionPolicy()
                {
                    //assemble object
                    SoftDeleteColumnName = softDeleteBooleanColumnName,
                    SoftDeleteMarkerValue = true.ToString().ToLowerInvariant()
                };

            //create data source
            dataSourceConnection.DataChangeDetectionPolicy = new HighWaterMarkChangeDetectionPolicy(trueForBlobFalseForTable ? FSPKConstants.Search.Fields.MetadataStorageLastModified : FSPKConstants.Search.Fields.Timestamp);
            await this.CreateDatasourceAsync(dataSourceConnection);

            //return
            return dataSourceConnection;
        }

        /// <summary>
        /// Creaes a skillset.
        /// </summary>
        private async Task CreateSkillSetAsync(SearchIndexerSkillset skillset)
        {
            //initialization
            Response<SearchIndexerSkillset> result = await this._searchIndexerClient.CreateSkillsetAsync(skillset);
            string error = await result.GetResponseErrorAsync<SearchIndexerSkillset>($"create skillset {skillset.Name}");

            //return
            if (!string.IsNullOrWhiteSpace(error))
                throw new Exception($"Failed to create skillset {skillset.Name}: {error}");
        }

        /// <summary>
        /// Creates an indexer.
        /// </summary>
        private async Task CreateIndexerAsync(string indexerName, SearchIndexerDataSourceConnection dataSource, string indexName, string skillsetName, string timestampField = null, Dictionary<string, string> outputFieldMappings = null)
        {
            //initialization
            SearchIndexer indexer = new SearchIndexer(indexerName, dataSource.Name, indexName)
            {
                //assemble object
                SkillsetName = skillsetName,
                Schedule = new IndexingSchedule(TimeSpan.FromMinutes(FSPKConstants.Search.Indexers.IntervalMinutes))
            };

            //check timestamp field
            if (!string.IsNullOrWhiteSpace(timestampField))
            {
                //map blob timestamp
                indexer.FieldMappings.Add(new FieldMapping(timestampField)
                {
                    //assemble object
                    TargetFieldName = FSPKConstants.Search.Fields.Timestamp
                });
            }

            //check output field mappings
            if (outputFieldMappings?.Any() ?? false)
            {
                //get all mappings
                foreach (string ouputField in outputFieldMappings.Keys)
                {
                    //map source to target
                    indexer.OutputFieldMappings.Add(new FieldMapping(ouputField)
                    {
                        //assemble object
                        TargetFieldName = outputFieldMappings[ouputField]
                    });
                }
            }

            //configure blob indexer
            if (dataSource.Type == SearchIndexerDataSourceType.AzureBlob)
            {
                //configure indexer parameters
                indexer.Parameters = new IndexingParameters()
                {
                    //assemble object                    
                    IndexingParametersConfiguration = new IndexingParametersConfiguration()
                    {
                        //assemble object
                        AllowSkillsetToReadFileData = true,
                        ParsingMode = BlobIndexerParsingMode.Default,
                        ImageAction = BlobIndexerImageAction.GenerateNormalizedImages
                    }
                };
            }

            //create indexer
            Response<SearchIndexer> result = await this._searchIndexerClient.CreateIndexerAsync(indexer);
            string error = await result.GetResponseErrorAsync<SearchIndexer>($"create indexer {indexer.Name}");

            //return
            if (!string.IsNullOrWhiteSpace(error))
                throw new Exception($"Failed to create indexer {indexer.Name}: {error}");
        }

        /// <summary>
        /// Adds an HNSW algorithm configuration to a search index.
        /// </summary>
        private void AddSearchIndexVectorHNSWAlgorithm(SearchIndex index, string name, VectorSearchAlgorithmMetric metric, int m, int efSearch, int efConstruction)
        {
            //return
            index.VectorSearch.Algorithms.Add(new HnswAlgorithmConfiguration(name)
            {
                //assemble object
                Parameters = new HnswParameters()
                {
                    //assemble object
                    M = m,
                    Metric = metric,
                    EfSearch = efSearch,
                    EfConstruction = efConstruction
                }
            });
        }

        /// <summary>
        /// Adds an Exhaustive Knn algorithm configuration to a search index.
        /// </summary>
        private void AddSearchIndexVectorExhaustiveKnnAlgorithm(SearchIndex index, string name, VectorSearchAlgorithmMetric metric)
        {
            //return
            index.VectorSearch.Algorithms.Add(new ExhaustiveKnnAlgorithmConfiguration(name)
            {
                //assemble object
                Parameters = new ExhaustiveKnnParameters()
                {
                    //assemble object
                    Metric = metric
                }
            });
        }

        /// <summary>
        /// Adds a vector search profile to a search index.
        /// </summary>
        private void AddSearchIndexVectorProfile(SearchIndex index, string name, string algorithmName, string compressionName, string vectorizerName)
        {
            //return
            index.VectorSearch.Profiles.Add(new VectorSearchProfile(name, algorithmName)
            {
                //assemble object
                VectorizerName = vectorizerName,
                CompressionName = compressionName
            });
        }

        /// <summary>
        /// Adds vector search scalar compression to a search index.
        /// </summary>
        private void AddSearchIndexScalarCompression(SearchIndex index, string name, bool enableRescoring, int defaultOversampling, VectorSearchCompressionRescoreStorageMethod rescoreStorageMethod)
        {
            //return
            index.VectorSearch.Compressions.Add(new ScalarQuantizationCompression(name)
            {
                //assemble object
                RescoringOptions = this.BuildRescoringOptions(enableRescoring, defaultOversampling, rescoreStorageMethod),
                Parameters = new ScalarQuantizationParameters()
                {
                    //assemble object
                    QuantizedDataType = VectorSearchCompressionTarget.Int8
                }
            });
        }

        /// <summary>
        /// Adds vector search binary compression to a search index.
        /// </summary>
        private void AddSearchIndexBinaryCompression(SearchIndex index, string name, bool enableRescoring, int defaultOversampling, VectorSearchCompressionRescoreStorageMethod rescoreStorageMethod)
        {
            //return
            index.VectorSearch.Compressions.Add(new BinaryQuantizationCompression(name)
            {
                //assemble object
                RescoringOptions = this.BuildRescoringOptions(enableRescoring, defaultOversampling, rescoreStorageMethod)
            });
        }

        /// <summary>
        /// Configures rescoring options for vector search compression.
        /// </summary>
        private RescoringOptions BuildRescoringOptions(bool enableRescoring, int defaultOversampling, VectorSearchCompressionRescoreStorageMethod rescoreStorageMethod)
        {
            //return
            return new RescoringOptions()
            {
                //assemble object
                EnableRescoring = enableRescoring,
                DefaultOversampling = defaultOversampling,
                RescoreStorageMethod = rescoreStorageMethod
            };
        }

        /// <summary>
        /// Adds an open ai vectorizer to a search index.
        /// </summary>
        private void AddAzureOpenAIVectorizer(SearchIndex index, string name)
        {
            //return
            index.VectorSearch.Vectorizers.Add(new AzureOpenAIVectorizer(name)
            {
                //assemble object
                Parameters = new AzureOpenAIVectorizerParameters()
                {
                    //assemble object
                    ApiKey = this._foundrySettings.AccountKey,
                    ModelName = this._foundrySettings.EmbeddingModel,
                    ResourceUri = this._foundrySettings.OpenAIEndpoint,
                    DeploymentName = this._foundrySettings.EmbeddingModel.ToString(),
                }
            });
        }

        /// <summary>
        /// Adds an open ai vectorizer to a search index.
        /// </summary>
        private void AddAzureVisionVectorizer(SearchIndex index, string name)
        {
            //return
            index.VectorSearch.Vectorizers.Add(new AIServicesVisionVectorizer(name)
            {
                //assemble object
                AIServicesVisionParameters = new AIServicesVisionParameters(this._foundrySettings.VisionModelVersion, this._foundrySettings.DocumentIntelligenceEndpoint)
            });
        }

        /// <summary>
        /// Adds a non-vector field to the index.
        /// </summary>
        private void AddStandardField(SearchIndex index, string name, bool isKey, bool isFilterable, bool isSortable, bool isFacetable, bool? isSearchable, LexicalAnalyzerName? analyzerName, SearchFieldDataType dataType)
        {
            //initializtion
            SearchField field = new SearchField(name, dataType)
            {
                //assemble object
                IsKey = isKey,
                IsHidden = false,
                IsSortable = isSortable,
                IsFacetable = isFacetable,
                IsFilterable = isFilterable,
            };

            //configure field
            field.IsStored = true;
            field.AnalyzerName = analyzerName;
            field.IsSearchable = isSearchable;

            //return
            index.Fields.Add(field);
        }

        /// <summary>
        /// Adds a vector field to the index.
        /// </summary>
        private void AddVectorField(SearchIndex index, string name, int dimensions, string profileName)
        {
            //return
            index.Fields.Add(new SearchField(name, SearchFieldDataType.Collection(SearchFieldDataType.Single))
            {
                //assemble object
                IsHidden = false,
                IsSearchable = true,
                VectorSearchDimensions = dimensions,
                VectorSearchProfileName = profileName
            });
        }

        /// <summary>
        /// Creates a document extraction skill for images.
        /// </summary>
        private DocumentExtractionSkill CreateImageExtrationSkill()
        {
            //initialization
            DocumentExtractionSkill imageExtractionSkill = new DocumentExtractionSkill(
            [
                //assemble array
                new InputFieldMappingEntry(FSPKConstants.Search.Abstration.FileData)
                {
                    //assemble object
                    Source = FSPKConstants.Search.Abstration.Document.CombineURL(FSPKConstants.Search.Abstration.FileData)
                }
            ],
            [
                //assemble array
                new OutputFieldMappingEntry(FSPKConstants.Search.Abstration.Content)
                {
                    //assemble object
                    TargetName = FSPKConstants.Search.Abstration.ExtractedContent
                },
                new OutputFieldMappingEntry(FSPKConstants.Search.Abstration.NormalizedImages)
                {
                    //assemble object
                    TargetName = FSPKConstants.Search.Abstration.ExtractedImages
                }
            ])
            {
                //assemble object
                Context = FSPKConstants.Search.Abstration.Document,
                Name = FSPKConstants.Search.Skills.ExtractionSkill
            };

            //configure document extraction skill
            imageExtractionSkill.Configuration.Add(FSPKConstants.Search.ImageExtraction.NormalizedImageMaxWidth, FSPKConstants.Search.ImageExtraction.MaxSize);
            imageExtractionSkill.Configuration.Add(FSPKConstants.Search.ImageExtraction.NormalizedImageMaxHeight, FSPKConstants.Search.ImageExtraction.MaxSize);
            imageExtractionSkill.Configuration.Add(FSPKConstants.Search.ImageExtraction.ImageAction, FSPKConstants.Search.ImageExtraction.GenerateNormalizedImages);

            //return
            return imageExtractionSkill;
        }

        /// <summary>
        /// Creates a text split skill.
        /// </summary>
        private SplitSkill CreateSplitSkill(bool useSentences = true)
        {
            //initialization
            SplitSkill splitSkill = new SplitSkill(
            [
                //assemble array
                new InputFieldMappingEntry(FSPKConstants.Search.Abstration.Text)
                {
                    //assemble object
                    Source = FSPKConstants.Search.Abstration.Document.CombineURL(FSPKConstants.Search.Abstration.ExtractedContent)
                }
            ],
            [
                //assemble array
                new OutputFieldMappingEntry(FSPKConstants.Search.Abstration.TextItems)
                {
                    //assemble object
                    TargetName = FSPKConstants.Search.Abstration.Pages
                }
            ])
            {
                //assemble object
                DefaultLanguageCode = SplitSkillLanguage.En,
                Name = FSPKConstants.Search.Skills.SplitSkill,
                Context = FSPKConstants.Search.Abstration.Document,
                MaximumPageLength = FSPKConstants.Search.Chunking.MaximumPageLength,
                TextSplitMode = useSentences ? TextSplitMode.Sentences : TextSplitMode.Pages
            };

            //check pages
            if (!useSentences)
            {
                //configure pages
                splitSkill.Unit = SplitSkillUnit.Characters;
                splitSkill.PageOverlapLength = FSPKConstants.Search.Chunking.PageOverlapLength;
                splitSkill.MaximumPagesToTake = FSPKConstants.Search.Chunking.MaximumPagesToTake;
            }

            //return
            return splitSkill;
        }

        /// <summary>
        /// Creates an Azure OpenAI (via Foundry) embedding skill.
        /// </summary>
        private AzureOpenAIEmbeddingSkill CreateOpenAIEmbeddingSkill(string name, string context, string inputSource, string targetSource)
        {
            //return
            return new AzureOpenAIEmbeddingSkill(
            [
                //assemble array
                new InputFieldMappingEntry(FSPKConstants.Search.Abstration.Text)
                {
                    //assemble object
                    Source = inputSource
                }
            ],
            [
                //assemble array
                new OutputFieldMappingEntry(FSPKConstants.Search.Abstration.Embedding)
                {
                    //assemble object
                    TargetName = targetSource
                }
            ])
            {
                //assemble object
                Name = name,
                Context = context,
                ApiKey = this._foundrySettings.AccountKey,
                ModelName = this._foundrySettings.EmbeddingModel,
                ResourceUri = this._foundrySettings.OpenAIEndpoint,
                DeploymentName = this._foundrySettings.EmbeddingDeploymentName,
                Dimensions = FSPKConstants.Search.Vectorization.TextDimensions
            };
        }

        /// <summary>
        /// Creates a custom search skill.
        /// </summary>
        private WebApiSkill CreateWebAPISkill2(string name, string context, string sourceName, string sourceField, string targetName, string targetField, string url)
        {
            //return
            return new WebApiSkill(
            [
                //assemble array
                new InputFieldMappingEntry(sourceName)
                {
                    //assemble object
                    Source = FSPKConstants.Search.Abstration.Document.CombineURL(sourceField)
                }
            ],
            [
                //assemble array
                new OutputFieldMappingEntry(targetName)
                {
                    //assemble object
                    TargetName = targetField
                }
            ],
            url)
            {
                //assemble object
                Name = name,
                Context = context,
                HttpMethod = FSPKConstants.HTTP.Post,
                AuthResourceId = new ResourceIdentifier(this._entraIDSettings.Scope)
            };
        }

        /// <summary>
        /// Creates a custom search skill.
        /// </summary>
        private WebApiSkill CreateWebAPISkill(string name, string context, string sourceName, string sourceField, string targetName, string targetField, string url)
        {
            //return
            return new WebApiSkill(
            [
                //assemble array
                new InputFieldMappingEntry(sourceName)
                {
                    //assemble object
                    Source = sourceField
                }
            ],
            [
                //assemble array
                new OutputFieldMappingEntry(targetName)
                {
                    //assemble object
                    TargetName = targetField
                }
            ],
            url)
            {
                //assemble object
                Name = name,
                Context = context,
                HttpMethod = FSPKConstants.HTTP.Post,
                AuthResourceId = new ResourceIdentifier(this._entraIDSettings.Scope)
            };
        }

        /// <summary>
        /// Creates an image embedding skill.
        /// </summary>
        private VisionVectorizeSkill CreateAzureVisionEmbeddingSkill(string context)
        {
            //return
            return new VisionVectorizeSkill(
            [
                //assemble array
                new InputFieldMappingEntry(FSPKConstants.Search.Abstration.Image)
                {
                    //assemble object
                    Source = context
                }
            ],
            [
                //assemble array
                new OutputFieldMappingEntry(FSPKConstants.Search.Abstration.Vector)
                {
                    //assemble object
                    TargetName = FSPKConstants.Search.Abstration.ImageVector
                }
            ], this._foundrySettings.VisionModelVersion)
            {
                //assemble object
                Context = context,
                Name = FSPKConstants.Search.Skills.ImageEmbeddingSkill
            };
        }

        /// <summary>
        /// Creates an LLM chat completion skill for image verbalization.
        /// </summary>
        private ChatCompletionSkill CreateImageVerbalizationSkill(string context, string systemMessage, string userMessage)
        {
            //return
            return new ChatCompletionSkill(
            [
                //assemble array
                new InputFieldMappingEntry(FSPKConstants.Search.ChatCompletion.SystemMessage)
                {
                    //assemble object
                    Source = systemMessage
                },
                new InputFieldMappingEntry(FSPKConstants.Search.ChatCompletion.UserMessage)
                {
                    //assemble object
                    Source = userMessage
                },
                new InputFieldMappingEntry(FSPKConstants.Search.Abstration.Image)
                {
                    //assemble object
                    Source = context.CombineURL(FSPKConstants.Search.Abstration.Data)
                }
            ],
            [
                //assemble array
                new OutputFieldMappingEntry(FSPKConstants.Search.Abstration.Response)
                {
                    //assemble object
                    TargetName = FSPKConstants.Search.Abstration.VerbalizedImage
                }
            ], string.Format(FSPKConstants.Search.ChatCompletion.EndpointFormat, this._foundrySettings.DocumentIntelligenceEndpoint, this._foundrySettings.LLMModel, this._foundrySettings.ChatCompletionAPIVersion)
            )
            {
                //assemble object                    
                Context = context,
                HttpMethod = FSPKConstants.HTTP.Post,
                ApiKey = this._foundrySettings.AccountKey,
                Name = FSPKConstants.Search.Skills.ChatCompletion,
                Timeout = TimeSpan.FromMinutes(FSPKConstants.Search.ChatCompletion.TimeoutMinutes)
            };
        }

        /// <summary>
        /// Creates a shaper skill to reformat extracted images for storage.
        /// </summary>
        private ShaperSkill CreateImageExtractionShaperSkill(string context, Uri containerURL)
        {
            //return
            return new ShaperSkill(
            [
                //assemble array
                new InputFieldMappingEntry(FSPKConstants.Search.Abstration.ExtractedImages)
                {
                    //assemble object
                    Source = context
                },
                new InputFieldMappingEntry(FSPKConstants.Search.Abstration.ImagePath)
                {
                    //assemble object
                    Source = string.Format(FSPKConstants.Search.ImageExtraction.ImagePathFormat, containerURL.ToString().ToLowerInvariant().TrimEnd('/'), context.CombineURL(FSPKConstants.Search.Abstration.ImagePath))
                }                 
            ],
            [
                //assemble array
                new OutputFieldMappingEntry(FSPKConstants.Search.Abstration.Output)
                {
                    //assemble object
                    TargetName = FSPKConstants.Search.Abstration.NewImages
                }
            ])
            {
                //assemble object
                Context = context,
                Name = FSPKConstants.Search.Skills.ShaperSkill
            };
        }

        /// <summary>
        /// Creates an entity recognition skill to extract PII.
        /// </summary>
        private EntityRecognitionSkill CreateEntityRecognitionSkill(EntityRecognitionSkill.SkillVersion skillVersion, Dictionary<EntityCategory, string> categories)
        {
            //initialization
            List<OutputFieldMappingEntry> outputMappings = new List<OutputFieldMappingEntry>();

            //get all categories
            foreach (EntityCategory category in categories.Keys)
            {
                //build output mappings
                outputMappings.Add(new OutputFieldMappingEntry($"{category.ToString().ToLowerInvariant()}s")
                {
                    //assemble object
                    TargetName = categories[category]
                });
            }

            //create skill
            EntityRecognitionSkill entityRecognitionSkill = new EntityRecognitionSkill(
            [   
                //assemble array
                new InputFieldMappingEntry(FSPKConstants.Search.Abstration.Text)
                {
                    //assemble object
                    Source = FSPKConstants.Search.Abstration.Document.CombineURL(FSPKConstants.Search.Abstration.Content)
                },
            ],
            outputMappings, skillVersion)
            {
                //assemble object
                Context = FSPKConstants.Search.Abstration.Document,
                DefaultLanguageCode = EntityRecognitionSkillLanguage.En,
                Name = FSPKConstants.Search.Skills.EntityRecognitionSkill
            };

            //assign categories
            foreach (EntityCategory category in categories.Keys)
                entityRecognitionSkill.Categories.Add(category);

            //return
            return entityRecognitionSkill;
        }

        /// <summary>
        /// Creates a knowledge store projection for extracted image persistance.
        /// </summary>
        private KnowledgeStoreProjection CreateImageKnowledgeStoreProjection(string keyFieldName, string allExtractedImages, Dictionary<string, string> metadata)
        {
            //initialization
            KnowledgeStoreProjection knowledgeStoreProjection = new KnowledgeStoreProjection();
            KnowledgeStoreTableProjectionSelector metadataObjectSelector = new KnowledgeStoreTableProjectionSelector(FSPKConstants.AzureStorage.Tables.ExtractedImageMetadata)
            {
                //assemble object
                GeneratedKeyName = keyFieldName,
                SourceContext = allExtractedImages
            };

            //map metadata
            foreach (string property in metadata.Keys)
            {
                //configure each blob property
                metadataObjectSelector.Inputs.Add(new InputFieldMappingEntry(property)
                {
                    //assemble object
                    Source = metadata[property]
                });
            }

            //project images into azure blobs
            knowledgeStoreProjection.Files.Add(new KnowledgeStoreFileProjectionSelector(FSPKConstants.AzureStorage.Blobs.ImageContainer)
            {
                //assemble object
                Source = FSPKConstants.Search.Abstration.Document.CombineURL(FSPKConstants.Search.Abstration.NormalizedImages).CombineURL(FSPKConstants.Search.Abstration.Star)
            });

            //return
            knowledgeStoreProjection.Tables.Add(metadataObjectSelector);
            return knowledgeStoreProjection;
        }

        /// <summary>
        /// Determines id an Azure Search operation was successful.
        /// </summary>
        private async Task<bool> ProcessIndexResponseAsync(Response<IndexDocumentsResult> response, string message)
        {
            //initialization
            string error = await response.GetResponseErrorAsync(message);

            //check results
            if (response?.Value?.Results?.Any() ?? false)
            {
                //get errors
                string[] errors = response.Value.Results.Where(r => !r.Succeeded).Select(r => $"{r.Key}: {r.ErrorMessage}").ToArray();
                if (errors.Any())
                {
                    //error
                    this._logger.LogError($"Unable to {message}: {string.Join(", ", errors)}");
                    return false;
                }
                else
                {
                    //success
                    return true;
                }
            }
            else if (string.IsNullOrWhiteSpace(error))
            {
                //return
                return true;
            }
            else
            {
                //general error
                this._logger.LogError(error);
                return false;
            }
        }

        /// <summary>
        /// Pauses 30 seconds to wait for an admin operation to complete.
        /// </summary>
        private async Task WaitForAdminOperationAsync(int delaySeconds = 5)
        {
            //return
            await Task.Delay(delaySeconds * 1000);
        }

        /// <summary>
        /// Normalizes a URL for consistent storage and retrieval.
        /// </summary>
        private string NormalizeURL(string url)
        {
            //return
            return url.Trim().ToLowerInvariant();
        }
        #endregion
    }
}