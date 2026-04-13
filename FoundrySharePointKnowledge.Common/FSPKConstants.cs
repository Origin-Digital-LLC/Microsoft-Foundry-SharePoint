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
            public const double TokenExpirationMinutes = 60;
            public const string AccessToken = "access_token";
            public const string Authorization = nameof(Authorization);
            public const string DefaultCorsPolicy = nameof(DefaultCorsPolicy);
            public const string TokenLinkFormat = "Get a token by running [this]({0}) flow and pasting the result from your Teams chat.";

            public static class TokenValidation
            {
                public const string Version = "ver";
                public const string Audience = "aud";
                public const string APIAudience = "api://";
                public const string Scope = "access_as_user";
                public const string Issuer = "https://sts.windows.net/";
                public const string Instance = "https://login.microsoftonline.com/";

                public static class Endpoints
                {
                    public const string Token = "/oauth2/v2.0/token";
                    public const string Authorize = "/oauth2/v2.0/authorize";
                    public const string Configuration = "/.well-known/openid-configuration";
                }
            }

            public static class RSA
            {
                public const string Kid = "kid";
                public const string Keys = "keys";
                public const string Modulus = "n";
                public const string Exponent = "e";
            }

            public static class OAuth
            {
                public const string Scope = "scope";
                public const string Bearer = "Bearer";
                public const string ClientId = "client_id";
                public const string TokenType = "token_type";
                public const string ExpiresIn = "expires_in";
                public const string GrantType = "grant_type";
                public const string AccessToken = "access_token";
                public const string ClientSecret = "client_secret";
                public const string ExtExpiresIn = "ext_expires_in";
                public const string ClientCredentials = "client_credentials";
                public const string CognitiveServicesScope = "https://cognitiveservices.azure.com/.default";
                public const string TokenEndpointFormat = "https://login.microsoftonline.com/{0}/oauth2/v2.0/token";
            }
        }       

        public static class JsonPropertyNames
        {
            public const string Data = "data";
            public const string WebId = "webId";
            public const string Value = "value";
            public const string Model = "model";
            public const string Usage = "usage";
            public const string Index = "index";
            public const string Object = "object";
            public const string Values = "values";
            public const string SiteUrl = "siteUrl";
            public const string Resource = "resource";
            public const string RecordId = "recordId";
            public const string TenantId = "tenantId";
            public const string Embedding = "embedding";
            public const string ClientState = "clientState";
            public const string TotalTokens = "total_tokens";
            public const string PromptTokens = "prompt_tokens";
            public const string SubscriptionId = "subscriptionId";
            public const string ExpirationDateTime = "expirationDateTime";
        }

        public static class Settings
        {
            public const string EntraId = nameof(EntraId);
            public const string KeyVaultURL = "AZURE_KEY_VAULT_URL";
            public const string TokenFlowURL = nameof(TokenFlowURL);
            public const string CorsAllowedOrigins = "CORS_ALLOWED_ORIGINS";
            public const string UseSwaggerAuthFlow = nameof(UseSwaggerAuthFlow);
            public const string VisionModelVersion = nameof(VisionModelVersion);
            public const string EmbeddingAPIVersion = nameof(EmbeddingAPIVersion);
            public const string ChatCompletionAPIVersion = nameof(ChatCompletionAPIVersion);
            public const string DocumentIntelligenceAPIVersion = nameof(DocumentIntelligenceAPIVersion);

            public static class KeyVault
            {
                public static class Search
                {
                    public const string APIURL = "api-url";
                    public const string URL = "search-api-url";
                    public const string Key = "search-admin-key";
                    public const string ResourceId = "search-resource-id";
                    public const string StorageAccountResourceId = "storage-account-resource-id";
                }

                public static class Foundry
                {
                    public const string LLMModel = "foundry-llm-model";
                    public const string ImageModel = "foundry-image-model";
                    public const string AccountKey = "foundry-account-key";
                    public const string SubscriptionId = "subscription-id";
                    public const string EmbeddingModel = "foundry-embedding-model";
                    public const string OpenAIEndpoint = "foundry-open-ai-endpoint";
                    public const string ProjectEndpoint = "foundry-project-endpoint";
                    public const string InferenceEndpoint = "foundry-inference-endpoint";
                    public const string DocumentIntelligenceEndpoint = "foundry-document-intelligence-endpoint";
                }

                public static class EntraID
                {
                    public const string TenantId = "auth-tenant-id";
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

                public static class SharePoint
                {
                    public const string WebhookSecret = "sharepoint-webhook-secret";
                    public const string SiteCollectionURL = "sharepoint-site-collection-url";
                }
            }

            public static class Blazor
            {
                public static class API
                {
                    public const string URL = nameof(URL);
                }
            }
        }

        public static class Search
        {
            public const string EndpointFormat = "https://{0}.search.windows.net/";

            public static class Queries
            {
                public const string Space = "%20";
                public const string Equal = " eq ";
            }

            public static class Indexers
            {
                public const int IntervalMinutes = 5;
            }      

            public static class Profiles
            {
                public const string Vision = "vision-profile-cosine-hnsw";
                public const string OpenAI = "openai-profile-cosine-hnsw";
                public const string Compressed = "compressed-profile-cosine-scalar";
            }

            public static class Vectorization
            {
                public const int TextDimensions = 1536;
                public const string Vision = "vision-vectorizer";
                public const string OpenAI = "openai-vectorizer";
                public const int AzureVisionImageDimensions = 1024;
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

            public static class Normalizers
            {
                public const string Lowercase = "lowercase";
            }

            public static class Indexes
            {
                public const string Images = "sharepoint-foundry-images";
                public const string Documents = "sharepoint-foundry-documents";
                public const string ListIems = "sharepoint-foundry-list-items";
                public const string Vectorized = "sharepoint-foundry-vectorized";
            }

            public static class DataSource
            {
                public const string ImagesName = "sharepoint-foundry-data-source-images";
                public const string DocumentsName = "sharepoint-foundry-data-source-documents";
                public const string ListItemsName = "sharepoint-foundry-data-source-list-items";
            }

            public static class Indexer
            {
                public const string ImagesName = "sharepoint-foundry-indexer-images";
                public const string DocumentsName = "sharepoint-foundry-indexer-documents";
                public const string ListItemsName = "sharepoint-foundry-indexer-list-items";
            }

            public static class Skills
            {
                public const string SplitSkill = "split-skill";
                public const string ShaperSkill = "shaper-skill";
                public const string ExtractionSkill = "extraction-skill";
                public const string ProperCaseSkill = "proper-case-skill";
                public const string ChatCompletion = "chat-completion-skill";
                public const string ListItemTitleSkill = "list-item-title-skill";
                public const string ImageEmbeddingSkill = "image-embedding-skill";
                public const string ContentEmbeddingSkill = "content-embedding-skill";
                public const string EntityRecognitionSkill = "entity-recognition-skill";
                public const string FullNameEmbeddingSkill = "full-name-embedding-skill";
                public const string ImageURLEmbeddingSkill = "image-url-embedding-skill";
                public const string ImageVectorizationSkill = "image-vectorization-skill";
                public const string ListItemDescriptionSkill = "list-item-description-skill";
                public const string ImageVerbalizationEmbeddingSkill = "image-verbalization-embedding-skill";
            }

            public static class Skillset
            {
                public const string ImagesName = "sharepoint-foundry-skillset-images";
                public const string DocumentsName = "sharepoint-foundry-skillset-documents";
                public const string ListItemsName = "sharepoint-foundry-skillset-listitems";
            }

            public static class Chunking
            {
                public const int MaximumPagesToTake = 0;
                public const int PageOverlapLength = 500;
                public const int MaximumPageLength = 2000;
            }

            public static class Abstration
            {
                public const string Star = "*";
                public const string Data = "data";
                public const string Text = "text";
                public const string Pages = "pages";
                public const string Image = "image";
                public const string Vector = "vector";
                public const string Output = "output";
                public const string Content = "content";
                public const string Response = "response";
                public const string Document = "/document";
                public const string FileData = "file_data";
                public const string FullName = "full_name";
                public const string Embedding = "embedding";
                public const string TextItems = "textItems";
                public const string ImagePath = "imagePath";
                public const string NewImages = "new_normalized_images";
                public const string VerbalizedImage = "verbalizedImage";
                public const string ExtractedContent = "extracted_content";
                public const string NormalizedImages = "normalized_images";
                public const string ExtractedImages = "extracted_normalized_images";
                public const string ImageVector = Abstration.Image + "_" + Abstration.Vector;
                public const string FullNameVector = Abstration.FullName + "_" + Abstration.Vector;
                public const string ImagePathVector = Abstration.ImagePath + "_" + Abstration.Vector;
                public const string VerbalizedImageVector = Abstration.VerbalizedImage + "_" + Abstration.Vector;
            }

            public static class ImageExtraction
            {
                public const int MaxSize = 2000;
                public const string ImageAction = "imageAction";
                public const string ImagePathFormat = "='{0}/'+$({1})";
                public const string NormalizedImageMaxWidth = "normalizedImageMaxWidth";
                public const string NormalizedImageMaxHeight = "normalizedImageMaxHeight";
                public const string GenerateNormalizedImages = "generateNormalizedImages";
            }

            public static class ImageVerbialization
            {
                public const string UserPrompt = "='Please describe this image.'";
                public const string SystemPrompt = "='You are tasked with generating concise, accurate descriptions of images, figures, diagrams, or charts in documents. The goal is to capture the key information and meaning conveyed by the image without including extraneous details like style, colors, visual aesthetics, or size.\n\nInstructions:\nContent Focus: Describe the core content and relationships depicted in the image.\n\nFor diagrams, specify the main elements and how they are connected or interact.\nFor charts, highlight key data points, trends, comparisons, or conclusions.\nFor figures or technical illustrations, identify the components and their significance.\nClarity & Precision: Use concise language to ensure clarity and technical accuracy. Avoid subjective or interpretive statements.\n\nAvoid Visual Descriptors: Exclude details about:\n\nColors, shading, and visual styles.\nImage size, layout, or decorative elements.\nFonts, borders, and stylistic embellishments.\nContext: If relevant, relate the image to the broader content of the technical document or the topic it supports.\n\nExample Descriptions:\nDiagram: \"A flowchart showing the four stages of a machine learning pipeline: data collection, preprocessing, model training, and evaluation, with arrows indicating the sequential flow of tasks.\"\n\nChart: \"A bar chart comparing the performance of four algorithms on three datasets, showing that Algorithm A consistently outperforms the others on Dataset 1.\"\n\nFigure: \"A labeled diagram illustrating the components of a transformer model, including the encoder, decoder, self-attention mechanism, and feedforward layers.'";
            }

            public static class ChatCompletion
            {
                public const int TimeoutMinutes = 1;
                public const string UserMessage = "userMessage";
                public const string SystemMessage = "systemMessage";
                public const string EndpointFormat = "{0}" + Routing.Foundry.OpenAIDeployments + "{1}/chat/completions?api-version={2}";
            }

            public static class Fields
            {
                public const string IdDelimiter = "=";
                public const string Timestamp = nameof(Timestamp);
                public const string TitleVector = nameof(TitleVector);
                public const string DescriptionVector = nameof(DescriptionVector);
                public const string MetadataStoragePath = "metadata_storage_path";
                public const string MetadataStorageLastModified = "metadata_storage_last_modified";
            }
        }

        public static class AzureStorage
        {
            public static class RetryPolicy
            {
                public const int Attempts = 5;
                public static readonly TimeSpan Backoff = TimeSpan.FromSeconds(30);
            }

            public static class Blobs
            {
                public const int Parallelism = 8;
                public const string ImageContainer = "extracted-images";
                public const string SourceContainer = "sharepoint-ingestion";
            }

            public static class Tables
            {
                public const int BatchSize = 100;
                public const string URL = nameof(URL);
                public const string SharePointListItems = nameof(SharePointListItems);
                public const string SharePointDeltaTokens = nameof(SharePointDeltaTokens);
                public const string ExtractedImageMetadata = nameof(ExtractedImageMetadata);
            }
        }

        public static class Foundry
        {
            public const string Client = nameof(Foundry);
            public const string EncodingFormat = "float";
            public const string ModelId = "prebuilt-layout";
            public const string WorkflowYaml = nameof(WorkflowYaml);
            public const string Scope = "https://ai.azure.com/user_impersonation";

            public static class Tools
            {
                public const string Type = "type";
                public const string APIType = "ApiType";
                public const string SiteURL = "site_url";
                public const string SharePointTarget = "-";
                public const string DisplayName = "displayName";
                public const string SearchType = "azure_ai_search";
                public const string ResourceId = nameof(ResourceId);
                public const string AppInsights = nameof(AppInsights);
                public const string SharePointGrounding = "sharepoint_grounding_preview";
            }
        }

        public static class Graph
        {
            public const string Version = "v1.0/";
            public const string Scope = Graph.URL + ".default";
            public const string URL = "https://graph.microsoft.com/";
        }

        public static class SharePoint
        {
            public const string Client = nameof(SharePoint);
            public const string Placeholder = nameof(Placeholder);
            public const string SiteCollectionURL = "https://netorg14925960.sharepoint.com/";
            public const string FileDownloadURLFormat = Graph.URL + Graph.Version + "sites/{0}/drives/{1}/items/{2}/content";

            public static class Fields
            {
                public const string Title = nameof(Title);
                public const string Description = "Case_x0020_Description";
            }
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
                public const string Search = "search";
                public const string Status = "status";
                public const string Webook = "webhook";
                public const string ProperCase = "proper-case";
                public const string SyncListItem = "sync-list-item";
                public const string SearchQuery = "search/{query?}";
                public const string DeployVectorized = "vectorized";
                public const string VectorizeImage = "vectorize-image";
                public const string ExecuteWorkflow = "execute-workflow";
                public const string ConverseWithAgent = "converse-with-agent";
                public const string PromoteFoundryAgents = "promote-foundry-agents";
                public const string MigrateCopilotAgents = "migrate-copilot-agents";
                public const string DeploySharePointDocuments = "sharepoint-foundry";
                public const string MigrateStorageAccount = "migrate-storage-account";
                public const string GetFoundryProjectSettings = "get-foundry-project-setting";
                public const string DeploySharePointListItems = "sharepoint-foundry-list-items";
            }

            public static class Foundry
            {
                public const string Embedddings = "/embeddings?api-version=";
                public const string OpenAIDeployments = "openai/deployments/";
            }

            public static class Blazor
            {
                public const string HR = "/hr";
                public const string Expertise = "/expertise";
            }
        }

        public static class HTTP
        {
            public const string Post = "POST";
            public const string SubDomainFormat = "{0}://{1}";
            public const string WWWAuthenticate = "www-authenticate";
            public const string Base64URL = "data:image/jpeg;base64,";
        }

        public static class Agents
        {
            public const string HR = "hr-agent";
        }

        public static class Workflows
        {
            public const string JSONTerminator = "```";
            public const string ExpertiseWorkflow = "expertise-workflow";
            public const string JSONDelimiter = "json" + Workflows.JSONTerminator;
        }

        public static class Blazor
        {
            public const string Head = "head::after";
            public const string Controller = "foundry";
            public const string ApplicationRoot = "#app";
        }

        public static class API
        {
            public const string Scope = "API Access";
            public const string Name = "Foundry SharePoint Knowledge API";
        }

        public static class OpenTelemetry
        {
            public const int MaxBodyLength = 8192;
            public const string Tag = "http.request.body";
        }
    }
}
