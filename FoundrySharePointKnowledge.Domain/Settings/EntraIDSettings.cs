using System;

namespace FoundrySharePointKnowledge.Domain.Settings
{
    /// <summary>
    /// This holds Entra ID settings from Key Vault.
    /// </summary>
    public class EntraIDSettings
    {
        #region Initialization
        public EntraIDSettings(string tenantId, string clientId, string clientSecret)
        {
            //initialization
            this.ClientSecret = clientSecret;
            this.TenantId = Guid.Parse(tenantId);
            this.ClientId = Guid.Parse(clientId);
        }
        #endregion
        #region Properties
        public Guid TenantId { get; set; }
        public Guid ClientId { get; set; }
        public string ClientSecret { get; set; }
        #endregion
    }
}
