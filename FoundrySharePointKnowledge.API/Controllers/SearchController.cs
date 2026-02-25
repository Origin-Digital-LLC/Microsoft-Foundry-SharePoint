using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using FoundrySharePointKnowledge.Common;
using FoundrySharePointKnowledge.Domain.Contracts;
using FoundrySharePointKnowledge.Domain.SharePoint;
using FoundrySharePointKnowledge.Domain.Search.WebAPISkill;
using FoundrySharePointKnowledge.Domain.Search.WebAPISkill.ProperCase;
using FoundrySharePointKnowledge.Domain.Search.WebAPISkill.ImageVectorization;

namespace FoundrySharePointKnowledge.API.Controllers
{
    /// <summary>
    /// These endpoints handle Power Automate document processing requests.
    /// </summary>
    public class SearchController : BaseController<SearchController>
    {
        #region Members
        private readonly IFoundryService _foundryService;
        #endregion
        #region Initialization
        public SearchController(ISearchService searchService,
                                IFoundryService foundryService,
                                ILogger<SearchController> logger) : base(logger, searchService) 
        {
            //initialization
            this._foundryService = foundryService ?? throw new ArgumentNullException(nameof(foundryService));
        }
        #endregion
        #region Endpoints
        /// <summary>
        /// Ingests a SharePoint file into an Azure Search index.
        /// </summary>
        [HttpPost(FSPKConstants.Routing.API.Ingest)]
        public async Task<IActionResult> IngestAsync([FromBody()] SPFile file)
        {
            //initialization
            this._logger.LogInformation($"Handling request to {nameof(this.IngestAsync)} from {this.HttpContext.Connection.RemoteIpAddress}.");
            bool result = await this._searchService.InjestFileAsync(file);

            //return
            if (result)
                return this.Ok($"{file} has been ingested successfully.");
            else
                return this.StatusCode(500, $"Failed to ingest {file?.ToString() ?? "N/A"}.");
        }

        /// <summary>
        /// Uploads a SharePoint file to blob storage.
        /// </summary>
        [HttpPost(FSPKConstants.Routing.API.Upload)]
        public async Task<IActionResult> UploadAsync([FromBody()] SPFile file)
        {
            //initialization
            this._logger.LogInformation($"Handling request to {nameof(this.UploadAsync)} from {this.HttpContext.Connection.RemoteIpAddress}.");
            bool result = await this._searchService.UploadFileAsync(file);

            //return
            if (result)
                return this.Ok($"{file} has been ingested successfully.");
            else
                return this.StatusCode(500, $"Failed to ingest {file?.ToString() ?? "N/A"}.");
        }

        /// <summary>
        /// Deletes a file from Azure Search and blob storage.
        /// </summary>
        [HttpPost(FSPKConstants.Routing.API.Delete)]
        public async Task<IActionResult> DeleteAsync([FromBody()] SPFile file)
        {
            //initialization
            this._logger.LogInformation($"Handling request to {nameof(this.DeleteAsync)} from {this.HttpContext.Connection.RemoteIpAddress}.");
            bool result = await this._searchService.DeleteFileAsync(file);

            //return
            if (result)
                return this.Ok($"{file} has been deleted successfully.");
            else
                return this.StatusCode(500, $"Failed to delete {file?.ToString() ?? "N/A"}.");
        }

        /// <summary>
        /// Issues a simple query against the foundry index.
        /// </summary>
        [HttpGet(FSPKConstants.Routing.API.SearchQuery)]
        public async Task<IActionResult> SearchQueryAsync([FromRoute()] string query = FSPKConstants.Search.Abstration.Star)
        {
            //initialization            
            this._logger.LogInformation($"Handling request to {nameof(this.SearchQueryAsync)} from {this.HttpContext.Connection.RemoteIpAddress}.");

            try
            {
                //return
                return this.Ok(await this._searchService.SearchAsync(query));
            }
            catch (Exception ex)
            {
                //error
                this._logger.LogError(ex, $"Failed to search for query {query}.");
                return this.BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// A custom skillset to vectorize an image extracted from a SharePoint document.
        /// </summary>
        [HttpPost(FSPKConstants.Routing.API.VectorizeImage)]
        public async Task<IActionResult> VectorizeImageAsync([FromBody()] WebAPISkillPayload<ImageVectorizationInput> payload)
        {
            //initialization
            this._logger.LogInformation($"Handling request to {nameof(this.VectorizeImageAsync)} from {this.HttpContext.Connection.RemoteIpAddress}.");

            try
            {
                //return                
                return this.Ok(await this._searchService.VectorizeImagesAsync(payload));
            }
            catch (Exception ex)
            {
                //error
                this._logger.LogError(ex, $"Failed to vectorize images: {payload}");
                return this.BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// A custom skillset to convert raw text to proper case.
        /// </summary>
        [HttpPost(FSPKConstants.Routing.API.ProperCase)]
        public async Task<IActionResult> ProperCaseAsync([FromBody()] WebAPISkillPayload<ProperCaseInput> payload)
        {
            //initialization
            this._logger.LogInformation($"Handling request to {nameof(this.ProperCaseAsync)} from {this.HttpContext.Connection.RemoteIpAddress}.");

            try
            {
                //return                
                return this.Ok(await this._searchService.ToProperCaseAsync(payload));
            }
            catch (Exception ex)
            {
                //error
                this._logger.LogError(ex, $"Failed to proper case text: {payload}");
                return this.BadRequest(ex.Message);
            }
        }
        #endregion       
    }
}
