using System;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Collections.Generic;

using Microsoft.Graph;
using Microsoft.OpenApi;
using Microsoft.Identity.Web;
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
using FoundrySharePointKnowledge.API.Utilities;
using FoundrySharePointKnowledge.Domain.Settings;
using FoundrySharePointKnowledge.Domain.Contracts;
using FoundrySharePointKnowledge.Infrastructure.Services;

using OpenTelemetry.Logs;

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

            //get settings
            SecretClient keyVaultClient = Program.AddKeyVaultClient(builder);
            EntraIDSettings entraIDSettings = await KeyVaultUtilities.GetEntraIDSettingsAsync(keyVaultClient);
            ApplicationInsightsSettings applicationInsightsSettings = await KeyVaultUtilities.GetApplicationInsightsSettingsAsync(keyVaultClient);

            //configure authentication
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                            .AddMicrosoftIdentityWebApi((options) =>
                            {
                                //configure JWT tokens
                                options.Audience = entraIDSettings.Scope;
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

            //configure logging
            builder.Services.AddOpenTelemetry().UseAzureMonitor((options) => options.ConnectionString = applicationInsightsSettings.ConnectionString);
            builder.Services.Configure<OpenTelemetryLoggerOptions>(options => options.IncludeScopes = true);
            builder.Logging.AddFilter<OpenTelemetryLoggerProvider>(level => level >= LogLevel.Information);

            //confgure api
            builder.Services.AddHttpClient();
            builder.Services.AddControllers();

            //configure swagger
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
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
            });

            //check local settings
            if (builder.Environment.IsDevelopment())
            {
                //add CORS
                string[] allowedOrigins = builder.Configuration.GetSection(FSPKConstants.Settings.CorsAllowedOrigins).Get<string[]>();
                builder.Services.AddCors(options =>
                {
                    //add policy
                    options.AddPolicy(FSPKConstants.Security.DefaultCorsPolicy, policy =>
                    {
                        //configure policy
                        policy.WithOrigins(allowedOrigins ?? Array.Empty<string>())
                              .AllowAnyMethod()
                              .AllowAnyHeader()
                              .AllowCredentials();
                    });
                });
            }

            //add API clients
            Program.AddGraphClient(builder, entraIDSettings);
            await Program.AddSearchClientsAsync(builder, keyVaultClient);

            //add foundry clients
            FoundrySettings foundrySettings = await Program.AddFoundryClientAsync(builder, keyVaultClient, entraIDSettings);
            Program.AddImageEmbeddingsClient(builder, foundrySettings);

            //add storage clients
            string storageConnectionString = await Program.AddBlobClientAsync(builder, keyVaultClient);
            Program.AddTableClient(builder, storageConnectionString);

            //dependency injection
            builder.Services.AddScoped<ISearchService, SearchService>();
            builder.Services.AddScoped<IFoundryService, FoundryService>();

            //build web app
            WebApp app = builder.Build();

            //configure swagger
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                //configure ui
                options.DocumentTitle = "Foundry SharePoint Knowledge API";
            });

            //configure CORS
            app.UseHttpsRedirection();
            if (builder.Environment.IsDevelopment())
                app.UseCors(FSPKConstants.Security.DefaultCorsPolicy);

            //configure middleware
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            //return
            await app.RunAsync();
        }
        #endregion
        #region Private Methods
        /// <summary>
        /// Registers a singleton Key Vault client.
        /// </summary>
        private static SecretClient AddKeyVaultClient(WebApplicationBuilder builder)
        {
            //initialization
            string keyVaultURL = builder.Configuration[FSPKConstants.Settings.KeyVaultURL];
            if (string.IsNullOrWhiteSpace(keyVaultURL))
                throw new ArgumentNullException(nameof(keyVaultURL), "Key Vault URL not found.");

            //return
            SecretClient keyVaultClient = new SecretClient(new Uri(keyVaultURL), new DefaultAzureCredential());
            builder.Services.AddSingleton(keyVaultClient);
            return keyVaultClient;
        }

        /// <summary>
        /// Registers a singleton Azure Search client.
        /// </summary>
        private static async Task AddSearchClientsAsync(WebApplicationBuilder builder, SecretClient keyVaultClient)
        {
            //initialization
            Dictionary<string, SearchClient> searchClients = new Dictionary<string, SearchClient>();
            AzureSearchSettings searchSettings = await KeyVaultUtilities.GetAzureSearchSettingsAsync(keyVaultClient);

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
        private static async Task<FoundrySettings> AddFoundryClientAsync(WebApplicationBuilder builder, SecretClient keyVaultClient, EntraIDSettings entraIDSettings)
        {
            //initialization
            string visionModelVersion = builder.Configuration[FSPKConstants.Settings.VisionModelVersion];
            string embeddingAPIVersion = builder.Configuration[FSPKConstants.Settings.EmbeddingAPIVersion];
            string chatCompletionAPIVersion = builder.Configuration[FSPKConstants.Settings.ChatCompletionAPIVersion];
            string documentIntelligenceAPIVersion = builder.Configuration[FSPKConstants.Settings.DocumentIntelligenceAPIVersion];

            //load secrets
            FoundryProjectSettings foundryProjectSettings = await KeyVaultUtilities.GetFoundryProjectSettingsAsync(keyVaultClient);
            FoundrySettings foundrySettings = await KeyVaultUtilities.GetFoundrySettingsAsync(keyVaultClient, embeddingAPIVersion, documentIntelligenceAPIVersion, chatCompletionAPIVersion, visionModelVersion);

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
        private static async Task<string> AddBlobClientAsync(WebApplicationBuilder builder, SecretClient keyVaultClient)
        {
            //initialization    
            BlobClientOptions options = new BlobClientOptions();
            BlobStorageSettings blobStorageSettings = await KeyVaultUtilities.GetBlobStorageSettingsAsync(keyVaultClient);

            //configure telemetry
            options.Diagnostics.IsLoggingEnabled = false;
            options.Diagnostics.IsTelemetryEnabled = false;
            options.Diagnostics.IsDistributedTracingEnabled = false;

            //configure retry
            options.Retry.Mode = RetryMode.Exponential;
            options.Retry.Delay = FSPKConstants.AzureStorage.RetryPolicy.Backoff;
            options.Retry.MaxRetries = FSPKConstants.AzureStorage.RetryPolicy.Attempts;

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

            //configure telemetry
            options.Diagnostics.IsLoggingEnabled = false;
            options.Diagnostics.IsTelemetryEnabled = false;
            options.Diagnostics.IsDistributedTracingEnabled = false;

            //configure retry
            options.Retry.Mode = RetryMode.Exponential;
            options.Retry.Delay = FSPKConstants.AzureStorage.RetryPolicy.Backoff;
            options.Retry.MaxRetries = FSPKConstants.AzureStorage.RetryPolicy.Attempts;

            //return
            builder.Services.AddSingleton(new TableServiceClient(blobConnectionString, options));
        }
        #endregion
    }
}
