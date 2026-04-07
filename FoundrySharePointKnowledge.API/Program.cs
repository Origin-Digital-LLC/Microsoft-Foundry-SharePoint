using System;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Collections.Generic;

using Microsoft.Graph;
using Microsoft.OpenApi;
using Microsoft.Identity.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using WebApp = Microsoft.AspNetCore.Builder.WebApplication;

using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Data.Tables;
using Azure.AI.Inference;
using Azure.Storage.Blobs;
using Azure.Search.Documents;
using Azure.AI.DocumentIntelligence;
using Azure.Search.Documents.Indexes;
using Azure.Security.KeyVault.Secrets;
using Azure.Monitor.OpenTelemetry.AspNetCore;

using FoundrySharePointKnowledge.Common;
using FoundrySharePointKnowledge.API.Middleware;
using FoundrySharePointKnowledge.Domain.Settings;
using FoundrySharePointKnowledge.Domain.Contracts;
using FoundrySharePointKnowledge.Infrastructure.Services;

using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

namespace FoundrySharePointKnowledge.API
{
    /// <summary>
    /// This is the Foundry SharePoint Knowledge API.
    /// </summary>
    public class Program
    {
        #region Initialization
        /// <summary>
        /// This is the entry point for the API.
        /// </summary>
        public static async Task Main(string[] args)
        {
            //initialization            
            WebApplicationBuilder builder = WebApp.CreateBuilder(args);
            bool useSwaggerAuthFlow = Convert.ToBoolean(builder.Configuration[FSPKConstants.Settings.UseSwaggerAuthFlow]);

            //get settings
            KeyVaultService keyVaultService = Program.AddKeyVaultService(builder);
            EntraIDSettings entraIDSettings = await keyVaultService.GetEntraIDSettingsAsync();
            SharePointSettings sharePointSettings = await keyVaultService.GetSharePointSettingsAsync();
            ApplicationInsightsSettings applicationInsightsSettings = await keyVaultService.GetApplicationInsightsSettingsAsync();

            //configure authentication
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                            .AddMicrosoftIdentityWebApi((options) =>
                            {
                                //configure JWT tokens
                                options.TokenValidationParameters.ValidAudiences = [entraIDSettings.Scope, entraIDSettings.ClientId.ToString()];
                            }, (options) =>
                            {
                                //configure MS identity
                                options.AllowWebApiToBeAuthorizedByACL = true;
                                options.ClientId = entraIDSettings.ClientId.ToString();
                                options.TenantId = entraIDSettings.TenantId.ToString();
                                options.Instance = FSPKConstants.Security.TokenValidation.Instance;
                            }).EnableTokenAcquisitionToCallDownstreamApi((options) =>
                            {
                                //configure downstream api
                                options.ClientSecret = entraIDSettings.ClientSecret;
                            }).AddInMemoryTokenCaches();

            //configure application insights via open telemetry
            builder.Services.AddOpenTelemetry()
                            .UseAzureMonitor((options) => options.ConnectionString = applicationInsightsSettings.ConnectionString)
                            .WithTracing((options) =>
                            {
                                //add request body logging
                                options.AddAspNetCoreInstrumentation();
                                options.AddProcessor<RequestBodyTelemtryProcessor>();
                            });

            //configure logging
            builder.Services.Configure<OpenTelemetryLoggerOptions>(options => options.IncludeScopes = true);
            builder.Logging.AddFilter<OpenTelemetryLoggerProvider>(level => level >= LogLevel.Information);

            //confgure api
            builder.Services.AddHttpClient();
            builder.Services.AddControllers();

            //configure swagger
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                //determine security model
                if (useSwaggerAuthFlow)
                {
                    //get oauth values
                    string type = SecuritySchemeType.OAuth2.ToString().ToLowerInvariant();
                    string scope = entraIDSettings.Scope.CombineURL(FSPKConstants.Security.TokenValidation.Scope);

                    //configure open api
                    OpenApiSecurityScheme oAuthScheme = new OpenApiSecurityScheme
                    {
                        //configure oauth
                        Type = SecuritySchemeType.OAuth2,
                        Flows = new OpenApiOAuthFlows
                        {
                            //configure auth flow
                            AuthorizationCode = new OpenApiOAuthFlow
                            {
                                //assemble object
                                AuthorizationUrl = new Uri(FSPKConstants.Security.TokenValidation.Instance.CombineURL(entraIDSettings.TenantId.ToString()).CombineURL(FSPKConstants.Security.TokenValidation.Endpoints.Authorize)),
                                TokenUrl = new Uri(FSPKConstants.Security.TokenValidation.Instance.CombineURL(entraIDSettings.TenantId.ToString()).CombineURL(FSPKConstants.Security.TokenValidation.Endpoints.Token)),
                                Scopes = new Dictionary<string, string>
                                {
                                    //assemble dictionary
                                    { scope, FSPKConstants.API.Scope }
                                }
                            }
                        }
                    };

                    //configure security
                    options.AddSecurityDefinition(type, oAuthScheme);
                    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement()
                    {
                        //assemble array
                        [new OpenApiSecuritySchemeReference(type, document)] = [scope]
                    });
                }
                else
                {
                    //configure open api
                    OpenApiSecurityScheme jwtSecurityScheme = new OpenApiSecurityScheme
                    {
                        //assemble object
                        In = ParameterLocation.Header,
                        Type = SecuritySchemeType.Http,
                        BearerFormat = FSPKConstants.Security.JWT,
                        Name = FSPKConstants.Security.Authorization,
                        Scheme = JwtBearerDefaults.AuthenticationScheme,
                        Description = string.Format(FSPKConstants.Security.TokenLinkFormat, builder.Configuration[FSPKConstants.Settings.TokenFlowURL]),
                    };

                    //add security
                    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, jwtSecurityScheme);
                    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement()
                    {
                        //default jwt bearer with no scopes
                        [new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme, document)] = []
                    });
                }
            });

            //add CORS (make sure Azure CORS settings are completely empty)
            string[] allowedOrigins = builder.Configuration.GetSection(FSPKConstants.Settings.CorsAllowedOrigins)?.Get<string>()?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            builder.Services.AddCors(options =>
            {
                //add policy
                options.AddPolicy(FSPKConstants.Security.DefaultCorsPolicy, policy =>
                {
                    //configure policy
                    policy.AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials()
                          .WithOrigins(allowedOrigins ?? Array.Empty<string>())
                          .WithExposedHeaders(FSPKConstants.HTTP.WWWAuthenticate);
                });
            });

            //add API clients
            Program.AddGraphClient(builder, entraIDSettings);
            await Program.AddSearchClientsAsync(builder, keyVaultService);

            //add foundry clients
            FoundrySettings foundrySettings = await Program.AddFoundryClientAsync(builder, keyVaultService, entraIDSettings);
            Program.AddImageEmbeddingsClient(builder, foundrySettings);

            //add storage clients
            string storageConnectionString = await Program.AddBlobClientAsync(builder, keyVaultService);
            Program.AddTableClient(builder, storageConnectionString);

            //dependency injection
            builder.Services.AddSingleton(sharePointSettings);
            builder.Services.AddScoped<ISearchService, SearchService>();
            builder.Services.AddScoped<IFoundryService, FoundryService>();
            builder.Services.AddScoped<ISharePointService, SharePointService>();

            //build web app
            WebApp app = builder.Build();

            //configure swagger
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                //configure ui
                options.DocumentTitle = FSPKConstants.API.Name;
                if (useSwaggerAuthFlow)
                {
                    //apply auth flow
                    options.OAuthClientId(entraIDSettings.ClientId.ToString());
                    options.OAuthUsePkce();
                }
            });

            //configure server
            app.UseHttpsRedirection();
            app.UseCors(FSPKConstants.Security.DefaultCorsPolicy);

            //configure authentication
            app.UseAuthentication();
            app.UseAuthorization();

            //configure routing
            app.MapGet("/", () => FSPKConstants.API.Name).ExcludeFromDescription();
            app.MapControllers();

            //return
            await app.RunAsync();
        }
        #endregion
        #region Private Methods
        /// <summary>
        /// Registers a singleton Key Vault service.
        /// </summary>
        private static KeyVaultService AddKeyVaultService(WebApplicationBuilder builder)
        {
            //initialization
            string keyVaultURL = builder.Configuration[FSPKConstants.Settings.KeyVaultURL];
            if (string.IsNullOrWhiteSpace(keyVaultURL))
                throw new ArgumentNullException(nameof(keyVaultURL), "Key Vault URL not found.");

            //since the key vault service is used during start up, give it a basic logger (as we don't have the app insights connection string yet)
            using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            ILogger<KeyVaultService> logger = loggerFactory.CreateLogger<KeyVaultService>();

            //return            
            KeyVaultService keyVaultService = new KeyVaultService(new SecretClient(new Uri(keyVaultURL), new DefaultAzureCredential()), logger);
            builder.Services.AddSingleton<IKeyVaultService>(keyVaultService);
            return keyVaultService;
        }

        /// <summary>
        /// Registers a singleton Azure Search client.
        /// </summary>
        private static async Task AddSearchClientsAsync(WebApplicationBuilder builder, KeyVaultService keyVaultService)
        {
            //initialization
            Dictionary<string, SearchClient> searchClients = new Dictionary<string, SearchClient>();
            AzureSearchSettings searchSettings = await keyVaultService.GetAzureSearchSettingsAsync();

            //build client components
            Uri uri = new Uri(searchSettings.SearchURL);
            AzureKeyCredential credential = new AzureKeyCredential(searchSettings.SearchKey);

            //create search clients
            searchClients.Add(FSPKConstants.Search.Indexes.Images, new SearchClient(uri, FSPKConstants.Search.Indexes.Images, credential));
            searchClients.Add(FSPKConstants.Search.Indexes.Foundry, new SearchClient(uri, FSPKConstants.Search.Indexes.Foundry, credential));
            searchClients.Add(FSPKConstants.Search.Indexes.Vectorized, new SearchClient(uri, FSPKConstants.Search.Indexes.Vectorized, credential));

            //return
            builder.Services.AddSingleton(searchClients);
            builder.Services.AddSingleton(searchSettings);
            builder.Services.AddSingleton(new SearchIndexClient(uri, credential));
            builder.Services.AddSingleton(new SearchIndexerClient(uri, credential));
        }

        /// <summary>
        /// Registers settings and an HTTP client for Foundry.
        /// </summary>
        private static async Task<FoundrySettings> AddFoundryClientAsync(WebApplicationBuilder builder, KeyVaultService keyVaultService, EntraIDSettings entraIDSettings)
        {
            //initialization
            string visionModelVersion = builder.Configuration[FSPKConstants.Settings.VisionModelVersion];
            string embeddingAPIVersion = builder.Configuration[FSPKConstants.Settings.EmbeddingAPIVersion];
            string chatCompletionAPIVersion = builder.Configuration[FSPKConstants.Settings.ChatCompletionAPIVersion];
            string documentIntelligenceAPIVersion = builder.Configuration[FSPKConstants.Settings.DocumentIntelligenceAPIVersion];

            //load secrets
            FoundryProjectSettings foundryProjectSettings = await keyVaultService.GetFoundryProjectSettingsAsync();
            FoundrySettings foundrySettings = await keyVaultService.GetFoundrySettingsAsync(embeddingAPIVersion, documentIntelligenceAPIVersion, chatCompletionAPIVersion, visionModelVersion);

            //register settings
            builder.Services.AddSingleton(foundrySettings);
            builder.Services.AddSingleton(foundryProjectSettings);

            //register embedding client
            builder.Services.AddHttpClient(FSPKConstants.Foundry.Client, client =>
            {
                //register client
                client.BaseAddress = foundrySettings.OpenAIEndpoint;
                client.DefaultRequestHeaders.Add(FSPKConstants.Security.Authorization, $"{JwtBearerDefaults.AuthenticationScheme} {foundrySettings.AccountKey}");
            });

            //return
            builder.Services.AddSingleton(new DocumentIntelligenceClient(foundrySettings.DocumentIntelligenceEndpoint, new AzureKeyCredential(foundrySettings.AccountKey)));
            return foundrySettings;
        }

        /// <summary>
        /// Registers a Foundry client for image embeddings.
        /// </summary>
        private static void AddImageEmbeddingsClient(WebApplicationBuilder builder, FoundrySettings foundrySettings)
        {
            //return
            builder.Services.AddSingleton(new ImageEmbeddingsClient(foundrySettings.InferenceEndpoint, new AzureKeyCredential(foundrySettings.AccountKey)));
        }

        /// <summary>
        /// Registers a client for Microsoft Graph.
        /// </summary>
        private static void AddGraphClient(WebApplicationBuilder builder, EntraIDSettings entraIDSettings)
        {
            //initialization
            string[] scopes = [FSPKConstants.Graph.Scope];
            ClientSecretCredential clientSecretCredential = entraIDSettings.ToCredential();

            //register sharepoint file downloader client
            builder.Services.AddHttpClient(FSPKConstants.SharePoint.Client, client =>
            {
                //authenticate client with graph's credentials (using the synchronous GetToken method here to ensure it is acquired before the request is issued)
                AccessToken token = clientSecretCredential.GetToken(new TokenRequestContext(scopes));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(token.TokenType, token.Token);
            });

            //return
            builder.Services.AddSingleton(entraIDSettings);
            builder.Services.AddSingleton(new GraphServiceClient(clientSecretCredential, scopes));
        }

        /// <summary>
        /// Configures an azure storage blob client.
        /// </summary>
        private static async Task<string> AddBlobClientAsync(WebApplicationBuilder builder, KeyVaultService keyVaultService)
        {
            //initialization    
            BlobClientOptions options = new BlobClientOptions();
            BlobStorageSettings blobStorageSettings = await keyVaultService.GetBlobStorageSettingsAsync();

            //configure client
            options.ConfigureAzureStorageOptions();

            //return
            builder.Services.AddSingleton(new BlobServiceClient(blobStorageSettings.ConnectionString, options));
            builder.Services.AddSingleton(blobStorageSettings);
            return blobStorageSettings.ConnectionString;
        }

        /// <summary>
        /// Configures an azure storage table client.
        /// </summary>
        private static void AddTableClient(WebApplicationBuilder builder, string blobConnectionString)
        {
            //initialization
            TableClientOptions options = new TableClientOptions();

            //configure client
            options.ConfigureAzureStorageOptions();

            //return
            builder.Services.AddSingleton(new TableServiceClient(blobConnectionString, options));
        }
        #endregion
    }
}
