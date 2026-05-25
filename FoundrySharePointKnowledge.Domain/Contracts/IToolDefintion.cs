using Azure.ResourceManager.CognitiveServices;

using FoundrySharePointKnowledge.Domain.Foundry;

namespace FoundrySharePointKnowledge.Domain.Contracts
{
    /// <summary>
    /// Defines the properties and metadata of a Foundry tool.
    /// </summary>
    public interface IToolDefintion
    {
        #region Properties
        public bool IsAgent { get; }
        public ToolType ConnectionType { get; }
        #endregion
        #region Methods
        CognitiveServicesConnectionData CreateConnection();
        #endregion
    }
}
