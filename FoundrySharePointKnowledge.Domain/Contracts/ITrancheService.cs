using System;
using System.Threading.Tasks;

using FoundrySharePointKnowledge.Domain.Tranches;
using FoundrySharePointKnowledge.Domain.Foundry.VectorStores;

namespace FoundrySharePointKnowledge.Domain.Contracts
{
    public interface ITrancheService
    {
        #region Methods
        Task CancelUploadAsync(string containerName);
        Task EditTrancheAsync(EditTrancheRequest request);
        Task UploadTrancheFilesAsync(UploadFilesRequest request);
        Task IndexTrancheFilesAsync(IndexFilesRequest request);
        Task<UpdateFilesResponse> UpdateFilesAsync(UpdateFilesRequest request);
        Task<TrancheTableEntity[]> LoadTranchesAsync(string userName);
        Task<TrancheTableEntity> LoadTrancheAsync(Guid trancheId);
        Task DeleteTrancheAsync(string containerName, Guid trancheId);
        Task<UploadSession> CreateTrancheAsync(string userName, string trancheName);
        Task TrackTrancheFilesAsync(Guid trancheId, string containerName, string[] fileNames);
        #endregion
    }
}
