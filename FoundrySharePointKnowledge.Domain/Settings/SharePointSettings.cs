using System;

namespace FoundrySharePointKnowledge.Domain.Settings
{
    /// <summary>
    /// This holds SharePoint settings from Key Vault.
    /// </summary>
    public record SharePointSettings
    {
        #region Initialization
        public SharePointSettings(string webhookSecret)
        {
            //initialization
            this.WebhookSecret = string.IsNullOrWhiteSpace(webhookSecret) ? throw new ArgumentNullException(nameof(webhookSecret)) : webhookSecret;
        }
        #endregion
        #region Properties
        public string WebhookSecret { get; init; }
        #endregion
    }
}
