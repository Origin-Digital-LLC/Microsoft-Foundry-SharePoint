using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.ClientModel;
using System.Threading.Tasks;
using System.Collections.Generic;

using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.AI.Projects;
using Azure.ResourceManager;
using Azure.AI.Projects.Agents;
using Azure.AI.Extensions.OpenAI;
using Azure.ResourceManager.CognitiveServices;
using Azure.ResourceManager.CognitiveServices.Models;

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
            AgentReference agent = null;
            string agentId = string.Empty;
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
                        //get agent
                        AgentRecord agentRecord = await foundryClient.Agents.GetAgentAsync(FSPKConstants.Agents.HR);
                        if (agentRecord == null)
                            throw new Exception($"Agent {FSPKConstants.Agents.HR} was not found.");

                        //get agent reference
                        agent = new AgentReference(agentRecord.Name);
                        agentId = agentRecord.Id;
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
                    { "Agent Id", agentId },
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

        public async Task<string> MigrateAgentsAsync(string sourceFoundryProjectURL, string destinationFoundryProjectURL, IToolDefintiion[] destinationTools)
        {
            //initialization
            if (!Uri.TryCreate(sourceFoundryProjectURL, UriKind.Absolute, out Uri sourceFroundryProjectURI))
                throw new InvalidOperationException(nameof(sourceFoundryProjectURL));
            if (!Uri.TryCreate(destinationFoundryProjectURL, UriKind.Absolute, out Uri destinationFoundryProjectURI))
                throw new InvalidOperationException(nameof(destinationFoundryProjectURL));

            try
            {
                DefaultAzureCredential credential = new DefaultAzureCredential();

                //create clients
                ArmClient armClient = new ArmClient(credential);
                AIProjectClient sourceClient = new AIProjectClient(sourceFroundryProjectURI, credential);
                AIProjectClient destinationClient = new AIProjectClient(destinationFoundryProjectURI, credential);

                //return
                return string.Empty;
            }
            catch (Exception ex)
            {
                //error
                this._logger.LogError(ex, $"Failed to migration agents from {sourceFoundryProjectURL} to {destinationFoundryProjectURL}");
                return ex.Message;
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

                // authenticate your client
                ArmClient client = new ArmClient(new DefaultAzureCredential());
                AIProjectClient foundryClient = new AIProjectClient(new Uri("https://test-foundry-02.services.ai.azure.com/api/projects/test-project"), new DefaultAzureCredential());
                //AIProjectClient foundryClient = new AIProjectClient(new Uri("https://cmfug-20260303-foundry01.services.ai.azure.com/api/projects/cmfug-agent-pool"), new DefaultAzureCredential());

                string subscriptionId = "af61721f-e1df-4570-9d4d-b0cf4aa66317";
                string resourceGroupName = "origin-nexus-lab";
                string accountName = "test-foundry-02";
                string projectName = "test-project";
                string searchConnectionName = "Search";
                string searchIndexName = "sharepoint-foundry";
                string appInsightsConnectionName = "Telemetry";
                string sharePointConnectionName = "SharePoint";

                CognitiveServicesProjectResource project = client.GetCognitiveServicesProjectResource(CognitiveServicesProjectResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, accountName, projectName));
                CognitiveServicesProjectConnectionCollection connections = project.GetCognitiveServicesProjectConnections();

                //--app insights--
                //CognitiveServicesConnectionData appInsightsConnection = new CognitiveServicesConnectionData(new ApiKeyAuthConnectionProperties
                //{
                //    IsSharedToAll = true,
                //    Category = "AppInsights",
                //    Target = "/subscriptions/af61721f-e1df-4570-9d4d-b0cf4aa66317/resourceGroups/ensemble-dev/providers/Microsoft.Insights/components/ensemble-dev-telemetry01",
                //    CredentialsKey = "InstrumentationKey=c6223306-abcc-4a02-b9cc-01138893d79e;IngestionEndpoint=https://northcentralus-0.in.applicationinsights.azure.com/;LiveEndpoint=https://northcentralus.livediagnostics.monitor.azure.com/;ApplicationId=b83bd50b-726c-40e7-897b-277d8dde1274"
                //});

                //ArmOperation<CognitiveServicesProjectConnectionResource> result = await connections.CreateOrUpdateAsync(WaitUntil.Completed, appInsightsConnectionName, appInsightsConnection);

                //--sp--
                //CustomKeysConnectionProperties sharePointConnectionProperties = new CustomKeysConnectionProperties
                //{
                //    Target = "-",
                //    IsSharedToAll = true,
                //    Category = CognitiveServicesConnectionCategory.CustomKeys
                //};

                //sharePointConnectionProperties.Metadata.Add("type", "sharepoint_grounding_preview");
                //sharePointConnectionProperties.CredentialsKeys.Add("site_url", "https://netorg14925960.sharepoint.com/teams/cmfug/hr");

                //CognitiveServicesConnectionData sharePointConnection = new CognitiveServicesConnectionData(sharePointConnectionProperties);
                //ArmOperation<CognitiveServicesProjectConnectionResource> result = await connections.CreateOrUpdateAsync(WaitUntil.Completed, sharePointConnectionName, sharePointConnection);

                //--search--
                ApiKeyAuthConnectionProperties searchConnectionProperties = new ApiKeyAuthConnectionProperties
                {
                    IsSharedToAll = true,
                    Category = CognitiveServicesConnectionCategory.CognitiveSearch,
                    Target = "https://cmfug-20260303-search01.search.windows.net/",
                    CredentialsKey = "wAyMm9vQzOUrOi7050aM1Vz5lVnCv2YT3afjgHKR7uAzSeBjt0tY"
                };

                searchConnectionProperties.Metadata.Add("ApiType", "Azure");
                searchConnectionProperties.Metadata.Add("type", "azure_ai_search");
                searchConnectionProperties.Metadata.Add("displayName", "cmfug-20260303-search01");
                searchConnectionProperties.Metadata.Add("ResourceId", "/subscriptions/af61721f-e1df-4570-9d4d-b0cf4aa66317/resourceGroups/cmfug-20260303/providers/Microsoft.Search/searchServices/cmfug-20260303-search01");

                CognitiveServicesConnectionData searchConnection = new CognitiveServicesConnectionData(searchConnectionProperties);
                ArmOperation<CognitiveServicesProjectConnectionResource> result = await connections.CreateOrUpdateAsync(WaitUntil.Completed, searchConnectionName, searchConnection);

                if (result.HasValue)
                {
                  
                }


                //get clients
                //AIProjectClient foundryClient = this.GetFoundryClient(tokenCredential);

                AIProjectConnection aiSearchConnection = await foundryClient.Connections.GetConnectionAsync(searchConnectionName);

                AzureAISearchToolIndex index = new AzureAISearchToolIndex()
                {
                    TopK = 50,
                    IndexName = searchIndexName,
                    ProjectConnectionId = aiSearchConnection.Id,
                    QueryType = AzureAISearchQueryType.VectorSemanticHybrid
                };


                //var indexResult = await foundryClient.Indexes.CreateOrUpdateAsync(searchIndexName, "1", new AzureAISearchIndex(searchConnectionName, searchIndexName));


                // Create the agent definition with the Azure AI Search tool.
                PromptAgentDefinition agentDefinition = new PromptAgentDefinition("gpt-4.1-mini")
                {
                    Instructions = "You are an HR expert who can summarize engineer bios. All records in the search index pertaining to the same person have the same \"FullName\" values and vectors, as well as matching \"Email\" field values that uniquely identify an engineer.\r\n\r\nPlease provide a summary of each engineer including FullName, Email, TechnicalSkills, SolutionSkills, and Experience. \r\n\r\nEnsure FullName is also proper cased.",
                    Tools = { new AzureAISearchTool(new AzureAISearchToolOptions(indexes: [index])) },
                    ToolChoice = BinaryData.FromString("\"required\""),
                    TextOptions = new ResponseTextOptions()
                    {
                        TextFormat = ResponseTextFormat.CreateJsonSchemaFormat("Bio", BinaryData.FromString(@"
                        {
                            ""type"": ""object"",
                            ""additionalProperties"": false,
                            ""required"": [
                                ""Engineers""
                            ],
                            ""properties"": {
                                ""Engineers"": {
                                    ""type"": ""array"",
                                    ""additionalProperties"": false,
                                    ""description"": ""These are the engineers return from a query."",
                                    ""items"": {
                                        ""type"": ""object"",
                                        ""additionalProperties"": false,
                                        ""required"": [
                                            ""FullName"",
                                            ""Email"",
                                            ""TechnicalSkills"",
                                            ""SolutionSkills"",
                                            ""Experience""
                                        ],
                                        ""properties"": {
                                            ""FullName"": {
                                                ""type"": ""string"",
                                                ""description"": ""The engineer's full name.""
                                            },
                                            ""Email"": {
                                                ""type"": ""string"",
                                                ""description"": ""The engineer's email address.""
                                            },
                                            ""TechnicalSkills"": {
                                                ""type"": ""array"",
                                                ""items"": {
                                                ""type"": ""string""
                                                },
                                                ""description"": ""A list of the engineer's technical skills.""
                                            },
                                            ""SolutionSkills"": {
                                                ""type"": ""array"",
                                                ""items"": {
                                                ""type"": ""string""
                                                },
                                                ""description"": ""A list of the engineer's solution skills.""
                                            },
                                            ""Experience"": {
                                                ""type"": ""string"",
                                                ""description"": ""A description of the engineer's experience.""
                                            }
                                        }
                                    }
                                }
                            }
                        }"), "This is the structured output for an engineer's bio.", true)
                    }
                };

                var agent = await foundryClient.Agents.CreateAgentVersionAsync("search3", new AgentVersionCreationOptions(agentDefinition)
                {
                    Description = request.Description
                });


                return string.Empty;


                ////check existing agent
                //await foreach (AgentRecord existingAgent in foundryClient.Agents.GetAgentsAsync())
                //{
                //    if (existingAgent.Name.Equals(request.Name, StringComparison.InvariantCultureIgnoreCase))
                //    {
                //        var def = (PromptAgentDefinition)existingAgent.Versions.Latest.Definition;

                //        string toolChoise = def.ToolChoice.ToString();
                //        foreach (var tool in def.Tools)
                //        {
                //            SharepointPreviewTool sp = (SharepointPreviewTool)tool.AsAgentTool();

                //        }


                //        throw new InvalidOperationException($"Agent {request.Name} already exists.");
                //    }
                //}

                ////get model
                //bool modelFound = false;
                //await foreach (AIProjectDeployment deployment in foundryClient.Deployments.GetDeploymentsAsync())
                //{
                //    //check all deployed models by name
                //    if (deployment.Name.Equals(request.Model, StringComparison.InvariantCultureIgnoreCase))
                //    {
                //        //model found
                //        modelFound = true;
                //        break;
                //    }
                //}

                ////check model
                //if (!modelFound)
                //    throw new InvalidOperationException($"Model {request.Model} has not been deployed.");


                //var connections = foundryClient.Connections.GetConnectionsAsync();
                //await foreach (var connection in connections)
                //{
                //    if (connection.Type == ConnectionType.Custom)
                //    {
                //    }
                //}


                //SharepointGroundingToolParameters sharepointGroundingToolParameters = new SharepointGroundingToolParameters("connectionId");
                //SharepointToolDefinition sharepointToolDefinition = new SharepointToolDefinition(sharepointGroundingToolParameters);

                //PromptAgentDefinition agentDefinition = new PromptAgentDefinition(request.Model);
                //agentDefinition.Instructions = request.Instructions;


                //SharepointPreviewTool sharePointTool = new SharepointPreviewTool(new SharePointGroundingToolOptions()
                //{

                //});

                //var agent = await foundryClient.Agents.CreateAgentVersionAsync(request.Name, new AgentVersionCreationOptions(agentDefinition)
                //{
                //    Description = request.Description
                //});


                //return string.Empty;


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