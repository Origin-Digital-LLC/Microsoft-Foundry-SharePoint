using System;

using FoundrySharePointKnowledge.Common;

namespace FoundrySharePointKnowledge.Domain.Settings
{
    /// <summary>
    /// This holds Azure Search settings from Key Vault.
    /// </summary>
    public record AzureSearchSettings
    {
        #region Initialization
        public AzureSearchSettings(string searchURL, string searchKey, string azureStorageResourceId, string apiURL)
        {
            //initialization
            this.SearchURL = string.IsNullOrWhiteSpace(searchURL) ? throw new ArgumentNullException(nameof(searchURL)) : searchURL;
            this.SearchKey = string.IsNullOrWhiteSpace(searchKey) ? throw new ArgumentNullException(nameof(searchKey)) : searchKey;
            this.WebAPISkillEndpoint = string.IsNullOrWhiteSpace(apiURL) ? throw new ArgumentNullException(nameof(apiURL)) : apiURL.CombineURL(FSPKConstants.Routing.API.Search);
            this.AzureStorageResourceId = string.IsNullOrWhiteSpace(azureStorageResourceId) ? throw new ArgumentNullException(nameof(azureStorageResourceId)) : azureStorageResourceId;
        }
        #endregion
        #region Properties
        public string SearchURL { get; init; }
        public string SearchKey { get; init; }
        public string WebAPISkillEndpoint { get; init; }
        public string AzureStorageResourceId { get; init; }
        #endregion
    }
}
