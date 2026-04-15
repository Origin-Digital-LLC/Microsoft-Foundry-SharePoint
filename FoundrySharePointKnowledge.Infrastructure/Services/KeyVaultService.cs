using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

using FoundrySharePointKnowledge.Common;
using FoundrySharePointKnowledge.Domain.Settings;
using FoundrySharePointKnowledge.Domain.Contracts;

namespace FoundrySharePointKnowledge.Infrastructure.Services
{
    /// <summary>
    /// This provides access to Key Vault secrets.
    /// </summary>
    public class KeyVaultService : IKeyVaultService
    {
        #region Members
        private readonly SecretClient _secretClient;
        private readonly ILogger<KeyVaultService> _logger;
        #endregion
        #region Initialization
        public KeyVaultService(SecretClient secretClient,
                               ILogger<KeyVaultService> logger)
        {
            //initialization
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this._secretClient = secretClient ?? throw new ArgumentNullException(nameof(secretClient));
        }
        #endregion
        #region Public Methods
        /// <summary>
        /// Gets all secrets from Key Vault. Pass in a different url to reference an "external" key vault using the default azure credential.
        /// </summary>
        public async Task<Dictionary<string, string>> GetAllSecretsAsync(string keyVaultURL = null)
        {
            //initialization
            SecretClient secretClient = this._secretClient;
            Dictionary<string, string> secrets = new Dictionary<string, string>();            
            List<Task<Response<KeyVaultSecret>>> getSecretsTasks = new List<Task<Response<KeyVaultSecret>>>();

            //if a Key Vault url was passed, use that instead
            if (!string.IsNullOrWhiteSpace(keyVaultURL))
            {
                //parse url
                if (Uri.TryCreate(keyVaultURL, UriKind.Absolute, out Uri keyVaultURI))
                    secretClient = new SecretClient(keyVaultURI, new DefaultAzureCredential());
                else
                    throw new Exception($"{keyVaultURL} is not a valid Key Vault URI.");
            }

            //get all secret names
            this._logger.LogInformation($"Getting all secrets from {secretClient.VaultUri}.");
            await foreach (SecretProperties secretProperties in secretClient.GetPropertiesOfSecretsAsync())
                getSecretsTasks.Add(secretClient.GetSecretAsync(secretProperties.Name));

            //wait for work to finish
            AggregateException error = await FSPKUtilities.WhenAllAsync(getSecretsTasks);
            if (error != null)
                throw new Exception($"Unable to get all Key Vault {secretClient.VaultUri} secrets: {error}");

            //return
            this._logger.LogInformation($"Found {getSecretsTasks.Count} secret(s) from {secretClient.VaultUri}.");
            return getSecretsTasks.ToDictionary(k => k.Result.Value.Name, v => this.GetSecretValue(v));
        }

        /// <summary>
        /// Gets Azure Search settings from Key Vault.
        /// </summary>
        public async Task<AzureSearchSettings> GetAzureSearchSettingsAsync()
        {
            //initialization
            this._logger.LogInformation($"Getting Azure AI Search secrets from {this._secretClient.VaultUri}.");
            Task<Response<KeyVaultSecret>> searchURLSecret = this._secretClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.Search.URL);
            Task<Response<KeyVaultSecret>> searchKeySecret = this._secretClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.Search.Key);
            Task<Response<KeyVaultSecret>> apiURLSecret = this._secretClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.Search.APIURL);
            Task<Response<KeyVaultSecret>> azureStorageResourceIdSecret = this._secretClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.Search.StorageAccountResourceId);

            //wait for work to finish
            AggregateException error = await FSPKUtilities.WhenAllAsync(searchURLSecret, searchKeySecret, apiURLSecret, azureStorageResourceIdSecret);
            if (error != null)
                throw new Exception($"Unable to get Azure Search Key Vault {this._secretClient.VaultUri} secrets: {error}");

            //extract secrets
            string apiURL = this.GetSecretValue(apiURLSecret);
            string searchURL = this.GetSecretValue(searchURLSecret);
            string searchKey = this.GetSecretValue(searchKeySecret);
            string azureStorageResourceId = this.GetSecretValue(azureStorageResourceIdSecret);

            //return
            return new AzureSearchSettings(searchURL, searchKey, azureStorageResourceId, apiURL);
        }

        /// <summary>
        /// Gets Microsoft Foundry settings from Key Vault.
        /// </summary>
        public async Task<FoundrySettings> GetFoundrySettingsAsync(string embeddingAPIVersion, string documentIntelligenceAPIVersion, string chatCompletionAPIVersion, string visionModelVersion)
        {
            //initialization
            this._logger.LogInformation($"Getting Foundry secrets from {this._secretClient.VaultUri}.");
            Task<Response<KeyVaultSecret>> llmModelSecret = this._secretClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.Foundry.LLMModel);
            Task<Response<KeyVaultSecret>> accountKeySecret = this._secretClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.Foundry.AccountKey);
            Task<Response<KeyVaultSecret>> imageModelSecret = this._secretClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.Foundry.ImageModel);
            Task<Response<KeyVaultSecret>> embeddingModelSecret = this._secretClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.Foundry.EmbeddingModel);
            Task<Response<KeyVaultSecret>> openAIEndpointSecret = this._secretClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.Foundry.OpenAIEndpoint);
            Task<Response<KeyVaultSecret>> inferenceEndpointSecret = this._secretClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.Foundry.InferenceEndpoint);
            Task<Response<KeyVaultSecret>> documentIntelligenceEndpointSecret = this._secretClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.Foundry.DocumentIntelligenceEndpoint);

            //wait for work to finish
            AggregateException error = await FSPKUtilities.WhenAllAsync(llmModelSecret, accountKeySecret, imageModelSecret, documentIntelligenceEndpointSecret, openAIEndpointSecret, inferenceEndpointSecret, embeddingModelSecret);
            if (error != null)
                throw new Exception($"Unable to get Microsoft Foundry Key Vault {this._secretClient.VaultUri} secrets: {error}");

            //extract secrets
            string llmModel = this.GetSecretValue(llmModelSecret);
            string accountKey = this.GetSecretValue(accountKeySecret);
            string imageModel = this.GetSecretValue(imageModelSecret);
            string embeddingModel = this.GetSecretValue(embeddingModelSecret);
            string openAIEndpoint = this.GetSecretValue(openAIEndpointSecret);
            string inferenceEndpoint = this.GetSecretValue(inferenceEndpointSecret);
            string documentIntelligenceEndpoint = this.GetSecretValue(documentIntelligenceEndpointSecret);

            //return
            return new FoundrySettings(llmModel, accountKey, openAIEndpoint, embeddingAPIVersion, embeddingModel, documentIntelligenceAPIVersion, documentIntelligenceEndpoint, chatCompletionAPIVersion, visionModelVersion, imageModel, inferenceEndpoint);
        }

        /// <summary>
        /// Gets Microsoft Foundry project settings from Key Vault.
        /// </summary>
        public async Task<FoundryProjectSettings> GetFoundryProjectSettingsAsync()
        {
            //initialization
            this._logger.LogInformation($"Getting Foundry Project secrets from {this._secretClient.VaultUri}.");
            Task<Response<KeyVaultSecret>> subscriptionIdSecret = this._secretClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.Foundry.SubscriptionId);
            Task<Response<KeyVaultSecret>> projectEndpointSecret = this._secretClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.Foundry.ProjectEndpoint);

            //wait for work to finish
            AggregateException error = await FSPKUtilities.WhenAllAsync(subscriptionIdSecret, projectEndpointSecret);
            if (error != null)
                throw new Exception($"Unable to get Foundry Project Key Vault {this._secretClient.VaultUri} secrets: {error}");

            //extract secrets
            string subscriptionId = this.GetSecretValue(subscriptionIdSecret);
            string projectEndpoint = this.GetSecretValue(projectEndpointSecret);

            //return
            return new FoundryProjectSettings(subscriptionId, projectEndpoint);
        }

        /// <summary>
        /// Gets Entra Id settings from Key Vault.
        /// </summary>
        public async Task<EntraIDSettings> GetEntraIDSettingsAsync()
        {
            //initialization
            this._logger.LogInformation($"Getting Entra ID secrets from {this._secretClient.VaultUri}.");
            Task<Response<KeyVaultSecret>> tenantIdSecret = this._secretClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.EntraID.TenantId);
            Task<Response<KeyVaultSecret>> clientIdSecret = this._secretClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.EntraID.ClientId);
            Task<Response<KeyVaultSecret>> clientSecretSecret = this._secretClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.EntraID.ClientSecret);

            //wait for work to finish
            AggregateException error = await FSPKUtilities.WhenAllAsync(tenantIdSecret, clientIdSecret, clientSecretSecret);
            if (error != null)
                throw new Exception($"Unable to get Entra ID Key Vault {this._secretClient.VaultUri} secrets: {error}");

            //extract secrets
            string tenantId = this.GetSecretValue(tenantIdSecret);
            string clientId = this.GetSecretValue(clientIdSecret);
            string clientSecret = this.GetSecretValue(clientSecretSecret);

            //return
            return new EntraIDSettings(tenantId, clientId, clientSecret);
        }

        /// <summary>
        /// Gets Application Insights settings from Key Vault.
        /// </summary>
        public async Task<ApplicationInsightsSettings> GetApplicationInsightsSettingsAsync()
        {
            //initialization
            this._logger.LogInformation($"Getting Application Insights secrets from {this._secretClient.VaultUri}.");
            Task<Response<KeyVaultSecret>> connectionStringSecret = this._secretClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.ApplicationInsights.ConnectionString);
            Task<Response<KeyVaultSecret>> instrumentationKeySecret = this._secretClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.ApplicationInsights.InstrumentationKey);

            //wait for work to finish
            AggregateException error = await FSPKUtilities.WhenAllAsync(instrumentationKeySecret, connectionStringSecret);
            if (error != null)
                throw new Exception($"Unable to get Application Insights Key Vault {this._secretClient.VaultUri} secrets: {error}");

            //extract secrets
            string connectionString = this.GetSecretValue(connectionStringSecret);
            string instrumentationKey = this.GetSecretValue(instrumentationKeySecret);

            //return
            return new ApplicationInsightsSettings(connectionString, instrumentationKey);
        }

        /// <summary>
        /// Gets Application Insights settings from Key Vault.
        /// </summary>
        public async Task<BlobStorageSettings> GetBlobStorageSettingsAsync()
        {
            //initialization
            this._logger.LogInformation($"Getting Azure Storage Blob secrets from {this._secretClient.VaultUri}.");
            Task<Response<KeyVaultSecret>> nameSecret = this._secretClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.BlobStorage.Name);
            Task<Response<KeyVaultSecret>> connectionStringSecret = this._secretClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.BlobStorage.ConnectionString);

            //wait for work to finish
            AggregateException error = await FSPKUtilities.WhenAllAsync(nameSecret, connectionStringSecret);
            if (error != null)
                throw new Exception($"Unable to get Blob Storage Key Vault {this._secretClient.VaultUri} secrets: {error}");

            //extract secrets
            string name = this.GetSecretValue(nameSecret);
            string connectionString = this.GetSecretValue(connectionStringSecret);

            //return
            return new BlobStorageSettings(name, connectionString);
        }

        /// <summary>
        /// Gets SharePoint settings from Key Vault.
        /// </summary>
        public async Task<SharePointSettings> GetSharePointSettingsAsync()
        {
            //initialization
            this._logger.LogInformation($"Getting SharePoint secrets from {this._secretClient.VaultUri}.");
            string webhookSecret = this.GetSecretValue(this._secretClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.SharePoint.WebhookSecret));

            //return
            return new SharePointSettings(webhookSecret);
        }
        #endregion
        #region Private Methods
        /// <summary>
        /// Extracts a secret value from a Key Vault response.
        /// </summary>
        private string GetSecretValue(Task<Response<KeyVaultSecret>> secretResult)
        {
            //initialization
            KeyVaultSecret secret = secretResult?.Result?.Value;
            if (secret == null)
                throw new Exception("A Key Vault secert was null.");

            //return
            return secret.Value ?? throw new Exception($"Could not get {secret?.Name ?? "N/A"} Key Vault secret.");
        }
        #endregion
    }
}
