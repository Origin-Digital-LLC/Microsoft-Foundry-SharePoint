using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Collections.Generic;
using System.Security.Cryptography;

using Microsoft.Graph;
using Microsoft.OpenApi;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using WebApp = Microsoft.AspNetCore.Builder.WebApplication;

using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Search.Documents;
using Azure.AI.DocumentIntelligence;
using Azure.Search.Documents.Indexes;
using Azure.Security.KeyVault.Secrets;

using FoundrySharePointKnowledge.Common;
using FoundrySharePointKnowledge.Infrastructure;
using FoundrySharePointKnowledge.Domain.Settings;
using FoundrySharePointKnowledge.Domain.Contracts;
using FoundrySharePointKnowledge.Infrastructure.Services;

namespace FoundrySharePointKnowledge.API
{
    /// <summary>
    /// This hosts the Foundry SharePoint Knowledge API.
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
            TimeSpan tokenExpiration = TimeSpan.FromMinutes(FSPKConstants.Security.TokenExpirationMinutes);

            //get settings
            SecretClient keyVaultClient = Program.AddKeyVaultClient(builder);
            EntraIDSettings entraIDSettings = await KeyVaultUtilities.GetEntraIDSettingsAsync(keyVaultClient);
            ApplicationInsightsSettings applicationInsightsSettings = await KeyVaultUtilities.GetApplicationInsightsSettingsAsync(keyVaultClient);

            //configure authentication
            builder.Services.AddAuthentication((options) =>
            {
                //set jwt bearer as default
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer((options) =>
            {
                //hook events
                options.Events = new JwtBearerEvents
                {
                    //intercept message received to set validation parameters
                    OnMessageReceived = async (context) =>
                    {
                        //ignore anonymous requests
                        if (Program.IsAnonymousRequest(context.HttpContext))
                            return;

                        //get fresh signing keys
                        SecurityKey[] signingKeys = await Program.GetIssuerSigningKeysAsync(context, entraIDSettings);

                        //set validation parameters
                        options.TokenValidationParameters = new TokenValidationParameters()
                        {
                            //assemble object
                            ValidateIssuer = true,
                            ValidateAudience = true,
                            ValidateLifetime = true,
                            ClockSkew = tokenExpiration,
                            IssuerSigningKeys = signingKeys,
                            ValidateIssuerSigningKey = true,
                            ValidAudience = $"{FSPKConstants.Security.Audience}{entraIDSettings.ClientId}",
                            ValidIssuer = $"{FSPKConstants.Security.Issuer.CombineURL(entraIDSettings.TenantId.ToString())}/"
                        };
                    }
                };
            });

            //configure logging
            builder.Services.AddApplicationInsightsTelemetry((options) =>
            {
                //add application insights
                options.ConnectionString = applicationInsightsSettings.ConnectionString;
            });

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
                    Description = string.Format(FSPKConstants.Security.TokenLinkFormat, builder.Configuration[FSPKConstants.Settings.TokenFlowURL])                    
                };

                //add security
                options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, jwtSecurityScheme);
                options.AddSecurityRequirement(document => new OpenApiSecurityRequirement()
                {
                    //default jwt bearer with no scopes
                    [new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme, document)] = []
                });
            });

            //add clients
            Program.AddGraphClient(builder, entraIDSettings);
            await Program.AddBlobClientAsync(builder, keyVaultClient);
            await Program.AddFoundryClientAsync(builder, keyVaultClient);
            await Program.AddSearchClientsAsync(builder, keyVaultClient);

            //dependency injection
            builder.Services.AddScoped<ISearchService, SearchService>();
            builder.Services.AddScoped<IFoundryService, FoundryService>();

            //build web app
            WebApp app = builder.Build();

            //configure swagger
            app.UseSwagger();
            app.UseSwaggerUI();

            //configure middleware
            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            //return
            await app.RunAsync();
        }
        #endregion
        #region Private Methods
        /// <summary>
        /// Skips token validation for anonymous requests.
        /// </summary>
        private static bool IsAnonymousRequest(HttpContext context)
        {
            //return
            return context?.GetEndpoint()?.Metadata?.GetMetadata<IAllowAnonymous>() != null;
        }

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
            searchClients.Add(FSPKConstants.Search.Indexes.Foundry, new SearchClient(uri, FSPKConstants.Search.Indexes.Foundry, credential));
            searchClients.Add(FSPKConstants.Search.Indexes.Vectorized, new SearchClient(uri, FSPKConstants.Search.Indexes.Vectorized, credential));

            //return
            builder.Services.AddSingleton(searchClients);
            builder.Services.AddSingleton(searchSettings);
            builder.Services.AddSingleton(new SearchIndexClient(uri, credential));
            builder.Services.AddSingleton(new SearchIndexerClient(uri, credential));
        }

        /// <summary>
        /// Registers settings and an HTTP client for Azure Foundry.
        /// </summary>
        private static async Task AddFoundryClientAsync(WebApplicationBuilder builder, SecretClient keyVaultClient)
        {
            //initialization
            string embeddingAPIVersion = builder.Configuration[FSPKConstants.Settings.EmbeddingAPIVersion];
            string documentIntelligenceAPIVersion = builder.Configuration[FSPKConstants.Settings.DocumentIntelligenceAPIVersion];
            AzureFoundrySettings foundrySettings = await KeyVaultUtilities.GetAzureFoundrySettingsAsync(keyVaultClient, embeddingAPIVersion, documentIntelligenceAPIVersion);

            //register settings
            builder.Services.AddSingleton(foundrySettings);

            //register embedding client
            builder.Services.AddHttpClient(FSPKConstants.Foundry.Client, client =>
            {
                //register client
                client.BaseAddress = new Uri(foundrySettings.OpenAIEndpoint);
                client.DefaultRequestHeaders.Add(FSPKConstants.Security.Authorization, $"{JwtBearerDefaults.AuthenticationScheme} {foundrySettings.AccountKey}");
            });

            //return
            builder.Services.AddSingleton(new DocumentIntelligenceClient(new Uri(foundrySettings.DocumentIntelligenceEndpoint),
                                                                         new AzureKeyCredential(foundrySettings.AccountKey)));
        }

        /// <summary>
        /// Registers a client for Microsoft Graph.
        /// </summary>
        private static void AddGraphClient(WebApplicationBuilder builder, EntraIDSettings entraIDSettings)
        {
            //initialization
            string[] scopes = [FSPKConstants.Graph.Scope];
            ClientSecretCredential clientSecretCredential = new ClientSecretCredential(entraIDSettings.TenantId.ToString(), entraIDSettings.ClientId.ToString(), entraIDSettings.ClientSecret);

            //register sharepoint file downloader client
            builder.Services.AddHttpClient(FSPKConstants.SharePoint.Client, client =>
            {
                //authenticate client with graph's credentials (using the synchronous GetToken method here to ensure it is acquired before the request is issued)
                AccessToken token = clientSecretCredential.GetToken(new TokenRequestContext(scopes));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(token.TokenType, token.Token);
            });

            //return
            builder.Services.AddSingleton(new GraphServiceClient(clientSecretCredential, scopes));
        }

        /// <summary>
        /// Configures an azure storage blob client.
        /// </summary>
        private static async Task AddBlobClientAsync(WebApplicationBuilder builder, SecretClient keyVaultClient)
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
            options.Retry.Delay = FSPKConstants.BlobStorage.RetryPolicy.Backoff;
            options.Retry.MaxRetries = FSPKConstants.BlobStorage.RetryPolicy.Attempts;

            //return
            builder.Services.AddSingleton(blobStorageSettings);
            builder.Services.AddSingleton(new BlobServiceClient(blobStorageSettings.ConnectionString, options));
        }

        /// <summary>
        /// https://medium.com/@bikashkshetri/add-msal-authentication-in-azure-functions-net-8-and-function-worker-runtime-dotnet-isolated-4b569e22f85a
        /// </summary>
        private static async Task<SecurityKey[]> GetIssuerSigningKeysAsync(MessageReceivedContext context, EntraIDSettings entraIDSettings)
        {
            //initialization
            List<SecurityKey> keys = new List<SecurityKey>();
            IHttpClientFactory httpClientFactory = context.HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>();
            string jwksURL = FSPKConstants.Security.Instance.CombineURL(entraIDSettings.TenantId.ToString()).CombineURL(FSPKConstants.Security.KeyDiscoveryEndpoint);

            //get signing keys
            string rawKeys = await httpClientFactory.CreateClient().GetStringAsync(jwksURL);
            using JsonDocument parsedKeys = JsonDocument.Parse(rawKeys);

            //enumerate keys
            foreach (JsonElement key in parsedKeys.RootElement.GetProperty(FSPKConstants.Security.RSA.Keys).EnumerateArray())
            {
                //extract rsa parameters
                RSAParameters rsaParameters = new RSAParameters
                {
                    //assemble object
                    Modulus = Base64UrlEncoder.DecodeBytes(key.GetProperty(FSPKConstants.Security.RSA.Modulus).GetString()),
                    Exponent = Base64UrlEncoder.DecodeBytes(key.GetProperty(FSPKConstants.Security.RSA.Exponent).GetString())
                };

                //import rsa paramegers
                RSA rsa = RSA.Create();
                rsa.ImportParameters(rsaParameters);

                //build rsa key
                keys.Add(new RsaSecurityKey(rsa)
                {
                    //assemble object
                    KeyId = key.GetProperty(FSPKConstants.Security.RSA.Kid).GetString()
                });
            }

            //return
            return keys.ToArray();
        }
        #endregion
    }
}
