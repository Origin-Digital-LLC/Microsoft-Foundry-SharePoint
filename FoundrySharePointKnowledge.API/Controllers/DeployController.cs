using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using FoundrySharePointKnowledge.Common;
using FoundrySharePointKnowledge.Domain.Contracts;

namespace FoundrySharePointKnowledge.API.Controllers
{
    /// <summary>
    /// These endpoitns deploy the search topology.
    /// </summary>
    public class DeployController : BaseController<DeployController>
    {
        #region Initialization
        public DeployController(ISearchService searchService,
                                ILogger<DeployController> logger) : base(logger, searchService) { }
        #endregion
        #region Endpoints
        /// <summary>
        /// Deploys an Azure Search index with vectorized content.
        /// </summary>
        [HttpGet(FSPKConstants.Routing.API.DeployVectorized)]
        public async Task<IActionResult> DeployVectorizedAsync()
        {
            //return
            return await this.DeployIndexAsync(true);
        }

        /// <summary>
        /// Deploys an Azure Search index with a vectorizer.
        /// </summary>
        [HttpGet(FSPKConstants.Routing.API.DeployFoundry)]
        public async Task<IActionResult> DeployFoundryAsync()
        {
            //return
            return await this.DeployIndexAsync(false);
        }
        #endregion
        #region Private Methods
        /// <summary>
        /// Deploys a search index.
        /// </summary>
        private async Task<IActionResult> DeployIndexAsync(bool trueForVectorizedFalseForVectorizable)
        {
#if DEBUG
            //initialization
            string indexName = trueForVectorizedFalseForVectorizable ? FSPKConstants.Search.Indexes.Vectorized : FSPKConstants.Search.Indexes.Foundry;
            this._logger.LogInformation($"Handling create search index {indexName} request from {this.HttpContext.Connection.RemoteIpAddress}.");

            //deploy
            string result = trueForVectorizedFalseForVectorizable ? await this._searchService.EnsureVectorizedIndexAsync(indexName)
                                                                  : await this._searchService.EnsureVectorizableBlobIndexAsync(indexName);

            //return
            if (string.IsNullOrWhiteSpace(result))
                return this.Ok($"Search index {indexName} deployed successfully.");
            else
                return this.StatusCode(500, $"Failed to deploy search index {indexName}: {result}");
#else
            //return
            await Task.Yield();
            return this.StatusCode(403, "Search deployment is only allowed locally.");
#endif
        }
        #endregion
    }
}
