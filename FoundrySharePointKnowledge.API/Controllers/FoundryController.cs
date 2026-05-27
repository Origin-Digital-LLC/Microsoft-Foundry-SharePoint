using System;
using System.Threading.Tasks;

using Microsoft.Identity.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Client;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;

using FoundrySharePointKnowledge.Common;
using FoundrySharePointKnowledge.Domain.Foundry;
using FoundrySharePointKnowledge.Domain.Settings;
using FoundrySharePointKnowledge.Domain.Contracts;
using FoundrySharePointKnowledge.Domain.Foundry.Agents;
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
        private readonly EntraIDSettings _entraIdSettings;
        private readonly ITokenAcquisition _tokenAcquisition;
        private readonly ITokenExchangeService _tokenExchangeService;
        private readonly FoundryProjectSettings _foundryProjectSettings;
        #endregion
        #region Initialization
        public FoundryController(ISearchService searchService,
                                 IFoundryService foundryService,
                                 EntraIDSettings entraIDSettings,
                                 ILogger<FoundryController> logger,
                                 ITokenAcquisition tokenAcquisition,
                                 ITokenExchangeService tokenExchangeService,
                                 FoundryProjectSettings foundryProjectSettings) : base(logger, searchService)
        {
            //initialization
            this._foundryService = foundryService ?? throw new ArgumentNullException(nameof(foundryService));
            this._entraIdSettings = entraIDSettings ?? throw new ArgumentNullException(nameof(entraIDSettings));
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