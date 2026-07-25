using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using FoundrySharePointKnowledge.Common;
using FoundrySharePointKnowledge.Domain.Tranches;
using FoundrySharePointKnowledge.Domain.Contracts;

namespace FoundrySharePointKnowledge.API.Controllers
{
    /// <summary>
    /// These endpoints handle bulk upload tranches: direct-to-storage uploads and their metadata.
    /// </summary>
    public class TrancheController : BaseController<TrancheController>
    {
        #region Members
        private readonly ITrancheService _trancheService;
        #endregion
        #region Initialization
        public TrancheController(ISearchService searchService,
                                 ITrancheService trancheService,
                                 ILogger<TrancheController> logger) : base(logger, searchService)
        {
            //initialization
            this._trancheService = trancheService ?? throw new ArgumentNullException(nameof(trancheService));
        }
        #endregion
        #region Endpoints
        /// <summary>
        /// Creates a bulk upload tranche by creating a container and returning a short-lived SAS URL.
        /// </summary>
        [HttpPost(FSPKConstants.Routing.API.Create)]
        public async Task<IActionResult> CreateTrancheAsync([FromBody()] CreateTrancheRequest request)
        {
            //initialization
            this._logger.LogInformation($"Handling request to {nameof(this.CreateTrancheAsync)} from {this.HttpContext.Connection.RemoteIpAddress}.");

            //get current user
            string userName = this.HttpContext.User.Identity.Name;

            try
            {
                //check user
                if (string.IsNullOrWhiteSpace(userName))
                    return this.BadRequest("Unable to determine the current user.");

                //return
                UploadSession session = await this._trancheService.CreateTrancheAsync(userName, request?.Name);
                return this.Ok(session);
            }
            catch (Exception ex)
            {
                //error
                this._logger.LogError(ex, $"Failed to create a tranche for {userName}.");
                return this.BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Records the files uploaded for a completed bulk upload tranche.
        /// </summary>
        [HttpPost(FSPKConstants.Routing.API.Complete)]
        public async Task<IActionResult> CompleteUploadAsync([FromBody()] CompleteTrancheUploadRequest request)
        {
            //initialization
            this._logger.LogInformation($"Handling request to {nameof(this.CompleteUploadAsync)} from {this.HttpContext.Connection.RemoteIpAddress}.");

            try
            {
                //check request
                if (request == null || request.FileNames.Length == 0 || string.IsNullOrWhiteSpace(request.ContainerName))
                    return this.BadRequest("Please specify the tranche, container, and files to complete.");

                //return
                await this._trancheService.TrackTrancheFilesAsync(request.TrancheId, request.ContainerName, request.FileNames);
                return this.Ok();
            }
            catch (Exception ex)
            {
                //error
                this._logger.LogError(ex, $"Failed to complete upload for tranche {request?.TrancheId}.");
                return this.BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Cancels a bulk upload by deleting its container.
        /// </summary>
        [HttpDelete(FSPKConstants.Routing.API.Cancel)]
        public async Task<IActionResult> CancelUploadAsync(string containerName)
        {
            //initialization
            this._logger.LogInformation($"Handling request to {nameof(this.CancelUploadAsync)} from {this.HttpContext.Connection.RemoteIpAddress}.");

            try
            {
                //check container
                if (string.IsNullOrWhiteSpace(containerName))
                    return this.BadRequest("Please specify the container name to cancel.");

                //return
                await this._trancheService.CancelUploadAsync(containerName);
                return this.Ok();
            }
            catch (Exception ex)
            {
                //error
                this._logger.LogError(ex, $"Failed to cancel upload for container {containerName}.");
                return this.BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Updates a tranche's editable metadata.
        /// </summary>
        [HttpPut(FSPKConstants.Routing.API.EditTranche)]
        public async Task<IActionResult> EditTrancheAsync([FromBody()] EditTrancheRequest request)
        {
            //initialization
            this._logger.LogInformation($"Handling request to {nameof(this.EditTrancheAsync)} from {this.HttpContext.Connection.RemoteIpAddress}.");

            try
            {
                //check request
                if (request == null || string.IsNullOrWhiteSpace(request.Name))
                    return this.BadRequest("Please specify the tranche and name to update.");

                //return
                await this._trancheService.EditTrancheAsync(request);
                return this.Ok();
            }
            catch (Exception ex)
            {
                //error
                this._logger.LogError(ex, $"Failed to edit tranche {request?.TrancheId}.");
                return this.BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Loads all bulk upload tranches for the current user.
        /// </summary>
        [HttpGet(FSPKConstants.Routing.API.Tranches)]
        public async Task<IActionResult> LoadTranchesAsync()
        {
            //initialization
            this._logger.LogInformation($"Handling request to {nameof(this.LoadTranchesAsync)} from {this.HttpContext.Connection.RemoteIpAddress}.");

            //get current user
            string userName = this.HttpContext.User.Identity.Name;

            try
            {
                //check user
                if (string.IsNullOrWhiteSpace(userName))
                    return this.BadRequest("Unable to determine the current user.");

                //return
                TrancheTableEntity[] tranches = await this._trancheService.LoadTranchesAsync(userName);
                return this.Ok(tranches);
            }
            catch (Exception ex)
            {
                //error
                this._logger.LogError(ex, $"Failed to load tranches for {userName}.");
                return this.BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Deletes a tranche's blob container and its tracked table records.
        /// </summary>
        [HttpDelete(FSPKConstants.Routing.API.DeleteTranche)]
        public async Task<IActionResult> DeleteTrancheAsync(string containerName, Guid trancheId)
        {
            //initialization
            this._logger.LogInformation($"Handling request to {nameof(this.DeleteTrancheAsync)} from {this.HttpContext.Connection.RemoteIpAddress}.");

            try
            {
                //check container
                if (string.IsNullOrWhiteSpace(containerName))
                    return this.BadRequest("Please specify the container name to delete.");

                //return
                await this._trancheService.DeleteTrancheAsync(containerName, trancheId);
                return this.Ok();
            }
            catch (Exception ex)
            {
                //error
                this._logger.LogError(ex, $"Failed to delete tranche {trancheId} and container {containerName}.");
                return this.BadRequest(ex.Message);
            }
        }
        #endregion
    }
}
