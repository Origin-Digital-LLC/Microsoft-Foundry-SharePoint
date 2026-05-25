using System.Threading.Tasks;
using System.Collections.Generic;

using FoundrySharePointKnowledge.Domain.Settings;

namespace FoundrySharePointKnowledge.Domain.Contracts
{
    public interface IKeyVaultService
    {
        #region Methods
        Task<EntraIDSettings> GetEntraIDSettingsAsync();
        Task<SharePointSettings> GetSharePointSettingsAsync();
        Task<AzureSearchSettings> GetAzureSearchSettingsAsync();
        Task<BlobStorageSettings> GetBlobStorageSettingsAsync();
        Task<FoundryProjectSettings> GetFoundryProjectSettingsAsync();
        Task<ApplicationInsightsSettings> GetApplicationInsightsSettingsAsync();
        Task<Dictionary<string, string>> GetAllSecretsAsync(string keyVaultURL = null);
        Task<FoundrySettings> GetFoundrySettingsAsync(string embeddingAPIVersion, string documentIntelligenceAPIVersion, string chatCompletionAPIVersion, string visionModelVersion);
        #endregion
    }
}