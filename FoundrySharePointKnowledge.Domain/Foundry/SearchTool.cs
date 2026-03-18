using System.Collections.Generic;
using Azure.ResourceManager.CognitiveServices.Models;

using FoundrySharePointKnowledge.Common;
using FoundrySharePointKnowledge.Domain.Contracts;

namespace FoundrySharePointKnowledge.Domain.Foundry
{
    /// <summary>
    /// This holds the settings for an Azure AI Search tool.
    /// </summary>
    public record SearchTool : IToolDefintiion
    {
        #region Initialization
        public SearchTool(string searchServiceName, string resourceId, string adminKey)
        {
            //initialization
            this.AdminKey = adminKey;
            this.ResourceId = resourceId;
            this.SearchServiceName = searchServiceName;
        }
        #endregion
        #region Properties
        public bool IsAgent => true;
        public string AdminKey { get; init; }
        public string ResourceId { get; init; }
        public string SearchServiceName { get; init; }
        public string SearchServiceURL => $"{this.SearchServiceName}{FSPKConstants.Search.EndpointSuffix}";
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return this.SearchServiceName;
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
                Target = this.SearchServiceURL,
                CredentialsKey = this.AdminKey,
                Category = CognitiveServicesConnectionCategory.CognitiveSearch
            };
        }

        /// <summary>
        /// Builds the tool definition's metadata.
        /// </summary>
        public Dictionary<string, string> ToMetadata()
        {
            //return
            return new Dictionary<string, string>()
            {
                //assemble dictionary
                {  nameof(ResourceId), this.ResourceId },
                {  FSPKConstants.Foundry.Tools.APIType, nameof(Azure) },
                {  FSPKConstants.Foundry.Tools.DisplayName, this.SearchServiceName },
                {  FSPKConstants.Foundry.Tools.Type, FSPKConstants.Foundry.Tools.SearchType }
            };
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
