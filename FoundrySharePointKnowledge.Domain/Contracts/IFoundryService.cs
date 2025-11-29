using System.Threading.Tasks;

using FoundrySharePointKnowledge.Domain.SharePoint;

namespace FoundrySharePointKnowledge.Domain.Contracts
{
    public interface IFoundryService
    {
        #region Methods
        Task<SPFileChunk[]> ChunkFileAsync(SPFile file);
        Task<byte[]> GetFileContentsMostPrivilegedAsync(SPFile file);
        Task<byte[]> GetFileContentsLeastPrivilegedAsync(SPFile file);
        #endregion
    }
}
