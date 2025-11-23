using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using FoundrySharePointKnowledge.Common;
using FoundrySharePointKnowledge.Domain.Contracts;
using FoundrySharePointKnowledge.Domain.SharePoint;

namespace FoundrySharePointKnowledge.API.Controllers
{
    /// <summary>
    /// These endpoints handle Power Automate document processing requests.
    /// </summary>
    public class SearchController : BaseController<SearchController>
    {
        #region Initialization
        public SearchController(ISearchService searchService,
                                ILogger<SearchController> logger) : base(logger, searchService) { }
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
        /// Upload a SharePoint file to blob storage.
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
        #endregion       
    }
}
