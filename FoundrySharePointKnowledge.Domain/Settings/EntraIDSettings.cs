using System;

using Azure.Identity;

using FoundrySharePointKnowledge.Common;

namespace FoundrySharePointKnowledge.Domain.Settings
{
    public record EntraIDSettings
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
        public Guid TenantId { get; init; }
        public Guid ClientId { get; init; }
        public string ClientSecret { get; init; }
        public string Scope => $"{FSPKConstants.Security.TokenValidation.APIAudience}{this.ClientId}";
        #endregion
        #region Public Methods
        /// <summary>
        /// Builds a client secret credential.
        /// </summary>
        public ClientSecretCredential ToCredential()
        {
            //return
            return new ClientSecretCredential(this.TenantId.ToString(), this.ClientId.ToString(), this.ClientSecret);
        }
        #endregion
    }
}
