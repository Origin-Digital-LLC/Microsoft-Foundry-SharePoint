using Azure.ResourceManager.CognitiveServices;
using Azure.ResourceManager.CognitiveServices.Models;

using FoundrySharePointKnowledge.Common;
using FoundrySharePointKnowledge.Domain.Contracts;

namespace FoundrySharePointKnowledge.Domain.Foundry
{
    /// <summary>
    /// This holds the settings for a SharePoint Online tool.
    /// </summary>
    public record SharePointToolDefinition : IToolDefintion
    {
        #region Initialization
        public SharePointToolDefinition(string siteCollectionURL)
        {
            //initialization
            this.SiteCollectionURL = siteCollectionURL;
        }
        #endregion
        #region Properties
        public string SiteCollectionURL { get; init; }

        public bool IsAgent => true;
        public ToolType ConnectionType => ToolType.SharePoint;
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return this.SiteCollectionURL;
        }
      
        /// <summary>
        /// Builds the tool definition's properties.
        /// </summary>
        public CognitiveServicesConnectionData CreateConnection()
        {
            //initialization
            CustomKeysConnectionProperties connection = new CustomKeysConnectionProperties
            {
                //assemble object
                IsSharedToAll = true,
                Target = FSPKConstants.Foundry.Tools.SharePointTarget,
                Category = CognitiveServicesConnectionCategory.CustomKeys
            };

            //add metadata
            connection.CredentialsKeys.Add(FSPKConstants.Foundry.Tools.SiteURL, this.SiteCollectionURL);
            connection.Metadata.Add(FSPKConstants.Foundry.Tools.Type, FSPKConstants.Foundry.Tools.SharePointGrounding);

            //return
            return new CognitiveServicesConnectionData(connection);
        }
        #endregion
    }
}
