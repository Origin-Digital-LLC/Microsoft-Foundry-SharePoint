using Azure.AI.Projects.Agents;
using Azure.ResourceManager.CognitiveServices;
using Azure.ResourceManager.CognitiveServices.Models;

using FoundrySharePointKnowledge.Common;
using FoundrySharePointKnowledge.Domain.Contracts;

namespace FoundrySharePointKnowledge.Domain.Foundry
{
    /// <summary>
    /// This holds the settings for an Azure AI Search tool.
    /// </summary>
    public record SearchToolDefinition : IToolDefintion
    {
        #region Initialization
        public SearchToolDefinition(string searchServiceName, string resourceId, string adminKey, string indexName, AzureAISearchQueryType? queryType, string filter, int? topK)
        {
            //initialization
            this.TopK = topK;
            this.Filter = filter;
            this.AdminKey = adminKey;
            this.IndexName = indexName;
            this.QueryType = queryType;
            this.ResourceId = resourceId;
            this.SearchServiceName = searchServiceName;
        }
        #endregion
        #region Properties
        public int? TopK { get; init; }
        public string Filter { get; init; }
        public string AdminKey { get; init; }
        public string IndexName { get; init; }
        public string ResourceId { get; init; }
        public string SearchServiceName { get; init; }
        public AzureAISearchQueryType? QueryType { get; init; }

        public bool IsAgent => true;
        public ToolType ConnectionType => ToolType.Search;
        public string SearchServiceURL => string.Format(FSPKConstants.Search.EndpointFormat, this.SearchServiceName);
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
        public CognitiveServicesConnectionData CreateConnection()
        {
            //initialization
            ApiKeyAuthConnectionProperties connection = new ApiKeyAuthConnectionProperties
            {
                //assemble object
                IsSharedToAll = true,
                Target = this.SearchServiceURL,
                CredentialsKey = this.AdminKey,
                Category = CognitiveServicesConnectionCategory.CognitiveSearch
            };

            //add metadata
            connection.Metadata.Add(nameof(ResourceId), this.ResourceId);
            connection.Metadata.Add(FSPKConstants.Foundry.Tools.APIType, nameof(Azure));
            connection.Metadata.Add(FSPKConstants.Foundry.Tools.DisplayName, this.SearchServiceName);
            connection.Metadata.Add(FSPKConstants.Foundry.Tools.Type, FSPKConstants.Foundry.Tools.SearchType);

            //return
            return new CognitiveServicesConnectionData(connection);
        }
        #endregion
    }
}
