using System.Threading.Tasks;

using Azure.Core;

using FoundrySharePointKnowledge.Domain.Foundry;
using FoundrySharePointKnowledge.Domain.Foundry.Agents;
using FoundrySharePointKnowledge.Domain.Foundry.Conversations;

namespace FoundrySharePointKnowledge.Domain.Contracts
{
    public interface IFoundryService
    {
        #region Methods
        Task<string> EnsureVectorStoreAsync(string name);
        Task<AgentResponse<string>> ConverseWithAgentAsync(ConversationPrompt prompt, FoundryCredential foundryCredential);
        Task<AgentResponse<EngineerBio[]>> ExecuteExpertiseFinderWorkflowAsync(string prompt, TokenCredential tokenCredential);
        Task<MigrateAgentsResponse> PromoteAgentsAsync(MigrateAgentsRequest migrateAgentsRequest, TokenCredential foundryCredential);
        #endregion  
    }
}
