using System;
using System.Threading.Tasks;

using FoundrySharePointKnowledge.Domain.Upload;

namespace FoundrySharePointKnowledge.Domain.Contracts
{
    public interface ITrancheService
    {
        #region Methods
        Task CancelUploadAsync(string containerName);
        Task EditTrancheAsync(EditTrancheRequest request);
        Task<TrancheTableEntity[]> LoadTranchesAsync(string userName);
        Task DeleteTrancheAsync(string containerName, Guid trancheId);
        Task<UploadSession> CreateTrancheAsync(string userName, string trancheName);
        Task TrackTrancheFilesAsync(Guid trancheId, string containerName, string[] fileNames);
        #endregion
    }
}
