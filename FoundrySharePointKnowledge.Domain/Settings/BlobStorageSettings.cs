using System;

namespace FoundrySharePointKnowledge.Domain.Settings
{
    /// <summary>
    /// This holds Azure Storage Account settings from Key Vault.
    /// </summary>
    public record BlobStorageSettings
    {
        #region Initialization
        public BlobStorageSettings(string name, string connectionString)
        {
            //initialization
            this.Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentNullException(nameof(name)) : name;
            this.ConnectionString = string.IsNullOrWhiteSpace(connectionString) ? throw new ArgumentNullException(nameof(connectionString)) : connectionString;
        }
        #endregion
        #region Properties
        public string Name { get; init; }
        public string ConnectionString { get; init; }
        #endregion
    }
}
