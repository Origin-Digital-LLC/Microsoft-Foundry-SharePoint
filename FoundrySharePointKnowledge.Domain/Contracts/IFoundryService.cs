using System;
using System.Threading;
using System.Threading.Tasks;

using Azure.Core;

using FoundrySharePointKnowledge.Domain.Foundry;
using FoundrySharePointKnowledge.Domain.Foundry.Agents;
using FoundrySharePointKnowledge.Domain.Foundry.VectorStores;
using FoundrySharePointKnowledge.Domain.Foundry.Conversations;

namespace FoundrySharePointKnowledge.Domain.Contracts
{
    public interface IFoundryService
    {
        #region Methods
        Task<string> EnsureVectorStoreAsync(string name);
        Task<bool> DeleteVectorStoreAsync(string vectorStoreId);
        Task<VectorStoreDetail> GetVectorStoreAsync(string vectorStoreId);
        Task<string> AddIndexBatchAsync(string vectorStoreId, string[] fileIds);
        Task<string[]> GetIndexedFileIdsAsync(string vectorStoreId, string batchId);
        Task<ResetFilesResponse> ResetFilesAsync(ResetFilesRequest resetFilesRequest);
        Task<IndexProgressResponse> GetIndexOperationProgressAsync(IndexProgressRequest indexProgressRequest);
        Task<AgentResponse<string>> ConverseWithAgentAsync(ConversationPrompt prompt, FoundryCredential foundryCredential);
        Task<AgentResponse<EngineerBio[]>> ExecuteExpertiseFinderWorkflowAsync(string prompt, TokenCredential tokenCredential);
        Task<MigrateAgentsResponse> PromoteAgentsAsync(MigrateAgentsRequest migrateAgentsRequest, TokenCredential foundryCredential);
        Task<UploadFilesResponse> UploadVectorStoreFilesAsync(UploadFilesRequest uploadFilesRequest, Func<int, int, Task> reportProgressAsync, CancellationToken cancellationToken);
        #endregion
    }
}
