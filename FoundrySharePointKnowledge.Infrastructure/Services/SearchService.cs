using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

using Azure;
using Azure.Storage.Blobs;
using Azure.Search.Documents;
using Azure.Storage.Blobs.Models;
using Azure.Search.Documents.Models;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;

using FoundrySharePointKnowledge.Common;
using FoundrySharePointKnowledge.Domain.Search;
using FoundrySharePointKnowledge.Domain.Settings;
using FoundrySharePointKnowledge.Domain.Contracts;
using FoundrySharePointKnowledge.Domain.SharePoint;

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
        private readonly IFoundryService _foundryService;
        private readonly AzureSearchSettings _searchSettings;
        private readonly SearchIndexClient _searchIndexClient;
        private readonly AzureFoundrySettings _foundrySettings;
        private readonly SearchIndexerClient _searchIndexerClient;
        private readonly Dictionary<string, SearchClient> _searchClients;
        #endregion
        #region Initialization
        public SearchService(BlobServiceClient blobClient,
                             ILogger<SearchService> logger,
                             IFoundryService foundryService,
                             AzureSearchSettings searchSettings,
                             SearchIndexClient searchIndexClient,
                             AzureFoundrySettings foundrySettings,
                             SearchIndexerClient searchIndexerClient,
                             Dictionary<string, SearchClient> searchClients)
        {
            //initialization
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this._blobClient = blobClient ?? throw new ArgumentNullException(nameof(blobClient));
            this._searchClients = searchClients ?? throw new ArgumentNullException(nameof(searchClients));
            this._foundryService = foundryService ?? throw new ArgumentNullException(nameof(foundryService));
            this._searchSettings = searchSettings ?? throw new ArgumentNullException(nameof(searchSettings));
            this._foundrySettings = foundrySettings ?? throw new ArgumentNullException(nameof(foundrySettings));
            this._searchIndexClient = searchIndexClient ?? throw new ArgumentNullException(nameof(searchIndexClient));
            this._searchIndexerClient = searchIndexerClient ?? throw new ArgumentNullException(nameof(searchIndexerClient));
        }
        #endregion
        #region Public Methods
        /// <summary>
        /// Vectorizes and indexes a SharePoint file.
        /// </summary>
        public async Task<bool> InjestFileAsync(SPFile file)
        {
            try
            {
                //initialization
                if (string.IsNullOrWhiteSpace(file?.URL))
                {
                    //error
                    this._logger.LogError("Cannot index a null file.");
                    return false;
                }

                //chunk file
                this._logger.LogInformation($"Acquiring file {file}.");
                SPFileChunk[] fileChunks = await this._foundryService.ChunkFileAsync(file);
                IndexDocumentsAction<SPFileChunk>[] indexedDocuments = new IndexDocumentsAction<SPFileChunk>[fileChunks.Length];

                //convert chunks to azure search documents
                for (int d = 0; d < fileChunks.Length; d++)
                {
                    //batch each chunk
                    SPFileChunk chunkedDocument = fileChunks[d];
                    indexedDocuments[d] = IndexDocumentsAction.Upload(chunkedDocument);
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
                if (string.IsNullOrWhiteSpace(file?.URL))
                {
                    //error
                    this._logger.LogError("Cannot upload a null file.");
                    return false;
                }

                //download file from sharepoint
                this._logger.LogInformation($"Acquiring file {file}.");
                byte[] fileContents = await this._foundryService.GetFileContentsAsync(file);
                if (!fileContents?.Any() ?? true)
                {
                    //error
                    this._logger.LogError($"File {file} has no content.");
                    return false;
                }

                //upload file to blob storage
                this._logger.LogInformation($"Uploading file {file} to azure storage blob container {FSPKConstants.BlobStorage.Container}.");
                BlobContainerClient container = this._blobClient.GetBlobContainerClient(FSPKConstants.BlobStorage.Container);
                BlobClient blob = container.GetBlobClient(file.Name);

                //build blob metadata
                Dictionary<string, string> metadata = new Dictionary<string, string>()
                {
                    //assemble dictionary
                    { nameof(file.Title), file.Title },
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
                string blobError = await this.GetResponseErrorAsync<BlobContentInfo>(blobResult, $"upsert blob {file}");
                if (!string.IsNullOrWhiteSpace(blobError))
                    throw new Exception($"Failed to upsert blob {file}: {blobError}");

                //return
                this._logger.LogInformation($"Successfully uploaded file {file} to azure storage blob container {FSPKConstants.BlobStorage.Container}.");
                return true;
            }
            catch (Exception ex)
            {
                //error
                this._logger.LogCritical(ex, $"Unable to upload file {file?.URL ?? "N/A"} to azure storage blob container {FSPKConstants.BlobStorage.Container}.");
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

                //get container
                this._logger.LogInformation($"Deleting file {file} from azure storage blob container {FSPKConstants.BlobStorage.Container}.");
                string errorMessage = $"Failed to delete search documents for {file} from index {FSPKConstants.Search.Indexes.Foundry}: ";
                BlobContainerClient container = this._blobClient.GetBlobContainerClient(FSPKConstants.BlobStorage.Container);
                BlobClient blob = container.GetBlobClient(file.Name);
                await container.CreateIfNotExistsAsync();

                //delete blob
                Response<bool> deleteBlobResult = await blob.DeleteIfExistsAsync();
                string deleteBlobError = await this.GetResponseErrorAsync<bool>(deleteBlobResult, $"delete blob {file}");
                if (!string.IsNullOrWhiteSpace(deleteBlobError))
                    throw new Exception($"{errorMessage}blob delete: {deleteBlobError}");

                //check result
                if (deleteBlobResult.Value)
                {
                    //create an exact match non-vectorized keyword query against the normalized url
                    string keyField = nameof(VectorizedChunk.ChunkId);
                    SearchOptions searchOptions = new SearchOptions()
                    {
                        //assemble object
                        VectorSearch = null,
                        SearchMode = SearchMode.All,
                        QueryType = SearchQueryType.Full,
                        Filter = $"{nameof(SPFile.URL)}{FSPKConstants.Search.Equal}'{this.NormalizeURL(file.URL).Replace(" ", FSPKConstants.Search.Space)}'"
                    };

                    //run query for documents keys with the given URL
                    searchOptions.Select.Add(keyField);
                    Response<SearchResults<VectorizedChunk>> searchResult = await this._searchClients[FSPKConstants.Search.Indexes.Foundry].SearchAsync<VectorizedChunk>(null, searchOptions);

                    //check result
                    string searchError = await this.GetResponseErrorAsync<SearchResults<VectorizedChunk>>(searchResult, $"search for file {file} in index {FSPKConstants.Search.Indexes.Foundry}");
                    if (!string.IsNullOrWhiteSpace(searchError))
                        throw new Exception($"{errorMessage}search: {searchError}");

                    //get document keys
                    List<string> documentsToDelete = new List<string>();
                    await foreach (SearchResult<VectorizedChunk> result in searchResult.Value.GetResultsAsync())
                        documentsToDelete.Add(result.Document.ChunkId);

                    //check documents (the search API will throw if the collection is empty)
                    if (!documentsToDelete.Any())
                        throw new Exception($"{errorMessage}search results: file not found");

                    //delete documents
                    Response<IndexDocumentsResult> deleteDocumentsResult = await this._searchClients[FSPKConstants.Search.Indexes.Foundry].DeleteDocumentsAsync(keyField, documentsToDelete);
                    string deleteDocumentsError = await this.GetResponseErrorAsync<IndexDocumentsResult>(deleteDocumentsResult, $"delete documents for {file} from index {FSPKConstants.Search.Indexes.Foundry}");
                    if (!string.IsNullOrWhiteSpace(deleteDocumentsError))
                        throw new Exception($"{errorMessage}index delete: {deleteDocumentsError}");

                    //return
                    this._logger.LogInformation($"Successfully deleted indexed documents for {file}.");
                    return true;
                }
                else
                {
                    //error
                    this._logger.LogWarning($"File {file} was not found in azure storage blob container {FSPKConstants.BlobStorage.Container}.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                //error
                this._logger.LogCritical(ex, $"Unable to delete file {file?.ToString() ?? "N/A"} from azure storage blob container {FSPKConstants.BlobStorage.Container} and search index {FSPKConstants.Search.Indexes.Foundry}.");
                return false;
            }
        }

        /// <summary>
        /// Creates a search index that expects "pre-vectorized" content. If there is an existing index with the same name, it will be deleted first.
        /// </summary>
        public async Task<string> EnsureVectorizedIndexAsync(string indexName)
        {
            try
            {
                //initialization
                SPFileChunk dummyFileChunk = new SPFileChunk();
                SearchIndex index = await this.EnsureIndexAsync(indexName);                               

                //configure vector search compression
                this.AddSearchIndexBinaryCompression(index, FSPKConstants.Search.Compression.Binary, true, FSPKConstants.Search.Compression.DefaultOversampling, VectorSearchCompressionRescoreStorageMethod.DiscardOriginals);
                this.AddSearchIndexScalarCompression(index, FSPKConstants.Search.Compression.Scalar, true, FSPKConstants.Search.Compression.DefaultOversampling, VectorSearchCompressionRescoreStorageMethod.PreserveOriginals);
                
                //configure vector search algorithms
                this.AddSearchIndexVectorHNSWAlgorithm(index, FSPKConstants.Search.Algorithms.Cosine.Name, VectorSearchAlgorithmMetric.Cosine, FSPKConstants.Search.Algorithms.M, FSPKConstants.Search.Algorithms.Cosine.EFSearch, FSPKConstants.Search.Algorithms.Cosine.EFConstruction);
                this.AddSearchIndexVectorHNSWAlgorithm(index, FSPKConstants.Search.Algorithms.Hamming.Name, VectorSearchAlgorithmMetric.Hamming, FSPKConstants.Search.Algorithms.M, FSPKConstants.Search.Algorithms.Hamming.EFSearch, FSPKConstants.Search.Algorithms.Hamming.EFConstruction);
                this.AddSearchIndexVectorExhaustiveKnnAlgorithm(index, FSPKConstants.Search.Algorithms.ExhaustiveKnn, VectorSearchAlgorithmMetric.Euclidean);

                //configure vector search profile
                this.AddSearchIndexVectorProfile(index, FSPKConstants.Search.Profiles.Compression, FSPKConstants.Search.Algorithms.Cosine.Name, FSPKConstants.Search.Compression.Scalar, null);              

                //add standard fields
                this.AddStandardField(index, nameof(dummyFileChunk.Id), true, true, true, false, null, null, SearchFieldDataType.String);
                this.AddStandardField(index, nameof(dummyFileChunk.URL), false, false, true, false, true, null, SearchFieldDataType.String);
                this.AddStandardField(index, nameof(dummyFileChunk.Name), false, false, true, false, true, null, SearchFieldDataType.String);
                this.AddStandardField(index, nameof(dummyFileChunk.ItemId), false, true, true, true, true, null, SearchFieldDataType.String);
                this.AddStandardField(index, nameof(dummyFileChunk.Title), false, false, true, false, true, null, SearchFieldDataType.String);
                this.AddStandardField(index, nameof(dummyFileChunk.DriveId), false, true, true, true, true, null, SearchFieldDataType.String);
                this.AddStandardField(index, nameof(dummyFileChunk.PageNumber), false, true, true, true, false, null, SearchFieldDataType.Int32);
                this.AddStandardField(index, nameof(dummyFileChunk.SecurityData), false, false, false, false, true, null, SearchFieldDataType.String);
                this.AddStandardField(index, nameof(dummyFileChunk.Content), false, false, false, false, true, LexicalAnalyzerName.EnMicrosoft, SearchFieldDataType.String);

                //add vector fields
                this.AddVectorField(index, nameof(dummyFileChunk.TitleVector), true, FSPKConstants.Search.Vectorization.Dimensions, FSPKConstants.Search.Profiles.Compression);
                this.AddVectorField(index, nameof(dummyFileChunk.ContentVector), false, FSPKConstants.Search.Vectorization.Dimensions, FSPKConstants.Search.Profiles.Compression);

                //return
                await this._searchIndexClient.CreateIndexAsync(index);
                return string.Empty;
            }
            catch (Exception ex)
            {
                //error
                this._logger.LogCritical(ex, $"Unable to deploy search index {indexName}.");
                return ex.Message;
            }
        }

        /// <summary>
        /// Creates a search index with a vectorizer connected to an azure storage account. If there is an existing index with the same name, it will be deleted first.
        /// </summary>
        public async Task<string> EnsureVectorizableBlobIndexAsync(string indexName)
        {
            try
            {
                //initialization
                SearchIndexer indexer = null;
                SearchIndexerSkillset skillset = null;
                SearchIndexerDataSourceConnection dataSourceConnection = null;                
                string allDocumentPages = FSPKConstants.Search.Chucking.Document.CombineURL(FSPKConstants.Search.Chucking.Pages).CombineURL(FSPKConstants.Search.Chucking.Star);

                //get field names
                VectorizedChunk dummyVectorizedChunk = new VectorizedChunk();
                string textVector = nameof(dummyVectorizedChunk.TextVector);
                string parentId = nameof(dummyVectorizedChunk.ParentId);
                string chunkId = nameof(dummyVectorizedChunk.ChunkId);
                string title = nameof(dummyVectorizedChunk.Title);
                string chunk = nameof(dummyVectorizedChunk.Chunk);
                string url = nameof(dummyVectorizedChunk.URL);

                //ensure index
                SearchIndex index = await this.EnsureIndexAsync(indexName);

                //configure semantic fields
                SemanticPrioritizedFields semanticFields = new SemanticPrioritizedFields();
                semanticFields.ContentFields.Add(new SemanticField(chunk));
                semanticFields.TitleField = new SemanticField(title);

                //configure index semantics
                index.Similarity = new BM25Similarity();
                index.SemanticSearch = new SemanticSearch();
                index.SemanticSearch.DefaultConfigurationName = FSPKConstants.Search.Semantics.Name;
                index.SemanticSearch.Configurations.Add(new SemanticConfiguration(FSPKConstants.Search.Semantics.Name, semanticFields));

                //configure vector search algorithms
                this.AddSearchIndexVectorHNSWAlgorithm(index, FSPKConstants.Search.Algorithms.Cosine.Name, VectorSearchAlgorithmMetric.Cosine, FSPKConstants.Search.Algorithms.M, FSPKConstants.Search.Algorithms.Cosine.EFSearch, FSPKConstants.Search.Algorithms.Cosine.EFConstruction);

                //configure vector search vectorizer
                this.AddSearchIndexVectorizer(index, FSPKConstants.Search.Vectorization.Name);

                //configure vector search profile
                this.AddSearchIndexVectorProfile(index, FSPKConstants.Search.Profiles.Vectorizable, FSPKConstants.Search.Algorithms.Cosine.Name, null, FSPKConstants.Search.Vectorization.Name);

                //add search fields
                this.AddStandardField(index, title, false, false, false, false, true, null, SearchFieldDataType.String);
                this.AddStandardField(index, chunk, false, false, false, false, true, null, SearchFieldDataType.String);
                this.AddStandardField(index, parentId, false, true, false, false, false, null, SearchFieldDataType.String);
                this.AddStandardField(index, url, false, true, false, false, true, LexicalAnalyzerName.Keyword, SearchFieldDataType.String);
                this.AddStandardField(index, chunkId, true, false, true, false, true, LexicalAnalyzerName.Keyword, SearchFieldDataType.String);
                this.AddVectorField(index, textVector, true, FSPKConstants.Search.Vectorization.Dimensions, FSPKConstants.Search.Profiles.Vectorizable);
                this.AddStandardField(index, FSPKConstants.Search.Fields.Timestamp, false, false, false, false, false, null, SearchFieldDataType.DateTimeOffset);

                //create index
                Response<SearchIndex> indexResult = await this._searchIndexClient.CreateIndexAsync(index);
                string indexError = await this.GetResponseErrorAsync<SearchIndex>(indexResult, $"create index {indexName}");
                if (!string.IsNullOrWhiteSpace(indexError))
                    throw new Exception($"Failed to create index {indexName}: {indexError}");

                try
                {
                    //check indexer
                    indexer = await this._searchIndexerClient.GetIndexerAsync(FSPKConstants.Search.Indexer.Name);
                    if (indexer != null)
                    {
                        //delete indexer
                        this._logger.LogWarning($"Deleting existing azure search indexer {FSPKConstants.Search.Indexer.Name}.");
                        await this._searchIndexerClient.DeleteIndexerAsync(FSPKConstants.Search.Indexer.Name);
                        await this.WaitForAdminOperationAsync();
                    }
                }
                catch { /* eat - the above method bombs if not found */ }

                try
                {
                    //check datasource
                    dataSourceConnection = await this._searchIndexerClient.GetDataSourceConnectionAsync(FSPKConstants.Search.Datasource.Name);
                    if (dataSourceConnection != null)
                    {
                        //delete datasource
                        this._logger.LogWarning($"Deleting existing azure search datasource {FSPKConstants.Search.Datasource.Name}.");
                        await this._searchIndexerClient.DeleteDataSourceConnectionAsync(FSPKConstants.Search.Datasource.Name);
                        await this.WaitForAdminOperationAsync();
                    }
                }
                catch { /* eat - the above method bombs if not found */ }

                //configure data source
                dataSourceConnection = new SearchIndexerDataSourceConnection(FSPKConstants.Search.Datasource.Name,
                                                                             SearchIndexerDataSourceType.AzureBlob,
                                                                             this._searchSettings.AzureStorageResourceId,
                                                                             new SearchIndexerDataContainer(FSPKConstants.Search.Datasource.Container));

                //configure data change detection policy
                dataSourceConnection.DataChangeDetectionPolicy = new HighWaterMarkChangeDetectionPolicy(FSPKConstants.Search.Fields.MetadataStorageLastModified);

                //create data source
                Response<SearchIndexerDataSourceConnection> datasourceResult = await this._searchIndexerClient.CreateDataSourceConnectionAsync(dataSourceConnection);
                string datasourceError = await this.GetResponseErrorAsync<SearchIndexerDataSourceConnection>(datasourceResult, $"create data source {FSPKConstants.Search.Datasource.Name}");
                if (!string.IsNullOrWhiteSpace(datasourceError))
                    throw new Exception($"Failed to create data source {FSPKConstants.Search.Datasource.Name}: {datasourceError}");

                try
                {
                    //check skillset
                    skillset = await this._searchIndexerClient.GetSkillsetAsync(FSPKConstants.Search.Skillset.Name);
                    if (skillset != null)
                    {
                        //delete skillset
                        this._logger.LogWarning($"Deleting existing azure search skillset {FSPKConstants.Search.Skillset.Name}.");
                        await this._searchIndexerClient.DeleteSkillsetAsync(FSPKConstants.Search.Skillset.Name);
                        await this.WaitForAdminOperationAsync();
                    }
                }
                catch { /* eat - the above method bombs if not found */ }

                //create split skill
                SplitSkill splitSkill = new SplitSkill([new InputFieldMappingEntry(FSPKConstants.Search.Chucking.Text)
                {
                    //assemble object
                    Source = FSPKConstants.Search.Chucking.Document.CombineURL(FSPKConstants.Search.Chucking.Content)
                }], [new OutputFieldMappingEntry(FSPKConstants.Search.Chucking.TextItems)
                {
                    //assemble object
                    TargetName = FSPKConstants.Search.Chucking.Pages
                }])
                {
                    //assemble object
                    TextSplitMode = TextSplitMode.Pages,
                    DefaultLanguageCode = SplitSkillLanguage.En,
                    Name = FSPKConstants.Search.Skillset.SplitSkill,
                    Context = FSPKConstants.Search.Chucking.Document,
                    MaximumPageLength = FSPKConstants.Search.Chucking.MaximumPageLength,
                    PageOverlapLength = FSPKConstants.Search.Chucking.PageOverlapLength,
                    MaximumPagesToTake = FSPKConstants.Search.Chucking.MaximumPagesToTake
                };

                //create ai skill
                AzureOpenAIEmbeddingSkill aiSkill = new AzureOpenAIEmbeddingSkill([new InputFieldMappingEntry(FSPKConstants.Search.Chucking.Text)
                {
                    //assemble object
                    Source = allDocumentPages
                }], [new OutputFieldMappingEntry(FSPKConstants.Search.Chucking.Embedding)
                {
                    //assemble object
                    TargetName = textVector
                }])
                {
                    //assemble object
                    Context = allDocumentPages,
                    ApiKey = this._foundrySettings.AccountKey,
                    Name = FSPKConstants.Search.Skillset.AISkill,
                    DeploymentName = this._foundrySettings.EmbeddingModel,
                    ResourceUri = new Uri(this._foundrySettings.OpenAIEndpoint),
                    Dimensions = FSPKConstants.Search.Vectorization.Dimensions,
                    ModelName = new AzureOpenAIModelName(this._foundrySettings.EmbeddingModel)
                };

                //configure skillset
                skillset = new SearchIndexerSkillset(FSPKConstants.Search.Skillset.Name, new SearchIndexerSkill[] { splitSkill, aiSkill })
                {
                    //assemble object
                    IndexProjection = new SearchIndexerIndexProjection(
                    [
                        //assemble array
                        new SearchIndexerIndexProjectionSelector(indexName, parentId, allDocumentPages, new InputFieldMappingEntry[]
                        {
                            //assemble object                           
                            new InputFieldMappingEntry(chunk)
                            {
                                //assemble object
                                Source = allDocumentPages
                            },
                            new InputFieldMappingEntry(textVector)
                            {
                                //assemble object
                                Source = allDocumentPages.CombineURL(textVector)
                            },
                            new InputFieldMappingEntry(url)
                            {
                                //assemble object
                                Source = FSPKConstants.Search.Chucking.Document.CombineURL(url)
                            },
                             new InputFieldMappingEntry(title)
                            {
                                //assemble object
                                Source = FSPKConstants.Search.Chucking.Document.CombineURL(title)
                            },
                            new InputFieldMappingEntry(FSPKConstants.Search.Fields.Timestamp)
                            {
                                //assemble object
                                Source = FSPKConstants.Search.Chucking.Document.CombineURL(FSPKConstants.Search.Fields.Timestamp)
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
                    }
                };

                //create skillset
                Response<SearchIndexerSkillset> skillsetResult = await this._searchIndexerClient.CreateSkillsetAsync(skillset);
                string skillsetError = await this.GetResponseErrorAsync<SearchIndexerSkillset>(skillsetResult, $"create skillset {FSPKConstants.Search.Skillset.Name}");
                if (!string.IsNullOrWhiteSpace(skillsetError))
                    throw new Exception($"Failed to create skillset {FSPKConstants.Search.Skillset.Name}: {skillsetError}");

                //configure indexer
                indexer = new SearchIndexer(indexName, dataSourceConnection.Name, indexName)
                {
                    //assemble object
                    SkillsetName = skillset.Name,
                    Schedule = new IndexingSchedule(TimeSpan.FromMinutes(FSPKConstants.Security.TokenExpirationMinutes))
                };

                //configure indexer field mappings
                indexer.FieldMappings.Add(new FieldMapping(FSPKConstants.Search.Fields.MetadataStoragePath)
                {
                    //assemble object
                    TargetFieldName = title
                });
                indexer.FieldMappings.Add(new FieldMapping(FSPKConstants.Search.Fields.MetadataStorageLastModified)
                {
                    //assemble object
                    TargetFieldName = FSPKConstants.Search.Fields.Timestamp
                });

                //configure indexer parameters
                indexer.Parameters = new IndexingParameters()
                {
                    //assemble object
                    IndexingParametersConfiguration = new IndexingParametersConfiguration()
                    {
                        //assemble object
                        ParsingMode = BlobIndexerParsingMode.Default,
                    }
                };

                //create indexer
                Response<SearchIndexer> indexerResult = await this._searchIndexerClient.CreateIndexerAsync(indexer);
                string indexerError = await this.GetResponseErrorAsync<SearchIndexer>(indexerResult, $"create indexer {FSPKConstants.Search.Indexer.Name}");
                if (!string.IsNullOrWhiteSpace(indexerError))
                    throw new Exception($"Failed to create indexer {FSPKConstants.Search.Indexer.Name}: {indexerError}");

                //return
                return string.Empty;
            }
            catch (Exception ex)
            {
                //error
                this._logger.LogCritical(ex, $"Unable to deploy search index {indexName}.");
                return ex.Message;
            }
        }
        #endregion
        #region Private Methods
        /// <summary>
        /// Deletes an existing index by name if found.
        /// </summary>
        private async Task<SearchIndex> EnsureIndexAsync(string indexName)
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

            //return
            return index;
        }

        /// <summary>
        /// Gets an Azure Search index by name.
        /// </summary>
        private async Task<SearchIndex> GetIndexByNameAsync(string indexName)
        {
            try
            {
                //initialization
                Response<SearchIndex> index = await this._searchIndexClient.GetIndexAsync(indexName);
                string error = await this.GetResponseErrorAsync(index, $"Get index {indexName}");

                //return
                if (string.IsNullOrWhiteSpace(error))
                    return index.Value;
                else
                    throw new Exception(error);
            }
            catch (Exception ex)
            {
                //error
                this._logger.LogCritical(ex, $"Search index {indexName} was not found.");
                return null;
            }
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
        /// Adds a vector search vectorizer to a search index.
        /// </summary>
        private void AddSearchIndexVectorizer(SearchIndex index, string name)
        {
            //return
            index.VectorSearch.Vectorizers.Add(new AzureOpenAIVectorizer(name)
            {
                //assemble object
                Parameters = new AzureOpenAIVectorizerParameters()
                {
                    //assemble object
                    ApiKey = this._foundrySettings.AccountKey,
                    DeploymentName = this._foundrySettings.EmbeddingModel,
                    ResourceUri = new Uri(this._foundrySettings.OpenAIEndpoint),
                    ModelName = new AzureOpenAIModelName(this._foundrySettings.EmbeddingModel)
                }
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
        private void AddVectorField(SearchIndex index, string name, bool isStored, int dimensions, string profileName)
        {
            //return
            index.Fields.Add(new SearchField(name, SearchFieldDataType.Collection(SearchFieldDataType.Single))
            {
                //assemble object
                IsHidden = false,
                IsStored = isStored,
                IsSearchable = true,
                VectorSearchDimensions = dimensions,
                VectorSearchProfileName = profileName
            });
        }

        /// <summary>
        /// Determines id an Azure Search operation was successful.
        /// </summary>
        private async Task<bool> ProcessIndexResponseAsync(Response<IndexDocumentsResult> response, string message)
        {
            //initialization
            string error = await this.GetResponseErrorAsync(response, message);

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
        /// Parses an error string from an Azure response.
        /// </summary>
        private async Task<string> GetResponseErrorAsync<T>(Response<T> response, string message)
        {
            //initialization
            string error = string.Empty;

            //check response
            if (response == null)
            {
                //no response
                error = "No response was received.";
            }
            else
            {
                //get raw response
                Response rawResponse = response.GetRawResponse();
                if (rawResponse == null)
                {
                    //no metadata
                    error = "No response metadata was received.";
                }
                else if (rawResponse.IsError)
                {
                    //return error contenxt
                    using StreamReader reader = new StreamReader(rawResponse.ContentStream);
                    error = await reader.ReadToEndAsync();
                }
            }

            //return
            return error;
        }

        /// <summary>
        /// Pauses 30 seconds to wait for an admin operation to complete against the Azure AI Search SDK.
        /// </summary>
        private async Task WaitForAdminOperationAsync()
        {
            //return
            await Task.Delay(30 * 1000);
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
