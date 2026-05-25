using Azure.ResourceManager.CognitiveServices;
using Azure.ResourceManager.CognitiveServices.Models;

using FoundrySharePointKnowledge.Common;
using FoundrySharePointKnowledge.Domain.Contracts;

namespace FoundrySharePointKnowledge.Domain.Foundry
{
    /// <summary>
    /// This holds the settings for a Foundry project's connection Application Insights.
    /// </summary>
    public record ProjectTelemetryDefinition : IToolDefintion
    {
        #region Initialization
        public ProjectTelemetryDefinition(string connectionString, string resourceId)
        {
            //initialization
            this.ResourceId = resourceId;
            this.ConnectionString = connectionString;
        }
        #endregion
        #region Properties
        public string ResourceId { get; init; }
        public string ConnectionString { get; init; }

        public bool IsAgent => false;
        public ToolType ConnectionType => ToolType.ApplicationInsights;
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
        public CognitiveServicesConnectionData CreateConnection()
        {
            //return
            return new CognitiveServicesConnectionData(new ApiKeyAuthConnectionProperties
            {
                //assemble object
                IsSharedToAll = true,
                Target = this.ResourceId,
                CredentialsKey = this.ConnectionString,
                Category = FSPKConstants.Foundry.Tools.AppInsights
            });
        }
        #endregion
    }
}
