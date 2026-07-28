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
        /// Gets a vector store along with every file attached to it, which is the service's own account of
        /// what an indexing run landed rather than anything this solution recorded alongside it.
        /// </summary>
        Task<VectorStoreDetail> GetVectorStoreAsync(string vectorStoreId);

        /// <summary>
        /// Gets the identifiers of the files one batch actually landed in a vector store, which is what tells
        /// a caller tracking individual files which of them made it in and which did not.
        /// </summary>
        Task<string[]> GetIndexedFileIdsAsync(string vectorStoreId, string batchId);

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
