using System;
using System.Threading.Tasks;

using Microsoft.Identity.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;

using FoundrySharePointKnowledge.Common;
using FoundrySharePointKnowledge.Domain.Foundry;
using FoundrySharePointKnowledge.Domain.Settings;
using FoundrySharePointKnowledge.Domain.Contracts;

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
        private readonly FoundryProjectSettings _foundryProjectSettings;
        #endregion
        #region Initialization
        public FoundryController(ISearchService searchService,
                                 IFoundryService foundryService,
                                 EntraIDSettings entraIDSettings,
                                 ILogger<FoundryController> logger,
                                 ITokenAcquisition tokenAcquisition,
                                 FoundryProjectSettings foundryProjectSettings) : base(logger, searchService)
        {
            //initialization
            this._foundryService = foundryService ?? throw new ArgumentNullException(nameof(foundryService));
            this._entraIdSettings = entraIDSettings ?? throw new ArgumentNullException(nameof(entraIDSettings));
            this._tokenAcquisition = tokenAcquisition ?? throw new ArgumentNullException(nameof(tokenAcquisition));
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
                        this._logger.LogInformation($"Executing the {prompt.Agent} workflow.");
                        return this.Ok(await this._foundryService.ExecuteExpertiseFinderWorkflowAsync(prompt.UserMessage, this._entraIdSettings.ToCredential()));

                    //invalid workflow
                    default:
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

            try
            {
                //check prompt
                if (string.IsNullOrWhiteSpace(prompt?.UserMessage))
                    return this.BadRequest($"Please specify a prompt to the {prompt.Agent} agent.");

                //exchange API token for foundry token
                string apiToken = await this.HttpContext.GetTokenAsync(FSPKConstants.Security.AccessToken);
                string foundryToken = await this._tokenAcquisition.GetAccessTokenForUserAsync([FSPKConstants.Foundry.Scope]);

                //return
                return this.Ok(await this._foundryService.ConverseWithAgentAsync(prompt, new FoundryCredential(foundryToken, FSPKConstants.Security.TokenExpirationMinutes)));
            }
            catch (Exception ex)
            {
                //error
                this._logger.LogError(ex, $"Failed to execute {prompt.Agent} workflow for prompt {prompt.UserMessage}.");
                return this.BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Returns the Foundry project endpoint.
        /// </summary>
        [HttpPost(FSPKConstants.Routing.API.CreateAgent)]
        public async Task<IActionResult> CreateAgentAsync([FromBody()] CreateAgentRequest request)
        {
            //initialization
            this._logger.LogInformation($"Handling request to {nameof(this.CreateAgentAsync)} from {this.HttpContext.Connection.RemoteIpAddress}.");

            //create agent
            string result = await this._foundryService.DeployAgentAsync(request, this._entraIdSettings.ToCredential());

            //return
            if (string.IsNullOrWhiteSpace(result))
                return this.Ok();
            else
                return this.BadRequest(result);
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