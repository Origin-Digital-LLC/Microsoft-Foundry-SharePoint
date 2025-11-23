using System;

namespace FoundrySharePointKnowledge.Domain.Settings
{
    /// <summary>
    /// This holds Azure Application Insights settings from Key Vault.
    /// </summary>
    public class ApplicationInsightsSettings
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
        public string ConnectionString { get; private set; }
        public string InstrumentationKey { get; private set; }
        #endregion        
    }
}
