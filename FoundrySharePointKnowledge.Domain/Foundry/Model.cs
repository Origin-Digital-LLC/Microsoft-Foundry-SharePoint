using System;

using Azure.ResourceManager.CognitiveServices;

namespace FoundrySharePointKnowledge.Domain.Foundry
{
    /// <summary>
    /// This represents a deployed LLM.
    /// </summary>
    public record Model
    {
        #region Initialization
        public Model(CognitiveServicesAccountDeploymentResource deployment)
        {
            //initialization
            if (deployment == null)
                throw new ArgumentNullException(nameof(deployment));

            //return
            this.Name = deployment.Data.Name;
            this.SKU = deployment.Data.Sku.Name;
            this.Capacity = deployment.Data.Sku.Capacity;
        }
        #endregion
        #region Properties
        public string SKU { get; init; }
        public string Name { get; init; }
        public int? Capacity { get; init; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return this.Name;
        }
        #endregion
    }
}
