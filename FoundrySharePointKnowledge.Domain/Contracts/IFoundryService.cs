using System.Threading.Tasks;

using FoundrySharePointKnowledge.Domain.SharePoint;

namespace FoundrySharePointKnowledge.Domain.Contracts
{
    public interface IFoundryService
    {
        #region Methods
        Task<SPFileChunk[]> ChunkFileAsync(SPFile file);
        Task<byte[]> GetFileContentsMostPriviledgedAsync(SPFile file);
        Task<byte[]> GetFileContentsLeastPriviledgedAsync(SPFile file);
        #endregion
    }
}
