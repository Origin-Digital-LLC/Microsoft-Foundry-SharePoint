using System;

using FoundrySharePointKnowledge.Common;

namespace FoundrySharePointKnowledge.Domain.Settings
{
    /// <summary>
    /// This holds Microsoft Foundry project settings from Key Vault.
    /// </summary>
    public record FoundryProjectSettings
    {
        #region Initialization
        [Obsolete("This is only needed for deserialization.")]
        public FoundryProjectSettings() { }
        public FoundryProjectSettings(string subscriptionId, string projectEndpoint)
        {
            //initialization
            this.ProjectEndpoint = string.IsNullOrWhiteSpace(projectEndpoint) ? throw new ArgumentNullException(nameof(projectEndpoint)) : FSPKUtilities.ParseURI(projectEndpoint, nameof(projectEndpoint));

            //return
            if (Guid.TryParse(subscriptionId, out _))
                this.SubscriptionId = subscriptionId;
            else
                throw new ArgumentNullException(nameof(subscriptionId));
        }
        #endregion
        #region Properties
        public Uri ProjectEndpoint { get; init; }
        public string SubscriptionId { get; init; }
        #endregion
    }
}
