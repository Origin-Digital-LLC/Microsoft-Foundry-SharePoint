using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Reflection;
using System.ClientModel;
using System.Threading.Tasks;
using System.Collections.Generic;

using Azure;
using Azure.Core;
using Azure.AI.Projects;
using Azure.ResourceManager;
using Azure.AI.Projects.Agents;
using Azure.AI.Extensions.OpenAI;
using Azure.ResourceManager.CognitiveServices;
using ConnectionType = Azure.AI.Projects.ConnectionType;

using Microsoft.Extensions.Logging;

using FoundrySharePointKnowledge.Common;
using FoundrySharePointKnowledge.Domain.Foundry;
using FoundrySharePointKnowledge.Domain.Settings;
using FoundrySharePointKnowledge.Domain.Contracts;

using OpenAI.Responses;

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

            //get clients
            ProjectOpenAIClient openAIClient = foundryClient.GetProjectOpenAIClient();
            ProjectConversationsClient conversationsClient = openAIClient.GetProjectConversationsClient();

            //create conversation
            ProjectConversation conversation = await conversationsClient.CreateProjectConversationAsync();
            using (this._logger.BeginScope(new Dictionary<string, object>
            {
                //assemble dictionary
                { $"Workflow {nameof(conversation)}", conversation.Id }
            }))
            {
                //excute workflow
                ProjectResponsesClient responseClient = openAIClient.GetProjectResponsesClientForAgent(agentReference, conversation.Id);
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

            //get openAI clients
            ProjectOpenAIClient openAIClient = foundryClient.GetProjectOpenAIClient();
            AgentAdministrationClient agentsClient = foundryClient.GetProjectAgentsClient();
            ProjectConversationsClient conversationsClient = openAIClient.GetProjectConversationsClient();

            try
            {
                //get agent                
                switch (prompt.Agent)
                {
                    //hr
                    case Agent.HR:

                        //get agent
                        ProjectsAgentRecord agentRecord = await agentsClient.GetAgentAsync(FSPKConstants.Agents.HR);
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
                    conversation = await conversationsClient.CreateProjectConversationAsync();

                    //track conversation id
                    prompt.ConversationId = conversation.Id;
                    this._logger.LogInformation($"Started new {agent.Name} conversation {prompt.ConversationId}.");
                }
                else
                {
                    //resume conversation
                    conversation = await conversationsClient.GetProjectConversationAsync(prompt.ConversationId);
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
                    ProjectResponsesClient responseClient = openAIClient.GetProjectResponsesClientForAgent(agent, prompt.ConversationId);
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
        /// Promotes agents from one Foundry project to another.
        /// </summary>
        public async Task<MigrateAgentsResponse> PromoteAgentsAsync(MigrateAgentsRequest migrateAgentsRequest, TokenCredential foundryCredential)
        {
            //initialization
            MigrateAgentsResponse result = new MigrateAgentsResponse();
            if (!Uri.TryCreate(migrateAgentsRequest.SourceProjectEndpoint, UriKind.Absolute, out Uri sourceFoundryProjectURI))
                throw new InvalidOperationException(nameof(migrateAgentsRequest.SourceProjectEndpoint));
            if (!Uri.TryCreate(migrateAgentsRequest.DestinationProjectEndpoint, UriKind.Absolute, out Uri destinationFoundryProjectURI))
                throw new InvalidOperationException(nameof(migrateAgentsRequest.DestinationProjectEndpoint));

            //this logs a warning
            void collectWarning(string warning)
            {
                //initialization
                result.Warnings.Add(warning);

                //return
                this._logger.LogWarning(warning);
            }

            //this logs an error
            void collectError(string error, Exception exception = null)
            {
                //initialization
                result.Errors.Add(error);

                //return
                if (exception == null)
                    this._logger.LogError(error);
                else
                    this._logger.LogError(exception, error);
            }

            try
            {
                //create ARM and foundry clients
                ArmClient armClient = new ArmClient(foundryCredential);
                AIProjectClient sourceFoundryClient = new AIProjectClient(sourceFoundryProjectURI, foundryCredential);
                AIProjectClient destinationFoundryClient = new AIProjectClient(destinationFoundryProjectURI, foundryCredential);

                //create agent clients
                AgentAdministrationClient sourceAgentsClient = sourceFoundryClient.GetProjectAgentsClient();
                AgentAdministrationClient destinationAgentsClient = destinationFoundryClient.GetProjectAgentsClient();

                //this parses a project connection name as the last segment of an azure resource id
                string destinationResourceGroupName = migrateAgentsRequest.DestinationResourceGroupName;
                string getConnectionName(string connectionId)
                {
                    //initialization
                    string[] connectionIdParts = connectionId.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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
                    return uri?.Host?.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)?.FirstOrDefault();
                }

                //this gets a foundry account name from a project url
                string getFoundryProjectName(Uri uri)
                {
                    //return
                    return uri?.ToString()?.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)?.LastOrDefault();
                }

                //this gets a resource name from a resource id
                string getResourceName(string resourceId)
                {
                    //retrn
                    return resourceId?.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)?.LastOrDefault();
                }

                //get foundry metadata
                string sourceFoundryAccountName = getFoundryAccountName(sourceFoundryProjectURI);
                string sourceFoundryProjectName = getFoundryProjectName(sourceFoundryProjectURI);
                string destinationFoundryAccountName = getFoundryAccountName(destinationFoundryProjectURI);
                string destinationFoundryProjectName = getFoundryProjectName(destinationFoundryProjectURI);

                //prepare data structures
                List<MigratableAgent> sourceAgents = new List<MigratableAgent>();
                Dictionary<string, string> workflows = new Dictionary<string, string>();
                Dictionary<string, string> connectionLookup = new Dictionary<string, string>();
                Dictionary<string, string> reverseConnectionLookup = new Dictionary<string, string>();
                Dictionary<string, string> destinationKeyValueSecrets = new Dictionary<string, string>();
                Dictionary<string, IToolDefintion> toolDefinitions = new Dictionary<string, IToolDefintion>();

                //check key vault url and load destination secrets if it is specified
                if (!string.IsNullOrWhiteSpace(migrateAgentsRequest.DestinationKeyVaultURL))
                    destinationKeyValueSecrets = await this._keyVaultService.GetAllSecretsAsync(migrateAgentsRequest.DestinationKeyVaultURL);

                //get source foundry account
                List<CognitiveServicesAccountDeploymentData> sourceModels = new List<CognitiveServicesAccountDeploymentData>();
                CognitiveServicesAccountResource sourceAccount = armClient.GetCognitiveServicesAccountResource(CognitiveServicesAccountResource.CreateResourceIdentifier(this._foundryProjectSettings.SubscriptionId, migrateAgentsRequest.SourceResourceGroupName, sourceFoundryAccountName));

                //get source foundry models
                CognitiveServicesAccountDeploymentCollection sourceDeployments = sourceAccount.GetCognitiveServicesAccountDeployments();
                await foreach (CognitiveServicesAccountDeploymentResource sourceDeployment in sourceDeployments.GetAllAsync())
                    sourceModels.Add(sourceDeployment.Data);

                //get source app insights connections
                await foreach (AIProjectConnection sourceAppInsightsConnection in sourceFoundryClient.Connections.GetConnectionsAsync(ConnectionType.ApplicationInsights))
                {
                    //check tool
                    if (!toolDefinitions.ContainsKey(sourceAppInsightsConnection.Name))
                    {
                        //get api key
                        AIProjectConnection sourceProjectConnection = await sourceFoundryClient.Connections.GetConnectionAsync(sourceAppInsightsConnection.Name, true);
                        if (!destinationKeyValueSecrets.TryGetValue(FSPKConstants.Settings.KeyVault.ApplicationInsights.ConnectionString, out string destinationAppInisghtsConnectionString))
                        {
                            //use source connection string
                            collectWarning($"Application Insights connection {sourceProjectConnection.Name} will use the source connection string since {FSPKConstants.Settings.KeyVault.ApplicationInsights.ConnectionString} wasn't found in the destination Key Vault ({migrateAgentsRequest.DestinationKeyVaultURL}).");
                            AIProjectConnectionApiKeyCredential apiCredentials = (AIProjectConnectionApiKeyCredential)sourceProjectConnection.Credentials;
                            destinationAppInisghtsConnectionString = apiCredentials.ApiKey;
                        }

                        //get resource id
                        if (!destinationKeyValueSecrets.TryGetValue(FSPKConstants.Settings.KeyVault.ApplicationInsights.ResourceId, out string destinationAppInsightsResourceId))
                        {
                            //use source resource id
                            collectWarning($"Application Insights connection {sourceProjectConnection.Name} will use the source target resource since {FSPKConstants.Settings.KeyVault.ApplicationInsights.ResourceId} wasn't found in the destination Key Vault ({migrateAgentsRequest.DestinationKeyVaultURL}).");
                            destinationAppInsightsResourceId = sourceProjectConnection.Target;
                        }

                        //get connection name
                        string destinationAppInsightsConnectionName = getResourceName(destinationAppInsightsResourceId);

                        //collect app insights connection
                        connectionLookup.Add(destinationAppInsightsConnectionName, sourceAppInsightsConnection.Name);
                        reverseConnectionLookup.Add(sourceAppInsightsConnection.Name, destinationAppInsightsConnectionName);
                        toolDefinitions.Add(sourceAppInsightsConnection.Name, new ProjectTelemetryDefinition(destinationAppInisghtsConnectionString, destinationAppInsightsResourceId));
                    }
                }

                //get source agents
                await foreach (ProjectsAgentRecord sourceAgent in sourceAgentsClient.GetAgentsAsync())
                {
                    //get each agent's latest version to determine its type
                    ProjectsAgentVersion latestVersion = sourceAgent.GetLatestVersion();
                    MigratableAgent migratableAgent = new MigratableAgent(latestVersion);

                    //check agent definition
                    if (latestVersion.Definition is DeclarativeAgentDefinition)
                    {
                        //set agent model
                        DeclarativeAgentDefinition definition = (DeclarativeAgentDefinition)migratableAgent.Definition;
                        migratableAgent.ModelName = definition.Model;

                        //get declaritive agent tools
                        ProjectsAgentTool[] tools = definition.Tools.Select(t => t.AsAgentTool()).ToArray();
                        foreach (ProjectsAgentTool tool in tools)
                        {
                            //get each tool
                            if (tool is SharepointPreviewTool)
                            {
                                //get sharepoint connection
                                ToolProjectConnection toolConnection = ((SharepointPreviewTool)tool).ToolOptions.ProjectConnections.FirstOrDefault();

                                //assign tool to agent
                                AIProjectConnection projectConnection = await sourceFoundryClient.Connections.GetConnectionAsync(getConnectionName(toolConnection.ProjectConnectionId), true);
                                migratableAgent.ToolNames.Add(projectConnection.Name);

                                //check tool
                                if (!toolDefinitions.ContainsKey(projectConnection.Name))
                                {
                                    //get site collectoin url
                                    if (!destinationKeyValueSecrets.TryGetValue(FSPKConstants.Settings.KeyVault.SharePoint.SiteCollectionURL, out string sourceSiteCollectionURL))
                                    {
                                        //use source sharepoint site collection url
                                        sourceSiteCollectionURL = ((AIProjectConnectionCustomCredential)projectConnection.Credentials).Keys.Values.First();
                                        collectWarning($"SharePoint connection {projectConnection.Name} will use the source site collection URL since {FSPKConstants.Settings.KeyVault.SharePoint.SiteCollectionURL} wasn't found in the destination Key Vault ({migrateAgentsRequest.DestinationKeyVaultURL}).");
                                    }

                                    //create sharepoint tool definition
                                    toolDefinitions.Add(projectConnection.Name, new SharePointToolDefinition(sourceSiteCollectionURL));
                                }
                            }
                            else if (tool is AzureAISearchTool)
                            {
                                //create a tool definition for each index
                                foreach (AzureAISearchToolIndex index in ((AzureAISearchTool)tool).Options.Indexes)
                                {
                                    //get azure ai search connection
                                    AIProjectConnection projectConnection = await sourceFoundryClient.Connections.GetConnectionAsync(getConnectionName(index.ProjectConnectionId), true);
                                    string sourceSearchConnectionName = $"{projectConnection.Name}-{index.IndexName}";
                                    migratableAgent.ToolNames.Add(sourceSearchConnectionName);

                                    //check tool
                                    if (!toolDefinitions.ContainsKey(sourceSearchConnectionName))
                                    {
                                        //get api key
                                        if (!destinationKeyValueSecrets.TryGetValue(FSPKConstants.Settings.KeyVault.Search.Key, out string azureAISearchAdminKey))
                                        {
                                            //use source azure ai search admin key
                                            collectWarning($"Azure AI Search index connection {sourceSearchConnectionName} will use the source admin key since {FSPKConstants.Settings.KeyVault.Search.Key} wasn't found in the destination Key Vault ({migrateAgentsRequest.DestinationKeyVaultURL}).");
                                            AIProjectConnectionApiKeyCredential apiCredentials = (AIProjectConnectionApiKeyCredential)projectConnection.Credentials;
                                            azureAISearchAdminKey = apiCredentials.ApiKey;
                                        }

                                        //get resource id
                                        if (!destinationKeyValueSecrets.TryGetValue(FSPKConstants.Settings.KeyVault.Search.ResourceId, out string azureAISearchResourceId))
                                        {
                                            //use source azure ai search resource id
                                            collectWarning($"Azure AI Search index connection {sourceSearchConnectionName} will use the source resource id since {FSPKConstants.Settings.KeyVault.Search.ResourceId} wasn't found in the destination Key Vault ({migrateAgentsRequest.DestinationKeyVaultURL}).");
                                            azureAISearchResourceId = projectConnection.Metadata[FSPKConstants.Foundry.Tools.ResourceId];
                                        }

                                        //parse destination tool connection
                                        string destinationToolDisplayName = getResourceName(azureAISearchResourceId);
                                        string destinationSearchConnectionName = $"{destinationToolDisplayName}-{index.IndexName}";

                                        //map to source tool connection
                                        connectionLookup.Add(destinationSearchConnectionName, sourceSearchConnectionName);
                                        reverseConnectionLookup.Add(sourceSearchConnectionName, destinationSearchConnectionName);

                                        //create azure ai search tool definition
                                        toolDefinitions.Add(sourceSearchConnectionName, new SearchToolDefinition(destinationSearchConnectionName,
                                                                                                                destinationToolDisplayName,
                                                                                                                azureAISearchResourceId,
                                                                                                                azureAISearchAdminKey,
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
                                collectWarning($"Agent {sourceAgent.Name} has a unsupported tool of type {tool.GetType().Name}.");
                            }
                        }
                    }
                    else if (latestVersion.Definition is WorkflowAgentDefinition)
                    {
                        //workflow
                        WorkflowAgentDefinition definition = (WorkflowAgentDefinition)latestVersion.Definition;

                        //get yaml
                        PropertyInfo workflowYaml = definition.GetType().GetProperty(FSPKConstants.Foundry.WorkflowYaml, BindingFlags.Instance | BindingFlags.GetProperty | BindingFlags.NonPublic);
                        migratableAgent.WorkflowYaml = workflowYaml.GetValue(definition).ToString();
                    }
                    else
                    {
                        //unsupported
                        throw new Exception($"Agent {sourceAgent.Name} has an unsupported definition of type {latestVersion.Definition.GetType().Name}.");
                    }

                    //collect agents
                    sourceAgents.Add(migratableAgent);
                }

                //get destination foundry project
                CognitiveServicesProjectResource destinationProject = armClient.GetCognitiveServicesProjectResource(CognitiveServicesProjectResource.CreateResourceIdentifier(this._foundryProjectSettings.SubscriptionId, destinationResourceGroupName, destinationFoundryAccountName, destinationFoundryProjectName));
                CognitiveServicesProjectConnectionCollection destinationProjectConnections = destinationProject.GetCognitiveServicesProjectConnections();

                //get destination connections
                List<CognitiveServicesProjectConnectionResource> destinationConnections = new List<CognitiveServicesProjectConnectionResource>();
                await foreach (CognitiveServicesProjectConnectionResource destinationConnection in destinationProjectConnections.GetAllAsync())
                    destinationConnections.Add(destinationConnection);

                //process destination connections
                foreach (CognitiveServicesProjectConnectionResource destinationConnection in destinationConnections)
                {
                    //check tool
                    string destinationConnectionName = destinationConnection.Data.Name;
                    if (toolDefinitions.ContainsKey(destinationConnectionName) || (connectionLookup.ContainsKey(destinationConnectionName) && toolDefinitions.ContainsKey(connectionLookup[destinationConnectionName])))
                    {
                        //tool already exists
                        if (migrateAgentsRequest.ForceChanges)
                        {
                            //delete
                            await destinationConnection.DeleteAsync(WaitUntil.Completed);
                            this._logger.LogInformation($"Deleted existing destination connection {destinationConnectionName} since force changes is enabled for this migration.");
                        }
                        else
                        {
                            //duplicate
                            collectWarning($"Destination connection {destinationConnectionName} already exists, and force changes is disabled for this migration.");
                            continue;
                        }
                    }
                }

                //create destination connections
                foreach (string sourceToolName in toolDefinitions.Keys)
                {
                    try
                    {
                        //create connection
                        IToolDefintion destinationTool = toolDefinitions[sourceToolName];
                        CognitiveServicesConnectionData destinationConnection = destinationTool.CreateConnection();
                        string destinationToolName = reverseConnectionLookup.GetValueOrDefault(sourceToolName, sourceToolName);
                        ArmOperation<CognitiveServicesProjectConnectionResource> connectionResult = await destinationProjectConnections.CreateOrUpdateAsync(WaitUntil.Completed, destinationToolName, destinationConnection);

                        //check result
                        if (connectionResult.HasValue)
                        {
                            //success
                            result.SuccessfulConnections.Add(destinationToolName);
                            this._logger.LogInformation($"Successfully created destinaton connection {destinationToolName}.");
                        }
                        else
                        {
                            //error
                            collectError($"Unable to create destination connection {destinationToolName}: {connectionResult.GetRawResponse().Content.ToString()}");
                        }
                    }
                    catch (Exception ex)
                    {
                        //error
                        collectError($"Failed to migrate destination connection {sourceToolName}.", ex);
                    }
                }

                //get destination foundry account
                CognitiveServicesAccountResource destinationAccount = armClient.GetCognitiveServicesAccountResource(CognitiveServicesAccountResource.CreateResourceIdentifier(this._foundryProjectSettings.SubscriptionId, destinationResourceGroupName, destinationFoundryAccountName));
                CognitiveServicesAccountDeploymentCollection destinationDeployments = destinationAccount.GetCognitiveServicesAccountDeployments();
                Dictionary<string, ProjectsAgentVersion> exitingDestinationAgents = new Dictionary<string, ProjectsAgentVersion>();
                Dictionary<string, AzureAISearchToolIndex> destinationIndexes = new Dictionary<string, AzureAISearchToolIndex>();

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
                            collectWarning($"Destination model {sourceModel.Name} already exists, and force changes is disabled for this migration.");
                            continue;
                        }
                    }

                    //this marks a model deployment error as a warning if no agents are using it
                    ArmOperation<CognitiveServicesAccountDeploymentResource> modelDeploymentResult = null;
                    void collectModelDeploymentError(string rawError)
                    {
                        //initialization
                        string error = $"Unable to create destination model {sourceModel.Name}: {rawError}";

                        //return
                        if (sourceAgents.Any(a => a.ModelName == sourceModel.Name))
                            collectError(error);
                        else
                            collectWarning(error);
                    }

                    try
                    {
                        //deploy model
                        modelDeploymentResult = await destinationDeployments.CreateOrUpdateAsync(WaitUntil.Completed, sourceModel.Name, new CognitiveServicesAccountDeploymentData()
                        {
                            //create model
                            Sku = sourceModel.Sku,
                            Properties = sourceModel.Properties
                        });

                        //check model
                        if (modelDeploymentResult.HasValue)
                        {
                            //success
                            result.SuccessfulModels.Add(sourceModel.Name);
                            this._logger.LogInformation($"Successfully deployed destinaton model {sourceModel.Name}.");
                        }
                        else
                        {
                            //error
                            collectModelDeploymentError(modelDeploymentResult.GetRawResponse().Content.ToString());
                        }
                    }
                    catch (Exception ex)
                    {
                        //ensure exception is  logged
                        collectModelDeploymentError(ex.ToString());
                        this._logger.LogError(ex, $"Failed to deploy destination model {sourceModel.Name}.");
                    }
                }

                //get destination agents
                await foreach (ProjectsAgentRecord destinationAgent in destinationAgentsClient.GetAgentsAsync())
                    exitingDestinationAgents.Add(destinationAgent.Name, destinationAgent.GetLatestVersion());

                //create destination agents
                foreach (MigratableAgent sourceAgent in sourceAgents.OrderBy(a => a.IsWorkflow))
                {
                    //check agent
                    if (exitingDestinationAgents.ContainsKey(sourceAgent.Name))
                    {
                        //agent exists
                        if (migrateAgentsRequest.ForceChanges)
                        {
                            //delete
                            await destinationAgentsClient.DeleteAgentAsync(sourceAgent.Name);
                            this._logger.LogInformation($"Deleted existing destination agent {sourceAgent.Name} since force changes is enabled for this migration.");
                        }
                        else
                        {
                            //duplicate
                            collectWarning($"Destination agent {sourceAgent.Name} already exists, and force changes is disabled for this migration.");
                            continue;
                        }
                    }

                    //check workflow
                    if (sourceAgent.IsWorkflow)
                    {
                        //create workflow definition
                        WorkflowAgentDefinition sourceWorkflowDefinition = (WorkflowAgentDefinition)sourceAgent.Definition;
                        WorkflowAgentDefinition destinationWorkflowDefinition = WorkflowAgentDefinition.FromYaml(sourceAgent.WorkflowYaml);

                        try
                        {
                            //create workflow
                            destinationWorkflowDefinition.ContentFilterConfiguration = sourceWorkflowDefinition.ContentFilterConfiguration;
                            ClientResult<ProjectsAgentVersion> workflowResult = await destinationAgentsClient.CreateAgentVersionAsync(sourceAgent.Name, new ProjectsAgentVersionCreationOptions(destinationWorkflowDefinition)
                            {
                                //assemble object
                                Description = sourceAgent.Description
                            });

                            //check result
                            if (workflowResult?.Value == null)
                            {
                                //error
                                collectError($"Unable to create destination workflow {sourceAgent}: {workflowResult.GetRawResponse().Content.ToString()}");
                            }
                            else
                            {
                                //success
                                result.SuccessfulWorkflows.Add(sourceAgent.Name);
                                this._logger.LogInformation($"Successfully migrated workflow {sourceAgent}.");
                            }
                        }
                        catch (Exception ex)
                        {
                            //error
                            collectError($"Failed to migrate workflow {sourceAgent}.", ex);
                        }
                    }
                    else
                    {
                        //get agent definition
                        if (sourceAgent.Definition is DeclarativeAgentDefinition)
                        {
                            //create agent definition
                            DeclarativeAgentDefinition sourceAgentDefinition = (DeclarativeAgentDefinition)sourceAgent.Definition;
                            DeclarativeAgentDefinition destinationAgentDefinition = new DeclarativeAgentDefinition(sourceAgentDefinition.Model);

                            //check model
                            if (!result.SuccessfulModels.Contains(sourceAgentDefinition.Model))
                            {
                                //error
                                collectError($"Could not migrate destination agent {sourceAgent.Name} because its model {sourceAgentDefinition.Model} failed to deploy.");
                                continue;
                            }

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
                            foreach (string sourceToolName in sourceAgent.ToolNames)
                            {
                                //check tool
                                IToolDefintion tool = toolDefinitions[sourceToolName];
                                string destinationToolName = reverseConnectionLookup.GetValueOrDefault(sourceToolName, sourceToolName);
                                if (!result.SuccessfulConnections.Contains(sourceToolName) && !result.SuccessfulConnections.Contains(destinationToolName))
                                {
                                    //error
                                    collectError($"Could not migrate destination agent {sourceAgent.Name}'s {sourceAgentDefinition.Model} tool because its connection failed to provision.");
                                    continue;
                                }

                                //migrate tool
                                if (tool is SearchToolDefinition)
                                {
                                    //create search tool
                                    SearchToolDefinition searchTool = (SearchToolDefinition)tool;
                                    if (!destinationIndexes.ContainsKey(searchTool.IndexName))
                                    {
                                        //create search index connection
                                        AIProjectConnection destinationSearchConnection = await destinationFoundryClient.Connections.GetConnectionAsync(destinationToolName);
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
                                    AIProjectConnection destinationSharePointConnection = await destinationFoundryClient.Connections.GetConnectionAsync(destinationToolName);

                                    //add sharepoint tool
                                    sharePointToolParameters.ProjectConnections.Add(new ToolProjectConnection(destinationSharePointConnection.Id));
                                    destinationAgentDefinition.Tools.Add(new SharepointPreviewTool(sharePointToolParameters));
                                }
                                else
                                {
                                    //unsupported tool
                                    collectError($"Unable to add tool {destinationToolName} to agent {sourceAgent}: {tool.GetType().Name} is unsupported.");
                                    continue;
                                }
                            }

                            try
                            {
                                //create agent
                                ClientResult<ProjectsAgentVersion> agentResult = await destinationAgentsClient.CreateAgentVersionAsync(sourceAgent.Name, new ProjectsAgentVersionCreationOptions(destinationAgentDefinition)
                                {
                                    //assemble object
                                    Description = sourceAgent.Description,
                                });

                                //check result
                                if (agentResult?.Value == null)
                                {
                                    //eror
                                    collectError($"Unable to create destination agent {sourceAgent}: {agentResult?.GetRawResponse()?.Content?.ToString() ?? "N/A"}");
                                }
                                else
                                {
                                    //success
                                    result.SuccessfulAgents.Add(sourceAgent.Name);
                                    this._logger.LogInformation($"Successfully migrated agent {sourceAgent}.");
                                }
                            }
                            catch (Exception ex)
                            {
                                //error
                                collectError($"Failed to migrate agent {sourceAgent.Name}.", ex);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //error
                collectError($"Agent migration {migrateAgentsRequest} failed.", ex);
            }

            //return
            return result;
        }
        #endregion
        #region Private Methods
        /// <summary>
        /// Builds a foundry client with the given credential.
        /// </summary>
        private AIProjectClient GetFoundryClient(TokenCredential tokenCredential, int index = 0)
        {
            //return
            return new AIProjectClient(this._foundryProjectSettings.ProjectEndpoints[index], tokenCredential);
        }
        #endregion
    }
}