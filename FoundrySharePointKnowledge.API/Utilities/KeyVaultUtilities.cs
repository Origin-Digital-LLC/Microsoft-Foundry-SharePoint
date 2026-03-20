using System;
using System.Threading.Tasks;

using Azure;
using Azure.Security.KeyVault.Secrets;

using FoundrySharePointKnowledge.Common;
using FoundrySharePointKnowledge.Domain.Settings;

namespace FoundrySharePointKnowledge.API.Utilities
{
    /// <summary>
    /// These are the key vault helpers.
    /// </summary>
    public static class KeyVaultUtilities
    {
        #region Public Methods
        /// <summary>
        /// Gets Azure Search settings from Key Vault.
        /// </summary>
        public static async Task<AzureSearchSettings> GetAzureSearchSettingsAsync(SecretClient keyVaultClient)
        {
            //initialization
            Task<Response<KeyVaultSecret>> searchURLSecret = keyVaultClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.Search.URL);
            Task<Response<KeyVaultSecret>> searchKeySecret = keyVaultClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.Search.Key);
            Task<Response<KeyVaultSecret>> apiURLSecret = keyVaultClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.Search.APIURL);
            Task<Response<KeyVaultSecret>> azureStorageResourceIdSecret = keyVaultClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.Search.StorageAccountResourceId);

            //wait for work to finish
            AggregateException error = await FSPKUtilities.WhenAllAsync(searchURLSecret, searchKeySecret, apiURLSecret, azureStorageResourceIdSecret);
            if (error != null)
                throw new Exception($"Unable to get Azure Search key vault secrets: {error}");

            //extract secrets
            string searchURL = KeyVaultUtilities.GetSecretValue(searchURLSecret?.Result, FSPKConstants.Settings.KeyVault.Search.URL);
            string searchKey = KeyVaultUtilities.GetSecretValue(searchKeySecret?.Result, FSPKConstants.Settings.KeyVault.Search.Key);
            string apiURL = KeyVaultUtilities.GetSecretValue(apiURLSecret?.Result, FSPKConstants.Settings.KeyVault.Search.APIURL);
            string azureStorageResourceId = KeyVaultUtilities.GetSecretValue(azureStorageResourceIdSecret?.Result, FSPKConstants.Settings.KeyVault.Search.StorageAccountResourceId);

            //return
            return new AzureSearchSettings(searchURL, searchKey, azureStorageResourceId, apiURL);
        }

        /// <summary>
        /// Gets Microsoft Foundry settings from Key Vault.
        /// </summary>
        public static async Task<FoundrySettings> GetFoundrySettingsAsync(SecretClient keyVaultClient, string embeddingAPIVersion, string documentIntelligenceAPIVersion, string chatCompletionAPIVersion, string visionModelVersion)
        {
            //initialization
            Task<Response<KeyVaultSecret>> llmModelSecret = keyVaultClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.Foundry.LLMModel);
            Task<Response<KeyVaultSecret>> accountKeySecret = keyVaultClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.Foundry.AccountKey);
            Task<Response<KeyVaultSecret>> imageModelSecret = keyVaultClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.Foundry.ImageModel);
            Task<Response<KeyVaultSecret>> embeddingModelSecret = keyVaultClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.Foundry.EmbeddingModel);
            Task<Response<KeyVaultSecret>> openAIEndpointSecret = keyVaultClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.Foundry.OpenAIEndpoint);
            Task<Response<KeyVaultSecret>> inferenceEndpointSecret = keyVaultClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.Foundry.InferenceEndpoint);
            Task<Response<KeyVaultSecret>> documentIntelligenceEndpointSecret = keyVaultClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.Foundry.DocumentIntelligenceEndpoint);

            //wait for work to finish
            AggregateException error = await FSPKUtilities.WhenAllAsync(llmModelSecret, accountKeySecret, imageModelSecret, documentIntelligenceEndpointSecret, openAIEndpointSecret, inferenceEndpointSecret, embeddingModelSecret);
            if (error != null)
                throw new Exception($"Unable to get Microsoft Foundry key vault secrets: {error}");

            //extract secrets
            string llmModel = KeyVaultUtilities.GetSecretValue(llmModelSecret?.Result, FSPKConstants.Settings.KeyVault.Foundry.LLMModel);
            string accountKey = KeyVaultUtilities.GetSecretValue(accountKeySecret?.Result, FSPKConstants.Settings.KeyVault.Foundry.AccountKey);
            string imageModel = KeyVaultUtilities.GetSecretValue(imageModelSecret?.Result, FSPKConstants.Settings.KeyVault.Foundry.ImageModel);
            string embeddingModel = KeyVaultUtilities.GetSecretValue(embeddingModelSecret?.Result, FSPKConstants.Settings.KeyVault.Foundry.EmbeddingModel);
            string openAIEndpoint = KeyVaultUtilities.GetSecretValue(openAIEndpointSecret?.Result, FSPKConstants.Settings.KeyVault.Foundry.OpenAIEndpoint);
            string inferenceEndpoint = KeyVaultUtilities.GetSecretValue(inferenceEndpointSecret?.Result, FSPKConstants.Settings.KeyVault.Foundry.InferenceEndpoint);            
            string documentIntelligenceEndpoint = KeyVaultUtilities.GetSecretValue(documentIntelligenceEndpointSecret?.Result, FSPKConstants.Settings.KeyVault.Foundry.DocumentIntelligenceEndpoint);

            //return
            return new FoundrySettings(llmModel, accountKey, openAIEndpoint, embeddingAPIVersion, embeddingModel, documentIntelligenceAPIVersion, documentIntelligenceEndpoint, chatCompletionAPIVersion, visionModelVersion, imageModel, inferenceEndpoint);
        }

        /// <summary>
        /// Gets Microsoft Foundry project settings from Key Vault.
        /// </summary>
        public static async Task<FoundryProjectSettings> GetFoundryProjectSettingsAsync(SecretClient keyVaultClient)
        {
            //initialization
            Response<KeyVaultSecret> projectEndpointSecret = await keyVaultClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.Foundry.ProjectEndpoint);

            //extract secrets
            string projectEndpoint = KeyVaultUtilities.GetSecretValue(projectEndpointSecret, FSPKConstants.Settings.KeyVault.Foundry.ProjectEndpoint);

            //return
            return new FoundryProjectSettings(projectEndpoint);
        }

        /// <summary>
        /// Gets Entra Id settings from key vault.
        /// </summary>
        public static async Task<EntraIDSettings> GetEntraIDSettingsAsync(SecretClient keyVaultClient)
        {
            //initialization
            Task<Response<KeyVaultSecret>> tenantIdSecret = keyVaultClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.EntraID.TenantId);
            Task<Response<KeyVaultSecret>> clientIdSecret = keyVaultClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.EntraID.ClientId);
            Task<Response<KeyVaultSecret>> clientSecretSecret = keyVaultClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.EntraID.ClientSecret);

            //wait for work to finish
            AggregateException error = await FSPKUtilities.WhenAllAsync(tenantIdSecret, clientIdSecret, clientSecretSecret);
            if (error != null)
                throw new Exception($"Unable to get Entra ID key vault secrets: {error}");

            //extract secrets
            string tenantId = KeyVaultUtilities.GetSecretValue(tenantIdSecret?.Result, FSPKConstants.Settings.KeyVault.EntraID.TenantId);
            string clientId = KeyVaultUtilities.GetSecretValue(clientIdSecret?.Result, FSPKConstants.Settings.KeyVault.EntraID.ClientId);
            string clientSecret = KeyVaultUtilities.GetSecretValue(clientSecretSecret?.Result, FSPKConstants.Settings.KeyVault.EntraID.ClientSecret);

            //return
            return new EntraIDSettings(tenantId, clientId, clientSecret);
        }

        /// <summary>
        /// Gets Application Insights settings from key vault.
        /// </summary>
        public static async Task<ApplicationInsightsSettings> GetApplicationInsightsSettingsAsync(SecretClient keyVaultClient)
        {
            //initialization
            Task<Response<KeyVaultSecret>> connectionStringSecret = keyVaultClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.ApplicationInsights.ConnectionString);
            Task<Response<KeyVaultSecret>> instrumentationKeySecret = keyVaultClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.ApplicationInsights.InstrumentationKey);

            //wait for work to finish
            AggregateException error = await FSPKUtilities.WhenAllAsync(instrumentationKeySecret, connectionStringSecret);
            if (error != null)
                throw new Exception($"Unable to get Application Insights key vault secrets: {error}");

            //extract secrets
            string connectionString = KeyVaultUtilities.GetSecretValue(connectionStringSecret?.Result, FSPKConstants.Settings.KeyVault.ApplicationInsights.ConnectionString);
            string instrumentationKey = KeyVaultUtilities.GetSecretValue(instrumentationKeySecret?.Result, FSPKConstants.Settings.KeyVault.ApplicationInsights.InstrumentationKey);
            
            //return
            return new ApplicationInsightsSettings(connectionString, instrumentationKey);
        }

        /// <summary>
        /// Gets Application Insights settings from key vault.
        /// </summary>
        public static async Task<BlobStorageSettings> GetBlobStorageSettingsAsync(SecretClient keyVaultClient)
        {
            //initialization
            Task<Response<KeyVaultSecret>> nameSecret = keyVaultClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.BlobStorage.Name);
            Task<Response<KeyVaultSecret>> connectionStringSecret = keyVaultClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.BlobStorage.ConnectionString);

            //wait for work to finish
            AggregateException error = await FSPKUtilities.WhenAllAsync(nameSecret, connectionStringSecret);
            if (error != null)
                throw new Exception($"Unable to get Blob Storage key vault secrets: {error}");

            //extract secrets
            string name = KeyVaultUtilities.GetSecretValue(nameSecret?.Result, FSPKConstants.Settings.KeyVault.BlobStorage.Name);
            string connectionString = KeyVaultUtilities.GetSecretValue(connectionStringSecret?.Result, FSPKConstants.Settings.KeyVault.BlobStorage.ConnectionString);

            //return
            return new BlobStorageSettings(name, connectionString);
        }

        /// <summary>
        /// Gets SharePoint settings from Key Vault.
        /// </summary>
        public static async Task<SharePointSettings> GetSharePointSettingsAsync(SecretClient keyVaultClient)
        {
            //initialization
            Response<KeyVaultSecret> webhookSecretSecret = await keyVaultClient.GetSecretAsync(FSPKConstants.Settings.KeyVault.SharePoint.WebhookSecret);

            //extract secrets
            string webhookSecret = KeyVaultUtilities.GetSecretValue(webhookSecretSecret, FSPKConstants.Settings.KeyVault.SharePoint.WebhookSecret);

            //return
            return new SharePointSettings(webhookSecret);
        }
        #endregion
        #region Private Methods
        /// <summary>
        /// Extracts a secret value from a key vault response.
        /// </summary>
        private static string GetSecretValue(Response<KeyVaultSecret> secret, string name)
        {
            //return
            return secret.Value?.Value ?? throw new Exception($"Could not get {name} keyvault secret.");
        }
        #endregion
    }
}
