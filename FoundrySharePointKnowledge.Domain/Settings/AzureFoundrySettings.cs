using System;

namespace FoundrySharePointKnowledge.Domain.Settings
{
    /// <summary>
    /// This holds Azure Foundry settings from Key Vault.
    /// </summary>
    public class AzureFoundrySettings
    {
        #region Initialization
        public AzureFoundrySettings(string accountKey, string openAIEndpoint, string embeddingAPIVersion, string embeddingModel, string documentIntelligenceAPIVersion, string documentIntelligenceEndpoint)
        {
            //initialization
            this.AccountKey = string.IsNullOrWhiteSpace(accountKey) ? throw new ArgumentNullException(nameof(accountKey)) : accountKey;
            this.OpenAIEndpoint = string.IsNullOrWhiteSpace(openAIEndpoint) ? throw new ArgumentNullException(nameof(openAIEndpoint)) : openAIEndpoint;
            this.EmbeddingModel = string.IsNullOrWhiteSpace(embeddingModel) ? throw new ArgumentNullException(nameof(embeddingModel)) : embeddingModel;
            this.EmbeddingAPIVersion = string.IsNullOrWhiteSpace(embeddingAPIVersion) ? throw new ArgumentNullException(nameof(embeddingAPIVersion)) : embeddingAPIVersion;
            this.DocumentIntelligenceEndpoint = string.IsNullOrWhiteSpace(documentIntelligenceEndpoint) ? throw new ArgumentNullException(nameof(documentIntelligenceEndpoint)) : documentIntelligenceEndpoint;
            this.DocumentIntelligenceAPIVersion = string.IsNullOrWhiteSpace(documentIntelligenceAPIVersion) ? throw new ArgumentNullException(nameof(documentIntelligenceAPIVersion)) : documentIntelligenceAPIVersion;
        }
        #endregion
        #region Properties
        public string AccountKey { get; private set; }
        public string OpenAIEndpoint { get; private set; }
        public string EmbeddingModel { get; private set; }
        public string EmbeddingAPIVersion { get; private set; }
        public string DocumentIntelligenceEndpoint { get; private set; }
        public string DocumentIntelligenceAPIVersion { get; private set; }
        #endregion
    }
}
