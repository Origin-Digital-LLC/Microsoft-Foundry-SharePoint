using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.ClientModel;
using System.Threading.Tasks;
using System.Collections.Generic;

using Azure.Core;
using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using Azure.AI.Agents.Persistent;

using Microsoft.Extensions.Logging;

using FoundrySharePointKnowledge.Common;
using FoundrySharePointKnowledge.Domain.Foundry;
using FoundrySharePointKnowledge.Domain.Settings;
using FoundrySharePointKnowledge.Domain.Contracts;

using OpenAI.Responses;

#pragma warning disable OPENAI001

namespace FoundrySharePointKnowledge.Infrastructure.Services
{
    /// <summary>
    /// This interacts with Microsoft Foundry project agents.
    /// </summary>
    public class FoundryService : IFoundryService
    {
        #region Members
        private readonly ILogger<FoundryService> _logger;
        private readonly FoundryProjectSettings _foundryProjectSettings;

        #endregion
        #region Initialization
        public FoundryService(ILogger<FoundryService> logger,
                              FoundryProjectSettings foundryProjectSettings)
        {
            //initialization
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this._foundryProjectSettings = foundryProjectSettings ?? throw new ArgumentNullException(nameof(foundryProjectSettings));
        }
        #endregion
        #region Public Methods
        /// <summary>
        /// Gets an array of engineer bios from a Foundry workflow.
        /// </summary>
        public async Task<AgentResponse<EngineerBio[]>> ExecuteExpertiseFinderWorkflowAsync(string prompt, TokenCredential tokenCredential)
        {
            //initialization
            if (string.IsNullOrWhiteSpace(prompt))
            {
                //error
                this._logger.LogWarning("A blank prompt was requested.");
                return new AgentResponse<EngineerBio[]>(Array.Empty<EngineerBio>());
            }

            //get agent
            AIProjectClient foundryClient = this.GetFoundryClient(tokenCredential);
            Dictionary<string, EngineerBio> engineers = new Dictionary<string, EngineerBio>();
            AgentReference agentReference = new AgentReference(FSPKConstants.Workflows.ExpertiseWorkflow);

            //create conversation
            ProjectConversation conversation = await foundryClient.OpenAI.Conversations.CreateProjectConversationAsync();
            using (this._logger.BeginScope(new Dictionary<string, object>
            {
                //assemble dictionary
                { $"Workflow {nameof(conversation)}", conversation.Id }
            }))
            {
                //excute workflow
                ProjectResponsesClient responseClient = foundryClient.OpenAI.GetProjectResponsesClientForAgent(agentReference, conversation.Id);
                ClientResult<ResponseResult> response = await responseClient.CreateResponseAsync(prompt);
                int step = 0;

                //get all agent response messages from the workflow's conversation
                foreach (MessageResponseItem outputItem in response.Value.OutputItems.OfType<MessageResponseItem>())
                {
                    //ignore the response of the intermediate email-extractor agent
                    step++;
                    if (step == 2)
                        continue;

                    //get each piece of content
                    foreach (ResponseContentPart content in outputItem.Content)
                    {
                        //check content
                        if (string.IsNullOrWhiteSpace(content?.Text))
                        {
                            //missing content
                            this._logger.LogWarning($"An empty workflow reponse was received from {outputItem.Id}.");
                            continue;
                        }

                        //parse json (not needed for JSON agents, but keeping for safety)
                        string json = content.Text.TrimStart(FSPKConstants.Workflows.JSONDelimiter).TrimEnd(FSPKConstants.Workflows.JSONTerminator).ToString();

                        try
                        {
                            //deserialized bios
                            EngineerBiosWrapper engineerBios = JsonSerializer.Deserialize<EngineerBiosWrapper>(json);
                            foreach (EngineerBio engineer in engineerBios.Engineers ?? Array.Empty<EngineerBio>())
                            {
                                //check email
                                if (string.IsNullOrWhiteSpace(engineer?.Email))
                                {
                                    //missing email
                                    this._logger.LogWarning($"No email address was found for {engineer?.FullName ?? "N/A"}.");
                                    continue;
                                }

                                //build bios
                                if (!engineers.ContainsKey(engineer.Email))
                                {
                                    //the workflow returns the core bio from the first agent
                                    engineers.Add(engineer.Email, engineer);
                                }
                                else
                                {
                                    //the last agent enriches the bio with photo information
                                    engineers[engineer.Email].PhotoURL = engineer.PhotoURL;
                                    engineers[engineer.Email].PhotoDescription = engineer.PhotoDescription;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            //error
                            this._logger.LogError(ex, $"Error processing engineer bio from {json}.");
                        }
                    }
                }

                //return
                return new AgentResponse<EngineerBio[]>(engineers.Values.ToArray());
            }
        }

        /// <summary>
        /// Facilitates a conversation with a Foundry agent.
        /// </summary>
        public async Task<AgentResponse<string>> ConverseWithAgentAsync(ConversationPrompt prompt, FoundryCredential foundryCredential)
        {
            //initialization
            if (string.IsNullOrWhiteSpace(prompt.UserMessage))
            {
                //error
                this._logger.LogWarning("A blank agent prompt was detected.");
                return null;
            }

            //create a foundry client as the current user
            AgentRecord agent = null;
            StringBuilder answer = new StringBuilder();
            HashSet<string> annotations = new HashSet<string>();
            AIProjectClient foundryClient = this.GetFoundryClient(foundryCredential);

            try
            {
                //get agent                
                switch (prompt.Agent)
                {
                    //hr
                    case Agent.HR:
                        agent = await foundryClient.Agents.GetAgentAsync(FSPKConstants.Agents.HR);
                        break;

                    //invalid agent
                    default:
                        throw new InvalidOperationException($"{prompt.Agent} is not a supported agent.");
                }

                //check agent
                if (agent == null)
                    throw new Exception($"Could not acquire a reference to agent {prompt.Agent}.");

                //get conversation
                ProjectConversation conversation = null;
                if (string.IsNullOrWhiteSpace(prompt.ConversationId))
                {
                    //create conversation
                    conversation = await foundryClient.OpenAI.Conversations.CreateProjectConversationAsync();

                    //track conversation id
                    prompt.ConversationId = conversation.Id;
                    this._logger.LogInformation($"Started new {agent.Name} conversation {prompt.ConversationId}.");
                }
                else
                {
                    //resume conversation
                    conversation = await foundryClient.OpenAI.Conversations.GetProjectConversationAsync(prompt.ConversationId);
                    this._logger.LogInformation($"Resuming {agent.Name} conversation {prompt.ConversationId}.");
                }

                //converse with agent
                using (this._logger.BeginScope(new Dictionary<string, object>
                {
                    //assemble dictionary
                    { "Agent Id", agent.Id },
                    { "Agent Name", agent.Name },
                    { "Conversation", conversation.Id }
                }))
                {
                    //send the user's message to the agent
                    ProjectResponsesClient responseClient = foundryClient.OpenAI.GetProjectResponsesClientForAgent(agent, prompt.ConversationId);
                    ClientResult<ResponseResult> response = await responseClient.CreateResponseAsync(prompt.UserMessage);

                    //get agent responses
                    foreach (MessageResponseItem outputItem in response.Value.OutputItems.OfType<MessageResponseItem>())
                    {
                        //get each piece of content
                        foreach (ResponseContentPart content in outputItem.Content)
                        {
                            //check content
                            if (string.IsNullOrWhiteSpace(content?.Text))
                            {
                                //missing content
                                this._logger.LogWarning($"An empty agent reponse was received from output {outputItem.Id}.");
                                continue;
                            }
                            else
                            {
                                //capture answer
                                answer.AppendLine(content.Text);
                                this._logger.LogInformation($"Got agent response {content.Text}.");
                            }

                            //collect annotations
                            foreach (ResponseMessageAnnotation annotation in content.OutputTextAnnotations)
                            {
                                //since the data source is sharepoint, only consider URL citations
                                if (annotation.Kind == ResponseMessageAnnotationKind.UriCitation)
                                    annotations.Add(((UriCitationMessageAnnotation)annotation).Uri.ToString());
                                else
                                    this._logger.LogInformation($"Ignoring {annotation.Kind} annotation.");
                            }
                        }
                    }

                    //return
                    return new AgentResponse<string>(answer.ToString().Trim(), prompt.ConversationId, annotations);
                }
            }
            catch (Exception ex)
            {
                //error
                this._logger.LogError(ex, "Unknown agent error.");
                throw;
            }
        }

        /// <summary>
        /// Deploys an agent to foundry.
        /// </summary>
        public async Task<string> DeployAgentAsync(CreateAgentRequest request, TokenCredential tokenCredential)
        {
            //initialization
            if (string.IsNullOrWhiteSpace(request?.Name) || string.IsNullOrWhiteSpace(request?.Model))
            {
                //error
                string warning = "Agent name and model are required.";
                this._logger.LogWarning(warning);
                return warning;
            }

            try
            {
                //get clients
                AIProjectClient foundryClient = this.GetFoundryClient(tokenCredential);

                //check existing agent
                await foreach (AgentRecord existingAgent in foundryClient.Agents.GetAgentsAsync())
                    if (existingAgent.Name.Equals(request.Name, StringComparison.InvariantCultureIgnoreCase))
                    {
                        var def = (PromptAgentDefinition)existingAgent.Versions.Latest.Definition;

                        string toolChoise = def.ToolChoice.ToString();
                        foreach (var tool in def.Tools)
                        {
                            SharepointPreviewTool sp = (SharepointPreviewTool)tool.AsAgentTool();
                            
                        }


                        throw new InvalidOperationException($"Agent {request.Name} already exists.");
                    }

                //get model
                bool modelFound = false;
                await foreach (AIProjectDeployment deployment in foundryClient.Deployments.GetDeploymentsAsync())
                {
                    //check all deployed models by name
                    if (deployment.Name.Equals(request.Model, StringComparison.InvariantCultureIgnoreCase))
                    {
                        //model found
                        modelFound = true;
                        break;
                    }
                }

                //check model
                if (!modelFound)
                    throw new InvalidOperationException($"Model {request.Model} has not been deployed.");

                var connections = foundryClient.Connections.GetConnectionsAsync();
                await foreach (var connection in connections)
                {
                    if (connection.Type == ConnectionType.Custom)
                    {
                    }
                }


                SharepointGroundingToolParameters sharepointGroundingToolParameters = new SharepointGroundingToolParameters("connectionId");
                SharepointToolDefinition sharepointToolDefinition = new SharepointToolDefinition(sharepointGroundingToolParameters);

                PromptAgentDefinition agentDefinition = new PromptAgentDefinition(request.Model);
                agentDefinition.Instructions = request.Instructions;


                SharepointPreviewTool sharePointTool = new SharepointPreviewTool(new SharePointGroundingToolOptions()
                {
                    
                });

                var agent = await foundryClient.Agents.CreateAgentVersionAsync(request.Name, new AgentVersionCreationOptions(agentDefinition)
                {
                    Description = request.Description
                });
{

                };
                return string.Empty;

                //ClientResult<AgentVersion> agent = await client.Agents.CreateAgentVersionAsync(request.Name, options);
                //PipelineResponse response = agent.GetRawResponse();

                //if (response.IsError)
                //{
                //    throw new Exception($"Agent provisioning returned a {response.Status}: {response.Content}");
                //}
                //else
                //{
                //    return string.Empty;
                //}
            }
            catch (Exception ex)
            {
                //error
                string error = $"Failed to create agent {request.Name}.";
                this._logger.LogError(ex, error);
                return error;
            }
        }
        #endregion
        #region Private Methods
        /// <summary>
        /// Builds a foundry client with the given credential.
        /// </summary>
        private AIProjectClient GetFoundryClient(TokenCredential tokenCredential)
        {
            //return
            return new AIProjectClient(this._foundryProjectSettings.ProjectEndpoint, tokenCredential);
        }
        #endregion
    }
}