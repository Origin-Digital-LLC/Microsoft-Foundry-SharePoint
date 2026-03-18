using System.Collections.Generic;

using Azure.ResourceManager.CognitiveServices.Models;

using FoundrySharePointKnowledge.Common;
using FoundrySharePointKnowledge.Domain.Contracts;

namespace FoundrySharePointKnowledge.Domain.Foundry
{
    /// <summary>
    /// This holds the settings for a SharePoint Online tool.
    /// </summary>
    public record SharePointTool : IToolDefintiion
    {
        #region Initialization
        public SharePointTool(string siteCollectionURL)
        {
            //initialization
            this.SiteCollectionURL = siteCollectionURL;
        }
        #endregion
        #region Properties
        public bool IsAgent => true;
        public string SiteCollectionURL { get; init; }      
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
        public CognitiveServicesConnectionProperties ToProperties()
        {
            //return
            return new CustomKeysConnectionProperties
            {
                //assemble object
                IsSharedToAll = true,
                Target = FSPKConstants.Foundry.Tools.SharePointTarget,
                Category = CognitiveServicesConnectionCategory.CustomKeys
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
                {  FSPKConstants.Foundry.Tools.Type, FSPKConstants.Foundry.Tools.SharePointGrounding }
            };
        }

        /// <summary>
        /// Builds the tool definition's credential keys.
        /// </summary>
        public Dictionary<string, string> ToCredentialKeys()
        {
            //return
            return new Dictionary<string, string>()
            {
                //assemble dictionary               
                {  FSPKConstants.Foundry.Tools.SiteURL, this.SiteCollectionURL }
            };
        }
        #endregion
    }
}
