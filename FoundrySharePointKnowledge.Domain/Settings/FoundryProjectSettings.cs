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
        public FoundryProjectSettings(string projectEndpoint)
        {
            //initialization
            this.ProjectEndpoint = FSPKUtilities.ParseURI(projectEndpoint, nameof(projectEndpoint));
        }
        #endregion
        #region Properties
        public Uri ProjectEndpoint { get; init; }
        #endregion
    }
}
