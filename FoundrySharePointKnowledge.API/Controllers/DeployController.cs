using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using FoundrySharePointKnowledge.Common;
using FoundrySharePointKnowledge.Domain.Contracts;

namespace FoundrySharePointKnowledge.API.Controllers
{
    /// <summary>
    /// These endpoitns deploy the search topography.
    /// </summary>
    public class DeployController : BaseController<DeployController>
    {
        #region Initialization
        public DeployController(ISearchService searchService,
                                ILogger<DeployController> logger) : base(logger, searchService) { }
        #endregion
        #region Endpoints       
        /// <summary>
        /// Deploys an Azure Search index populated from SharePoint remote event recievers (Power Automate).
        /// </summary>
        [HttpPut(FSPKConstants.Routing.API.DeploySharePointDocuments)]
        public async Task<IActionResult> DeploySharePointDocumentSearchAsync()
        {
            //initialization
            this._logger.LogInformation($"Handling {nameof(this.DeploySharePointDocumentSearchAsync)} request from {this.HttpContext.Connection.RemoteIpAddress}.");

            //deploy
            string result = await this._searchService.EnsureVectorizableBlobIndexAsync(FSPKConstants.Search.Indexes.Foundry, FSPKConstants.Search.Indexes.Images);

            //return
            if (string.IsNullOrWhiteSpace(result))
                return this.Ok($"Search index {FSPKConstants.Search.Indexes.Foundry} deployed successfully.");
            else
                return this.StatusCode(500, $"Failed to deploy Foundry SharePoint document search: {result}");
        }

        /// <summary>
        /// Deploys an Azure Search index populated from SharePoint webhooks.
        /// </summary>
        [HttpPut(FSPKConstants.Routing.API.DeploySharePointListItems)]
        public async Task<IActionResult> DeploySharePointListItemsSearchAsync()
        {
            //initialization
            this._logger.LogInformation($"Handling {nameof(this.DeploySharePointListItemsSearchAsync)} request from {this.HttpContext.Connection.RemoteIpAddress}.");

            //return
            string result = await this._searchService.EnsureSharePointListIndexAsync(FSPKConstants.Search.Indexes.ListIems);
            if (string.IsNullOrWhiteSpace(result))
                return this.Ok($"Search index {FSPKConstants.Search.Indexes.ListIems} deployed successfully.");
            else
                return this.StatusCode(500, $"Failed to deploy Foundry SharePoint list item search: {result}");
        }
        #endregion
    }
}
