using System;

namespace FoundrySharePointKnowledge.Domain.Settings
{
    /// <summary>
    /// This holds Azure Application Insights settings from Key Vault.
    /// </summary>
    public record ApplicationInsightsSettings
    {
        #region Initialization
        public ApplicationInsightsSettings(string connectionString, string instrumentationKey)
        {
            //initialization
            this.ConnectionString = string.IsNullOrWhiteSpace(connectionString) ? throw new ArgumentNullException(nameof(connectionString)) : connectionString;
            this.InstrumentationKey = string.IsNullOrWhiteSpace(instrumentationKey) ? throw new ArgumentNullException(nameof(instrumentationKey)) : instrumentationKey;
        }
        #endregion
        #region Properties
        public string ConnectionString { get; init; }
        public string InstrumentationKey { get; init; }
        #endregion        
    }
}
