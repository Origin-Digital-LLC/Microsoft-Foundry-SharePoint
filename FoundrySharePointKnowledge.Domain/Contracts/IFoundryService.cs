using System.Threading.Tasks;

using Azure.Core;

using FoundrySharePointKnowledge.Domain.Foundry;

namespace FoundrySharePointKnowledge.Domain.Contracts
{
    public interface IFoundryService
    {
        #region Methods
        Task<string> MigrateAgentsAsync(MigrateAgentsRequest migrateAgentsRequest, TokenCredential foundryCredential);
        Task<AgentResponse<string>> ConverseWithAgentAsync(ConversationPrompt prompt, FoundryCredential foundryCredential);
        Task<AgentResponse<EngineerBio[]>> ExecuteExpertiseFinderWorkflowAsync(string prompt, TokenCredential tokenCredential);
        #endregion  
    }
}
