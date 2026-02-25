using System;

using Azure.Search.Documents.Indexes.Models;

using FoundrySharePointKnowledge.Common;

namespace FoundrySharePointKnowledge.Domain.Settings
{
    /// <summary>
    /// This holds Microsoft Foundry settings from Key Vault.
    /// </summary>
    public record FoundrySettings
    {
        #region Initialization
        public FoundrySettings(string llmModel, string accountKey, string openAIEndpoint, string embeddingAPIVersion, string embeddingModel, string documentIntelligenceAPIVersion, string documentIntelligenceEndpoint, string chatCompletionAPIVersion, string visionModelVersion, string imageModel, string inferenceAPIEndpoint)
        {
            //initialization
            this.LLMModel = string.IsNullOrWhiteSpace(llmModel) ? throw new ArgumentNullException(nameof(llmModel)) : llmModel;
            this.AccountKey = string.IsNullOrWhiteSpace(accountKey) ? throw new ArgumentNullException(nameof(accountKey)) : accountKey;
            this.ImageModel = string.IsNullOrWhiteSpace(imageModel) ? throw new ArgumentNullException(nameof(imageModel)) : imageModel;
            this.VisionModelVersion = string.IsNullOrWhiteSpace(visionModelVersion) ? throw new ArgumentNullException(nameof(visionModelVersion)) : visionModelVersion;
            this.EmbeddingAPIVersion = string.IsNullOrWhiteSpace(embeddingAPIVersion) ? throw new ArgumentNullException(nameof(embeddingAPIVersion)) : embeddingAPIVersion;
            this.EmbeddingModel = string.IsNullOrWhiteSpace(embeddingModel) ? throw new ArgumentNullException(nameof(embeddingModel)) : new AzureOpenAIModelName(embeddingModel);
            this.ChatCompletionAPIVersion = string.IsNullOrWhiteSpace(chatCompletionAPIVersion) ? throw new ArgumentNullException(nameof(chatCompletionAPIVersion)) : chatCompletionAPIVersion;
            this.DocumentIntelligenceAPIVersion = string.IsNullOrWhiteSpace(documentIntelligenceAPIVersion) ? throw new ArgumentNullException(nameof(documentIntelligenceAPIVersion)) : documentIntelligenceAPIVersion;

            //return
            this.OpenAIEndpoint = FSPKUtilities.ParseURI(openAIEndpoint, nameof(openAIEndpoint));
            this.InferenceEndpoint = FSPKUtilities.ParseURI(inferenceAPIEndpoint, nameof(inferenceAPIEndpoint));
            this.DocumentIntelligenceEndpoint = FSPKUtilities.ParseURI(documentIntelligenceEndpoint, nameof(documentIntelligenceEndpoint));
        }
        #endregion
        #region Properties
        public string LLMModel { get; init; }
        public string AccountKey { get; init; }
        public string ImageModel { get; init; }
        public Uri OpenAIEndpoint { get; init; }
        public Uri InferenceEndpoint { get; init; }
        public string VisionModelVersion { get; init; }
        public string EmbeddingAPIVersion { get; init; }
        public string ChatCompletionAPIVersion { get; init; }
        public Uri DocumentIntelligenceEndpoint { get; init; }
        public AzureOpenAIModelName EmbeddingModel { get; init; }
        public string DocumentIntelligenceAPIVersion { get; init; }
        public string EmbeddingDeploymentName => this.EmbeddingModel.ToString();
        #endregion
    }
}
