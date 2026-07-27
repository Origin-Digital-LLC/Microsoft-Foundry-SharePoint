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
        Task<ResetFilesResponse> ResetFilesAsync(ResetFilesRequest resetFilesRequest);
        /// <summary>
        /// Starts one batch indexing the supplied files into a vector store and returns its identifier; the
        /// caller is responsible for keeping each batch within the store's per-batch file cap.
        /// </summary>
        Task<string> AddIndexBatchAsync(string vectorStoreId, string[] fileIds);

        /// <summary>
        /// Uploads a tranche's blobs into a Foundry project as markdown, reporting how many of how many files
        /// have been dealt with so a caller running this in the background can record its progress; the total
        /// is only known once the container has been listed, so it is reported alongside every count.
        /// </summary>
        Task<UploadFilesResponse> UploadVectorStoreFilesAsync(UploadFilesRequest uploadFilesRequest, Func<int, int, Task> reportProgressAsync, CancellationToken cancellationToken);
        Task<IndexProgressResponse> GetIndexOperationProgressAsync(IndexProgressRequest indexProgressRequest);
        Task<AgentResponse<string>> ConverseWithAgentAsync(ConversationPrompt prompt, FoundryCredential foundryCredential);
        Task<AgentResponse<EngineerBio[]>> ExecuteExpertiseFinderWorkflowAsync(string prompt, TokenCredential tokenCredential);
        Task<MigrateAgentsResponse> PromoteAgentsAsync(MigrateAgentsRequest migrateAgentsRequest, TokenCredential foundryCredential);
        #endregion
    }
}
