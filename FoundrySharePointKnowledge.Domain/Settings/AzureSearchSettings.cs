using System;

namespace FoundrySharePointKnowledge.Domain.Settings
{
    /// <summary>
    /// This holds Azure Search settings from Key Vault.
    /// </summary>
    public class AzureSearchSettings
    {
        #region Initialization
        public AzureSearchSettings(string searchURL, string searchKey, string azureStorageResourceId)
        {
            //initialization
            this.SearchURL = string.IsNullOrWhiteSpace(searchURL) ? throw new ArgumentNullException(nameof(searchURL)) : searchURL;
            this.SearchKey = string.IsNullOrWhiteSpace(searchKey) ? throw new ArgumentNullException(nameof(searchKey)) : searchKey;
            this.AzureStorageResourceId = string.IsNullOrWhiteSpace(azureStorageResourceId) ? throw new ArgumentNullException(nameof(azureStorageResourceId)) : azureStorageResourceId;
        }
        #endregion
        #region Properties
        public string SearchURL { get; private set; }
        public string SearchKey { get; private set; }
        public string AzureStorageResourceId { get; private set; }
        #endregion
    }
}
