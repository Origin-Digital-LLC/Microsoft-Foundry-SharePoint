using System.Threading.Tasks;

using FoundrySharePointKnowledge.Domain.SharePoint;

namespace FoundrySharePointKnowledge.Domain.Contracts
{
    public interface IFoundryService
    {
        #region Methods
        Task<byte[]> GetFileContentsAsync(SPFile file);
        Task<SPFileChunk[]> ChunkFileAsync(SPFile file);
        #endregion
    }
}
