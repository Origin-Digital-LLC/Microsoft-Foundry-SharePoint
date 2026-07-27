using System;
using System.Threading.Tasks;

using Microsoft.Identity.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Client;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

using FoundrySharePointKnowledge.Common;
using FoundrySharePointKnowledge.Domain.Foundry;
using FoundrySharePointKnowledge.Domain.Tranches;
using FoundrySharePointKnowledge.Domain.Settings;
using FoundrySharePointKnowledge.Domain.Contracts;
using FoundrySharePointKnowledge.Domain.Foundry.Agents;
using FoundrySharePointKnowledge.Domain.Foundry.VectorStores;
using FoundrySharePointKnowledge.Domain.Foundry.Conversations;
using Prompt = FoundrySharePointKnowledge.Domain.Foundry.Conversations.Prompt;

namespace FoundrySharePointKnowledge.API.Controllers
{
    /// <summary>
    /// These endpoints handle Foundry chat completions.
    /// </summary>
    public class FoundryController : BaseController<FoundryController>
    {
        #region Members
        private readonly IFoundryService _foundryService;
        private readonly ITrancheService _trancheService;
        private readonly EntraIDSettings _entraIdSettings;
        private readonly IBackgroundQueue _backgroundQueue;
        private readonly ITokenAcquisition _tokenAcquisition;
        private readonly ITokenExchangeService _tokenExchangeService;
        private readonly FoundryProjectSettings _foundryProjectSettings;
        #endregion
        #region Initialization
        public FoundryController(ISearchService searchService,
                                 IFoundryService foundryService,
                                 ITrancheService trancheService,
                                 EntraIDSettings entraIDSettings,
                                 IBackgroundQueue backgroundQueue,
                                 ILogger<FoundryController> logger,
                                 ITokenAcquisition tokenAcquisition,
                                 ITokenExchangeService tokenExchangeService,
                                 FoundryProjectSettings foundryProjectSettings) : base(logger, searchService)
        {
            //initialization
            this._foundryService = foundryService ?? throw new ArgumentNullException(nameof(foundryService));
            this._trancheService = trancheService ?? throw new ArgumentNullException(nameof(trancheService));
            this._entraIdSettings = entraIDSettings ?? throw new ArgumentNullException(nameof(entraIDSettings));
            this._backgroundQueue = backgroundQueue ?? throw new ArgumentNullException(nameof(backgroundQueue));
            this._tokenAcquisition = tokenAcquisition ?? throw new ArgumentNullException(nameof(tokenAcquisition));
            this._tokenExchangeService = tokenExchangeService ?? throw new ArgumentNullException(nameof(tokenExchangeService));
            this._foundryProjectSettings = foundryProjectSettings ?? throw new ArgumentNullException(nameof(foundryProjectSettings));
        }
        #endregion
        #region Endpoints
        /// <summary>
        /// Executes a workflow.
        /// </summary>
        [HttpPost(FSPKConstants.Routing.API.ExecuteWorkflow)]
        public async Task<IActionResult> ExecuteWorkflowAsync([FromBody()] Prompt prompt)
        {
            //initialization
            this._logger.LogInformation($"Handling request to {nameof(this.ExecuteWorkflowAsync)} from {this.HttpContext.Connection.RemoteIpAddress}.");

            try
            {
                //check prompt
                if (string.IsNullOrWhiteSpace(prompt?.UserMessage))
                    return this.BadRequest($"Please specify a prompt to the {prompt.Agent} workflow.");

                //determine which workflow to execute
                switch (prompt.Agent)
                {
                    //bios
                    case Agent.Bios:

                        //run workflow
                        this._logger.LogInformation($"Executing the {prompt.Agent} workflow.");
                        AgentResponse<EngineerBio[]> result = await this._foundryService.ExecuteExpertiseFinderWorkflowAsync(prompt.UserMessage, this._entraIdSettings.ToCredential());

                        //return
                        if (result == null)
                            return this.BadRequest($"The {prompt.Agent} workflow failed.");
                        else
                            return this.Ok(result);

                    //invalid workflow
                    default:

                        //error
                        string error = $"{prompt.Agent} is not a supported workflow.";
                        this._logger.LogWarning(error);
                        return this.BadRequest(error);
                }
            }
            catch (Exception ex)
            {
                //error
                this._logger.LogError(ex, $"Failed to execute {prompt.Agent} workflow for prompt {prompt.UserMessage}.");
                return this.BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Starts or continues a conversation with an agent.
        /// </summary>
        [HttpPost(FSPKConstants.Routing.API.ConverseWithAgent)]
        public async Task<IActionResult> ConverseWithAgentAsync([FromBody()] ConversationPrompt prompt)
        {
            //initialization
            this._logger.LogInformation($"Handling request to {nameof(this.ConverseWithAgentAsync)} from {this.HttpContext.Connection.RemoteIpAddress}.");

            //check prompt
            if (string.IsNullOrWhiteSpace(prompt?.UserMessage))
                return this.BadRequest($"Please specify a prompt to the {prompt.Agent} agent.");

            //get current user
            string userName = this.HttpContext.User.Identity.Name;

            try
            {
                //exchange API token for foundry token
                FoundryCredential foundryCredential = await this._tokenExchangeService.GetFoundryCredentialAsync(userName);

                //return
                AgentResponse<string> result = await this._foundryService.ConverseWithAgentAsync(prompt, foundryCredential);
                if (string.IsNullOrWhiteSpace(result?.Message))
                    return this.BadRequest($"Failed to converse with agent {prompt.Agent}.");
                else
                    return this.Ok(result);
            }
            catch (MicrosoftIdentityWebChallengeUserException ex)
            {
                //incremental consent error
                this._logger.LogWarning(ex, $"The current user {userName} has not consented to {FSPKConstants.Foundry.Scope}. Instructing the frontend to start the incremental consent flow.");

                //return
                await this._tokenAcquisition.ReplyForbiddenWithWwwAuthenticateHeaderAsync(ex.Scopes, ex.MsalUiRequiredException);
                return new EmptyResult();
            }
            catch (MsalUiRequiredException ex)
            {
                //token is expired or requires user interaction (MFA, conditional access, etc.)
                this._logger.LogWarning(ex, $"Interactive sign-in required for {userName}. ErrorCode={ex.ErrorCode}.");

                //return
                await this._tokenAcquisition.ReplyForbiddenWithWwwAuthenticateHeaderAsync(Array.Empty<string>(), ex);
                return new EmptyResult();
            }
            catch (Exception ex)
            {
                //general error
                this._logger.LogError(ex, $"General token exchange error for {userName}.");
                return this.BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Promotes Foundry agents from one project to another.
        /// </summary>
        [HttpPost(FSPKConstants.Routing.API.PromoteFoundryAgents)]
        public async Task<IActionResult> PromoteFoundryAgentsAsync([FromBody()] MigrateAgentsRequest request)
        {
            //initialization
            this._logger.LogInformation($"Handling request to {nameof(this.PromoteFoundryAgentsAsync)} from {this.HttpContext.Connection.RemoteIpAddress}.");

            //migrate agents
            MigrateAgentsResponse response = await this._foundryService.PromoteAgentsAsync(request, this._entraIdSettings.ToCredential());

            //return
            if (response.IsSuccessful)
                return this.Ok(response);
            else
                return this.BadRequest(response);
        }

        /// <summary>
        /// Ensure a vector store with the given name exists in a Foundry project.
        /// </summary>
        [HttpPut(FSPKConstants.Routing.API.EnsureVectorStore)]
        public async Task<IActionResult> EnsureVectorStoreAsync(string name)
        {
            //initialization
            this._logger.LogInformation($"Handling request to {nameof(this.EnsureVectorStoreAsync)} from {this.HttpContext.Connection.RemoteIpAddress}.");

            //check prompt
            if (string.IsNullOrWhiteSpace(name))
                return this.BadRequest("Please specify the vector store name.");

            //return
            string vectorStoreId = await this._foundryService.EnsureVectorStoreAsync(name);
            if (string.IsNullOrWhiteSpace(vectorStoreId))
                return this.BadRequest($"Failed to create vector store {name}.");
            else
                return this.Ok(vectorStoreId);
        }

        /// <summary>
        /// Starts uploading a tranche's files to a Foundry project in the background; an upload of any size
        /// outlives its request, so this reports only that the operation was accepted and its progress is
        /// tracked against the tranche from there.
        /// </summary>
        [HttpPost(FSPKConstants.Routing.API.UploadFiles)]
        public async Task<IActionResult> UploadFilesAsync([FromBody()] UploadFilesRequest uploadFilesRequest)
        {
            //initialization
            this._logger.LogInformation($"Handling request to {nameof(this.UploadFilesAsync)} from {this.HttpContext.Connection.RemoteIpAddress}.");

            try
            {
                //check request
                if (uploadFilesRequest == null || string.IsNullOrWhiteSpace(uploadFilesRequest.ContainerName))
                    return this.BadRequest("Please specify the tranche and container to upload.");

                //a tranche that has gone away has nothing to upload
                TrancheTableEntity tranche = await this._trancheService.LoadTrancheAsync(uploadFilesRequest.TrancheId);
                if (tranche == null)
                    return this.NotFound($"Tranche {uploadFilesRequest.TrancheId} was not found.");

                //mark the upload as started before queueing it, so a poll arriving first cannot read the
                //tranche as though nothing had been asked of it
                await this._trancheService.EditTrancheAsync(new EditTrancheRequest(tranche)
                {
                    //assemble object
                    UploadedFileProgress = FSPKConstants.Blazor.Synchronization.UploadStarted
                });

                //hand the upload off to a background worker, which resolves its own services
                this._backgroundQueue.Enqueue(async (serviceProvider, cancellationToken) =>
                {
                    //upload
                    ITrancheService trancheService = serviceProvider.GetRequiredService<ITrancheService>();
                    await trancheService.UploadTrancheFilesAsync(uploadFilesRequest, cancellationToken);
                });

                //return
                return this.Accepted();
            }
            catch (Exception ex)
            {
                //error
                return this.BadRequest($"Failed to start uploading files to Foundry: {ex.Message}");
            }
        }

        /// <summary>
        /// Starts indexing a tranche's uploaded files into its vector store in the background; the files are
        /// indexed one batch at a time and a tranche of any size outlives its request, so this reports only
        /// that the operation was accepted and its progress is tracked against the tranche from there.
        /// </summary>
        [HttpPost(FSPKConstants.Routing.API.IndexFiles)]
        public async Task<IActionResult> IndexFilesAsync([FromBody()] IndexFilesRequest indexFilesRequest)
        {
            //initialization
            this._logger.LogInformation($"Handling request to {nameof(this.IndexFilesAsync)} from {this.HttpContext.Connection.RemoteIpAddress}.");

            try
            {
                //check request
                if (indexFilesRequest == null || string.IsNullOrWhiteSpace(indexFilesRequest.VectorStoreId))
                    return this.BadRequest("Please specify the tranche and vector store to index.");

                //a tranche that has gone away has nothing to index
                TrancheTableEntity tranche = await this._trancheService.LoadTrancheAsync(indexFilesRequest.TrancheId);
                if (tranche == null)
                    return this.NotFound($"Tranche {indexFilesRequest.TrancheId} was not found.");

                //mark the indexing as started before queueing it, so a poll arriving first cannot read the
                //tranche as though nothing had been asked of it
                await this._trancheService.EditTrancheAsync(new EditTrancheRequest(tranche)
                {
                    //assemble object
                    IndexedFileProgress = FSPKConstants.Blazor.Synchronization.IndexingStarted
                });

                //hand the indexing off to a background worker, which resolves its own services
                this._backgroundQueue.Enqueue(async (serviceProvider, _) =>
                {
                    //index
                    ITrancheService trancheService = serviceProvider.GetRequiredService<ITrancheService>();
                    await trancheService.IndexTrancheFilesAsync(indexFilesRequest);
                });

                //return
                return this.Accepted();
            }
            catch (Exception ex)
            {
                //error
                return this.BadRequest($"Failed to start indexing files to Foundry vector store {indexFilesRequest?.VectorStoreId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the progress of an ongoing Foundry vector store indexing operation.
        /// </summary>
        [HttpPost(FSPKConstants.Routing.API.IndexFilesProgress)]
        public async Task<IActionResult> GetIndexOperationProgressAsync([FromBody()] IndexProgressRequest indexProgressRequest)
        {
            //initialization
            this._logger.LogInformation($"Handling request to {nameof(this.GetIndexOperationProgressAsync)} from {this.HttpContext.Connection.RemoteIpAddress}.");

            try
            {
                //poll
                return this.Ok(await this._foundryService.GetIndexOperationProgressAsync(indexProgressRequest));
            }
            catch (Exception ex)
            {
                //error
                return this.BadRequest($"Failed to get the indexing progress of Foundry vector store {indexProgressRequest?.VectorStoreId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Removes all files from a Foundry project's vector store.
        /// </summary>
        [HttpPost(FSPKConstants.Routing.API.ResetFiles)]
        public async Task<IActionResult> ResetFilesAsync([FromBody()] ResetFilesRequest resetFilesRequest)
        {
            //initialization
            this._logger.LogInformation($"Handling request to {nameof(this.ResetFilesAsync)} from {this.HttpContext.Connection.RemoteIpAddress}.");

            try
            {
                //reset
                return this.Ok(await this._foundryService.ResetFilesAsync(resetFilesRequest));
            }
            catch (Exception ex)
            {
                //error
                return this.BadRequest($"Failed to reset files in Foundry: {ex.Message}");
            }
        }

        /// <summary>
        /// Returns the Foundry project endpoint.
        /// </summary>
        [AllowAnonymous()]
        [HttpGet(FSPKConstants.Routing.API.GetFoundryProjectSettings)]
        public IActionResult GetFoundryProjectSettings()
        {
            //initialization
            this._logger.LogInformation($"Handling request to {nameof(this.GetFoundryProjectSettings)} from {this.HttpContext.Connection.RemoteIpAddress}.");

            //return
            return this.Ok(this._foundryProjectSettings);
        }
        #endregion
    }
}