using System.Collections.Generic;

using Azure.ResourceManager.CognitiveServices.Models;

namespace FoundrySharePointKnowledge.Domain.Contracts
{
    /// <summary>
    /// Defines the properties and metadata of a Foundry tool.
    /// </summary>
    public interface IToolDefintiion
    {
        #region Methods
        public bool IsAgent { get; }
        Dictionary<string, string> ToMetadata();
        Dictionary<string, string> ToCredentialKeys();
        CognitiveServicesConnectionProperties ToProperties();
        #endregion
    }
}
