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
using ConnectionType = Azure.AI.Projects.ConnectionType;
using Microsoft.Graph.Drives.Item.Items.Item.Workbook.Names.Item.RangeNamespace.ColumnsBeforeWithCount;

#pragma warning disable AAIP001
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
        private readonly IKeyVaultService _keyVaultService;
        private readonly FoundryProjectSettings _foundryProjectSettings;

        #endregion
        #region Initialization
        public FoundryService(ILogger<FoundryService> logger,
                              IKeyVaultService keyVaultService,
                              FoundryProjectSettings foundryProjectSettings)
        {
            //initialization
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this._keyVaultService = keyVaultService ?? throw new ArgumentNullException(nameof(keyVaultService));
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

        /// <summary>
        /// Moves agents from one foundry project to another.
        /// </summary>
        public async Task<string> MigrateAgentsAsync(MigrateAgentsRequest migrateAgentsRequest, TokenCredential foundryCredential)
        {
            //initialization
            if (!Uri.TryCreate(migrateAgentsRequest.SourceProjectEndpoint, UriKind.Absolute, out Uri sourceFoundryProjectURI))
                throw new InvalidOperationException(nameof(migrateAgentsRequest.SourceProjectEndpoint));
            if (!Uri.TryCreate(migrateAgentsRequest.DestinationProjectEndpoint, UriKind.Absolute, out Uri destinationFoundryProjectURI))
                throw new InvalidOperationException(nameof(migrateAgentsRequest.DestinationProjectEndpoint));

            try
            {
                //create clients and get secrets
                ArmClient armClient = new ArmClient(foundryCredential);
                AIProjectClient sourceFoundryClient = new AIProjectClient(sourceFoundryProjectURI, foundryCredential);
                AIProjectClient destinationFoundryClient = new AIProjectClient(destinationFoundryProjectURI, foundryCredential);                

                //this parses a project connection name as the last segment of an azure resource id
                string destinationResourceGroupName = migrateAgentsRequest.DestinationResourceGroupName;
                string getConnectionName(string connectionId)
                {
                    //initialization
                    string[] connectionIdParts = connectionId.Split('/', StringSplitOptions.RemoveEmptyEntries);

                    //set destination resource group name if not provided
                    if (string.IsNullOrWhiteSpace(destinationResourceGroupName))
                        destinationResourceGroupName = connectionIdParts[3];

                    //return
                    return connectionIdParts.Last();
                }

                //this gets a foundry account name from a project uri
                string getFoundryAccountName(Uri uri)
                {
                    //return
                    return uri?.Host?.Split('.')?.FirstOrDefault();
                }

                //this gets a foundry account name from a project url
                string getFoundryProjectName(Uri uri)
                {
                    //return
                    return uri?.ToString()?.Split('/')?.LastOrDefault();
                }

                //get foundry metadata
                string sourceFoundryAccountName = getFoundryAccountName(sourceFoundryProjectURI);
                string sourceFoundryProjectName = getFoundryProjectName(sourceFoundryProjectURI);
                string destinationFoundryAccountName = getFoundryAccountName(destinationFoundryProjectURI);
                string destinationFoundryProjectName = getFoundryProjectName(destinationFoundryProjectURI);

                //prepare data structures
                List <MigratableAgent> sourceAgents = new List<MigratableAgent>();
                Dictionary<string, IToolDefintion> toolDefinitions = new Dictionary<string, IToolDefintion>();
                //Dictionary<string, string> destinationKeyValueSecrets = await this._keyVaultService.GetAllSecretsAsync(migrateAgentsRequest.DestinationKeyVaultURL);

                //get source foundry account
                List<CognitiveServicesAccountDeploymentData> sourceModels = new List<CognitiveServicesAccountDeploymentData>();
                CognitiveServicesAccountResource sourceAccount = armClient.GetCognitiveServicesAccountResource(CognitiveServicesAccountResource.CreateResourceIdentifier(this._foundryProjectSettings.SubscriptionId, migrateAgentsRequest.SourceResourceGroupName, sourceFoundryAccountName));

                //get source foundry models
                CognitiveServicesAccountDeploymentCollection sourceDeployments = sourceAccount.GetCognitiveServicesAccountDeployments();
                await foreach (CognitiveServicesAccountDeploymentResource deployment in sourceDeployments.GetAllAsync())
                    sourceModels.Add(deployment.Data);

                //get source app insights connections
                await foreach (AIProjectConnection sourceAppInisghtsConnection in sourceFoundryClient.Connections.GetConnectionsAsync(ConnectionType.ApplicationInsights))
                {
                    //check tool
                    if (!toolDefinitions.ContainsKey(sourceAppInisghtsConnection.Name))
                    {
                        //get api key
                        AIProjectConnection projectConnection = await sourceFoundryClient.Connections.GetConnectionAsync(sourceAppInisghtsConnection.Name, true);
                        AIProjectConnectionApiKeyCredential apiCredentials = (AIProjectConnectionApiKeyCredential)projectConnection.Credentials;

                        //collect app insights connections
                        toolDefinitions.Add(sourceAppInisghtsConnection.Name, new ProjectTelemetryDefinition(apiCredentials.ApiKey, projectConnection.Target));
                    }
                }

                //get source agents
                await foreach (AgentRecord agent in sourceFoundryClient.Agents.GetAgentsAsync())
                {
                    //get each agent's latest version to determine its type
                    AgentVersion latestVersion = agent.GetLatestVersion();
                    MigratableAgent migratableAgent = new MigratableAgent(latestVersion);
                    
                    //check agent definition
                    if (latestVersion.Definition is PromptAgentDefinition)
                    {
                        //get prompt agent tools
                        AgentTool[] tools = ((PromptAgentDefinition)migratableAgent.Definition).Tools.Select(t => t.AsAgentTool()).ToArray();
                        for (int t = 0; t < tools.Length; t++)
                        {
                            //get each tool
                            AgentTool tool = tools[t];
                            if (tool is SharepointPreviewTool)
                            {
                                //get sharepoint connection
                                ToolProjectConnection toolConnection = ((SharepointPreviewTool)tool).ToolOptions.ProjectConnections.FirstOrDefault();
                                if (toolConnection == null)
                                    this._logger.LogError($"Could not identify SharePoint tool ({t + 1}/{tools.Length}) for agent {agent.Name}.");

                                //assign tool to agent
                                AIProjectConnection projectConnection = await sourceFoundryClient.Connections.GetConnectionAsync(getConnectionName(toolConnection.ProjectConnectionId), true);
                                migratableAgent.ToolNames.Add(projectConnection.Name);

                                //check tool
                                if (!toolDefinitions.ContainsKey(projectConnection.Name))
                                {
                                    //create sharepoint tool definition
                                    string siteCollectionURL = ((AIProjectConnectionCustomCredential)projectConnection.Credentials).Keys.Values.First();
                                    toolDefinitions.Add(projectConnection.Name, new SharePointToolDefinition(siteCollectionURL));
                                }
                            }
                            else if (tool is AzureAISearchTool)
                            {
                                //create a tool definition for each index
                                foreach (AzureAISearchToolIndex index in ((AzureAISearchTool)tool).Options.Indexes)
                                {
                                    //get azure ai search connection
                                    AIProjectConnection projectConnection = await sourceFoundryClient.Connections.GetConnectionAsync(getConnectionName(index.ProjectConnectionId), true);
                                    string searchConnectionName = $"{projectConnection.Name}-{index.IndexName}";
                                    migratableAgent.ToolNames.Add(searchConnectionName);

                                    //check tool
                                    if (!toolDefinitions.ContainsKey(searchConnectionName))
                                    {
                                        //create azure ai search tool definition
                                        AIProjectConnectionApiKeyCredential apiCredentials = (AIProjectConnectionApiKeyCredential)projectConnection.Credentials;
                                        toolDefinitions.Add(searchConnectionName, new SearchToolDefinition(projectConnection.Metadata[FSPKConstants.Foundry.Tools.DisplayName],
                                                                                                           projectConnection.Metadata[FSPKConstants.Foundry.Tools.ResourceId],
                                                                                                           apiCredentials.ApiKey,
                                                                                                           index.IndexName,
                                                                                                           index.QueryType,
                                                                                                           index.Filter,
                                                                                                           index.TopK));
                                    }
                                }
                            }
                            else
                            {
                                //unsupported tool
                                this._logger.LogWarning($"Agent {agent.Name} has a tool of type {tool.GetType().Name} which is not currently supported in migrations.");
                            }
                        }
                    }
                    else if (latestVersion.Definition is WorkflowAgentDefinition)
                    {
                        //TODO
                        WorkflowAgentDefinition definition = (WorkflowAgentDefinition)latestVersion.Definition;
                        
                    }                   
                    else
                    {
                        //unsupported
                        throw new Exception($"Agent {agent.Name} has an unsupported definition of type {latestVersion.Definition.GetType().Name}.");
                    }

                    //collect agents
                    sourceAgents.Add(migratableAgent);
                }

                //get destination foundry project
                CognitiveServicesProjectResource destinationProject = armClient.GetCognitiveServicesProjectResource(CognitiveServicesProjectResource.CreateResourceIdentifier(this._foundryProjectSettings.SubscriptionId, destinationResourceGroupName, destinationFoundryAccountName, destinationFoundryProjectName));
                CognitiveServicesProjectConnectionCollection destinationProjectConnections = destinationProject.GetCognitiveServicesProjectConnections();

                //get destination connections
                List<CognitiveServicesProjectConnectionResource> destinationConnections = new List<CognitiveServicesProjectConnectionResource>();
                await foreach (CognitiveServicesProjectConnectionResource connection in destinationProjectConnections.GetAllAsync())
                    destinationConnections.Add(connection);

                //process destination connctions
                foreach (CognitiveServicesProjectConnectionResource connection in destinationConnections)
                {
                    //check tool
                    if (toolDefinitions.ContainsKey(connection.Data.Name))
                    {
                        //tool already exists
                        if (migrateAgentsRequest.ForceChanges)
                        {
                            //delete
                            await connection.DeleteAsync(WaitUntil.Completed);
                            this._logger.LogInformation($"Deleted existing destination connection {connection.Data.Name} since force changes is enabled for this migration.");
                        }
                        else
                        {
                            //duplicate
                            this._logger.LogWarning($"Destination connection {connection.Data.Name} already exists, and force changes is disabled for this migration.");
                            continue;
                        }
                    }
                }

                //create destination connections
                foreach (string toolName in toolDefinitions.Keys)
                {
                    //create connection
                    IToolDefintion tool = toolDefinitions[toolName];
                    CognitiveServicesConnectionData connection = tool.CreateConnection();
                    ArmOperation<CognitiveServicesProjectConnectionResource> result = await destinationProjectConnections.CreateOrUpdateAsync(WaitUntil.Completed, toolName, connection);

                    //check result
                    if (!result.HasValue)
                        this._logger.LogError($"Unable to create connection {toolName}: {result.GetRawResponse().Content.ToString()}");
                }

                //get destination foundry account
                CognitiveServicesAccountResource destinationAccount = armClient.GetCognitiveServicesAccountResource(CognitiveServicesAccountResource.CreateResourceIdentifier(this._foundryProjectSettings.SubscriptionId, destinationResourceGroupName, destinationFoundryAccountName));
                CognitiveServicesAccountDeploymentCollection destinationDeployments = destinationAccount.GetCognitiveServicesAccountDeployments();
                Dictionary<string, AzureAISearchToolIndex> destinationIndexes = new Dictionary<string, AzureAISearchToolIndex>();
                Dictionary<string, AgentVersion> exitingDestinationAgents = new Dictionary<string, AgentVersion>();

                //get destination deployments
                Dictionary<string, CognitiveServicesAccountDeploymentResource> destinationModels = new Dictionary<string, CognitiveServicesAccountDeploymentResource>();
                await foreach (CognitiveServicesAccountDeploymentResource destinationModel in destinationDeployments.GetAllAsync())
                    destinationModels.Add(destinationModel.Data.Name, destinationModel);

                //deploy destination models
                foreach (CognitiveServicesAccountDeploymentData sourceModel in sourceModels)
                {
                    //check model
                    if (destinationModels.ContainsKey(sourceModel.Name))
                    {
                        //model already exists
                        if (migrateAgentsRequest.ForceChanges)
                        {
                            //delete
                            await destinationModels[sourceModel.Name].DeleteAsync(WaitUntil.Completed);
                            this._logger.LogInformation($"Deleted existing destination model {sourceModel.Name} since force changes is enabled for this migration.");
                        }
                        else
                        {
                            //duplicate
                            this._logger.LogWarning($"Destination model {sourceModel.Name} already exists, and force changes is disabled for this migration.");
                            continue;
                        }
                    }

                    try
                    {
                        //deploy model
                        ArmOperation<CognitiveServicesAccountDeploymentResource> modelDeploymentResult = await destinationDeployments.CreateOrUpdateAsync(WaitUntil.Completed, sourceModel.Name, new CognitiveServicesAccountDeploymentData()
                        {
                            //create model
                            Sku = sourceModel.Sku,
                            Properties = sourceModel.Properties
                        });
                    }
                    catch (Exception ex)
                    {
                        //error
                        this._logger.LogError(ex, $"Failed to deploy destination model {sourceModel.Name}.");
                    }
                }

                //get destination agents               
                await foreach (AgentRecord agent in destinationFoundryClient.Agents.GetAgentsAsync())
                    exitingDestinationAgents.Add(agent.Name, agent.GetLatestVersion());

                //create destination agents
                foreach (MigratableAgent agent in sourceAgents)
                {
                    //check agent
                    if (exitingDestinationAgents.ContainsKey(agent.Name))
                    {
                        //agent exists
                        if (migrateAgentsRequest.ForceChanges)
                        {
                            //delete
                            await destinationFoundryClient.Agents.DeleteAgentAsync(agent.Name);
                            this._logger.LogWarning($"Deleted existing destination agent {agent.Name} since force changes is enabled for this migration.");
                        }
                        else
                        {
                            //duplicate
                            this._logger.LogWarning($"Destination agent {agent.Name} already exists, and force changes is disabled for this migration.");
                            continue;
                        }
                    }

                    //get agent definition
                    if (agent.Definition is PromptAgentDefinition)
                    {
                        //create agent definition
                        PromptAgentDefinition sourceAgentDefinition = (PromptAgentDefinition)agent.Definition;
                        PromptAgentDefinition destinationAgentDefinition = new PromptAgentDefinition(sourceAgentDefinition.Model);

                        //hydrate agent definition
                        destinationAgentDefinition.TopP = sourceAgentDefinition.TopP;
                        destinationAgentDefinition.ToolChoice = sourceAgentDefinition.ToolChoice;
                        destinationAgentDefinition.Temperature = sourceAgentDefinition.Temperature;
                        destinationAgentDefinition.TextOptions = sourceAgentDefinition.TextOptions;
                        destinationAgentDefinition.Instructions = sourceAgentDefinition.Instructions;
                        destinationAgentDefinition.ReasoningOptions = sourceAgentDefinition.ReasoningOptions;
                        destinationAgentDefinition.ContentFilterConfiguration = sourceAgentDefinition.ContentFilterConfiguration;

                        //add structured inputs
                        foreach (string structuredInputKey in sourceAgentDefinition.StructuredInputs?.Keys ?? new List<string>())
                            destinationAgentDefinition.StructuredInputs.Add(structuredInputKey, sourceAgentDefinition.StructuredInputs[structuredInputKey]);

                        //add tools
                        foreach (string toolName in agent.ToolNames)
                        {
                            //get earch tool
                            IToolDefintion tool = toolDefinitions[toolName];
                            if (tool is SearchToolDefinition)
                            {
                                //create search tool
                                SearchToolDefinition searchTool = (SearchToolDefinition)tool;
                                if (!destinationIndexes.ContainsKey(searchTool.IndexName))
                                {
                                    //create search index connection
                                    AIProjectConnection destinationSearchConnection = await destinationFoundryClient.Connections.GetConnectionAsync(toolName);
                                    destinationIndexes.Add(searchTool.IndexName, new AzureAISearchToolIndex()
                                    {
                                        //assemble object
                                        TopK = searchTool.TopK,
                                        Filter = searchTool.Filter,
                                        IndexName = searchTool.IndexName,
                                        QueryType = searchTool.QueryType,
                                        ProjectConnectionId = destinationSearchConnection.Id
                                    });
                                }

                                //add search tool
                                destinationAgentDefinition.Tools.Add(new AzureAISearchTool(new AzureAISearchToolOptions([destinationIndexes[searchTool.IndexName]])));
                            }
                            else if (tool is SharePointToolDefinition)
                            {
                                //create sharepoint tool
                                SharePointToolDefinition sharePointTool = (SharePointToolDefinition)tool;
                                SharePointGroundingToolOptions sharePointToolParameters = new SharePointGroundingToolOptions();
                                AIProjectConnection destinationSharePointConnection = await destinationFoundryClient.Connections.GetConnectionAsync(toolName);

                                //add sharepoint tool
                                sharePointToolParameters.ProjectConnections.Add(new ToolProjectConnection(destinationSharePointConnection.Id));
                                destinationAgentDefinition.Tools.Add(new SharepointPreviewTool(sharePointToolParameters));
                            }
                            else
                            {
                                //unsupported tool
                                this._logger.LogError($"Unable to add tool {toolName} to agent {agent}: {tool.GetType().Name} is unsupported.");
                                continue;
                            }
                        }

                        //create agent
                        ClientResult<AgentVersion> result = await destinationFoundryClient.Agents.CreateAgentVersionAsync(agent.Name, new AgentVersionCreationOptions(destinationAgentDefinition)
                        {
                            //assemble object
                            Description = agent.Description
                        });

                        //check result
                        if (result?.Value == null)
                            this._logger.LogError($"Unable to create destination agent {agent}: {result.GetRawResponse().Content.ToString()}");
                        else
                            this._logger.LogInformation($"Successfully migrated agent {agent}.");
                    }
                    else if (agent.Definition is WorkflowAgentDefinition)
                    {
                    }
                }

                //return
                return string.Empty;
            }
            catch (Exception ex)
            {
                //error
                this._logger.LogError(ex, $"Agent migration {migrateAgentsRequest} failed.");
                return ex.Message;
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