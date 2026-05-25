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
        public FoundryProjectSettings(string subscriptionId, string projectEndpointSecret)
        {
            //initialization
            if (!Guid.TryParse(subscriptionId, out _))
                throw new ArgumentNullException(nameof(subscriptionId));
            if (string.IsNullOrWhiteSpace(projectEndpointSecret))
                throw new ArgumentNullException(nameof(projectEndpointSecret));

            //this could be an comma-separated string
            string[] projectEndpoints = projectEndpointSecret.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            this.ProjectEndpoints = new Uri[projectEndpoints.Length];

            //parse project endpoint uris
            for (int e = 0; e < projectEndpoints.Length; e++)
                this.ProjectEndpoints[e] = FSPKUtilities.ParseURI(projectEndpoints[e], nameof(projectEndpointSecret));

            //return
            this.SubscriptionId = subscriptionId;
        }
        #endregion
        #region Properties
        public string SubscriptionId { get; init; }
        public Uri[] ProjectEndpoints { get; init; }
        #endregion
    }
}
