using System;

namespace FoundrySharePointKnowledge.Common
{
    /// <summary>
    /// These are the system-wide well-known values.
    /// </summary>
    public static class FSPKConstants
    {
        public static class Security
        {
            public const string JWT = nameof(JWT);
            public const string Audience = "api://";
            public const int TokenExpirationMinutes = 5;
            public const string Issuer = "https://sts.windows.net/";
            public const string Authorization = nameof(Authorization);
            public const string KeyDiscoveryEndpoint = "/discovery/keys";
            public const string Instance = "https://login.microsoftonline.com/";
            public const string GraphScope = "https://graph.microsoft.com/.default";
            public const string TokenLinkFormat = "Get a token by running [this]({0}) flow and pasting the result from the output body of the last activity.";

            public static class RSA
            {
                public const string Kid = "kid";
                public const string Keys = "keys";
                public const string Modulus = "n";
                public const string Exponent = "e";
            }
        }

        public static class JsonPropertyNames
        {
            public const string Data = "data";
            public const string Model = "model";
            public const string Usage = "usage";
            public const string Index = "index";
            public const string Object = "object";
            public const string Embedding = "embedding";
            public const string TotalTokens = "total_tokens";
            public const string PromptTokens = "prompt_tokens";
        }

        public static class Settings
        {
            public const string KeyVaultURL = nameof(KeyVaultURL);
            public const string TokenFlowURL = nameof(TokenFlowURL);
            public const string EmbeddingAPIVersion = nameof(EmbeddingAPIVersion);
            public const string DocumentIntelligenceAPIVersion = nameof(DocumentIntelligenceAPIVersion);

            public static class KeyVault
            {
                public static class Search
                {
                    public const string URL = "search-api-url";
                    public const string Key = "search-admin-key";
                    public const string StorageAccountResourceId = "storage-account-resource-id";
                }

                public static class Foundry
                {
                    public const string AccountKey = "foundry-account-key";
                    public const string EmbeddingModel = "embedding-model";
                    public const string OpenAIEndpoint = "foundry-open-ai-endpoint";
                    public const string DocumentIntelligenceEndpoint = "foundry-document-intelligence-endpoint";
                }

                public static class EntraID
                {
                    public const string TenantId = "tenant-id";
                    public const string ClientId = "auth-client-id";
                    public const string ClientSecret = "auth-client-secret";
                }

                public static class ApplicationInsights
                {
                    public const string ConnectionString = "app-insights-connection-string";
                    public const string InstrumentationKey = "app-insights-instramentation-key";
                }

                public static class BlobStorage
                {
                    public const string Name = "storage-account-name";
                    public const string ConnectionString = "storage-account-connection-string";
                }
            }
        }

        public static class Search
        {
            public const string Space = "%20";
            public const string Equal = " eq ";
            public static class Indexes
            {
                public const string Foundry = "sharepoint-foundry";
                public const string Vectorized = "sharepoint-foundry-vectorized";
            }

            public static class Profiles
            {
                public const string Compression = "vector-profile-cosine-scalar";
                public const string Vectorizable = "vector-profile-cosine-vectorizable";
            }

            public static class Vectorization
            {
                public const int Dimensions = 1536;                
                public const string Name = "sharepoint-foundry-vectorizer";                
            }

            public static class Compression
            {
                public const int DefaultOversampling = 10;
                public const string Scalar = "scalar-quantization";
                public const string Binary = "binary-quantization";
            }

            public static class Algorithms
            {
                public const int M = 4;
                public const string ExhaustiveKnn = "exhaustive-knn";

                public static class Cosine
                {
                    public const int EFSearch = 500;
                    public const string Name = "cosine";
                    public const int EFConstruction = 400;
                }

                public static class Hamming
                {
                    public const int EFSearch = 800;
                    public const string Name = "hamming";
                    public const int EFConstruction = 800;
                }
            }

            public static class Semantics
            {
                public const string Name = "semantic-search";
            }

            public static class Datasource
            {
                public const string Container = "sharepoint-ingestion";
                public const string Name = "sharepoint-foundry-datasource";
            }

            public static class Indexer
            {
                public const string Name = "sharepoint-foundry-indexer";
            }

            public static class Skillset
            {
                public const string AISkill = "ai-skill";
                public const string SplitSkill = "split-skill";
                public const string Name = "sharepoint-foundry-skillset";
            }

            public static class Chucking
            {
                public const string Star = "*";
                public const string Text = "text";
                public const string Pages = "pages";
                public const string Content = "content";
                public const int MaximumPagesToTake = 0;
                public const int PageOverlapLength = 500;
                public const int MaximumPageLength = 2000;
                public const string Document = "/document";
                public const string Embedding = "embedding";
                public const string TextItems = "textItems";              
            }

            public static class Fields
            {
                public const string Timestamp = nameof(Timestamp);
                public const string MetadataStoragePath = "metadata_storage_path";
                public const string MetadataStorageLastModified = "metadata_storage_last_modified";
            }
        }

        public static class BlobStorage
        {
            public const string Container = "sharepoint-ingestion";

            public static class RetryPolicy
            {
                public const int Attempts = 5;
                public static readonly TimeSpan Backoff = TimeSpan.FromSeconds(30);
            }
        }

        public static class Foundry
        {
            public const string Client = nameof(Foundry);
            public const string ModelId = "prebuilt-layout";
        }

        public static class ContentTypes
        {
            public const string CSV = "text/csv";
            public const string PDF = "application/pdf";
            public const string PlainText = "text/plain";
            public const string JSON = "application/json";
            public const string OctetStream = "application/octet-stream";
            public const string Excel = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            public const string Word = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
            public const string PowerPoint = "application/vnd.openxmlformats-officedocument.presentationml.presentation";
        }

        public static class Extensions
        {
            public const string PDF = ".pdf";
            public const string DOC = ".doc";
            public const string XLS = ".xls";
            public const string TXT = ".txt";
            public const string PPT = ".ppt";
            public const string CSV = ".csv";
            public const string XLSX = ".xlsx";
            public const string DOCX = ".docx";
            public const string PPTX = ".pptx";
            public const string JSON = ".json";
        }

        public static class Routing
        {
            public const string Controller = "[controller]";
            public static class API
            {
                public const string Delete = "delete";
                public const string Upload = "upload";
                public const string Ingest = "ingest";
                public const string DeployFoundry = "deploy-foundry";
                public const string DeployVectorized = "deploy-vectorized";
            }
            public static class Foundry
            {
                public const string Embedddings = "/embeddings?api-version=";
                public const string OpenAIDeployments = "/openai/deployments/";
            }
        }
    }
}
