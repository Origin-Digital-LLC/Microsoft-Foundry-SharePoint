using System.Collections.Generic;

using Azure.ResourceManager.CognitiveServices.Models;

using FoundrySharePointKnowledge.Common;
using FoundrySharePointKnowledge.Domain.Contracts;

namespace FoundrySharePointKnowledge.Domain.Foundry
{
    /// <summary>
    /// This holds the settings for a Foundry project's connection Application Insights.
    /// </summary>
    public record ProjectTelemetry : IToolDefintiion
    {
        #region Initialization
        public ProjectTelemetry(string connectionString, string resourceId)
        {
            //initialization
            this.ResourceId = resourceId;
            this.ConnectionString = connectionString;
        }
        #endregion
        #region Properties
        public bool IsAgent => false;
        public string ResourceId { get; init; }
        public string ConnectionString { get; init; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return this.ResourceId;
        }

        /// <summary>
        /// Builds the tool definition's properties.
        /// </summary>
        public CognitiveServicesConnectionProperties ToProperties()
        {
            //return
            return new ApiKeyAuthConnectionProperties
            {
                //assemble object
                IsSharedToAll = true,
                Target = this.ResourceId,
                CredentialsKey = this.ConnectionString,
                Category = FSPKConstants.Foundry.Tools.AppInsights
            };
        }

        /// <summary>
        /// Builds the tool definition's metadata.
        /// </summary>
        public Dictionary<string, string> ToMetadata()
        {
            //return
            return new Dictionary<string, string>();            
        }

        /// <summary>
        /// Builds the tool definition's credential keys.
        /// </summary>
        public Dictionary<string, string> ToCredentialKeys()
        {
            //return
            return new Dictionary<string, string>();
        }
        #endregion
    }
}
