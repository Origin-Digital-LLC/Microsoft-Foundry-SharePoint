using System.Threading.Tasks;

using FoundrySharePointKnowledge.Domain.SharePoint;

namespace FoundrySharePointKnowledge.Domain.Contracts
{
    public interface ISearchService
    {
        #region Methods
        Task<bool> DeleteFileAsync(SPFile file);
        Task<bool> InjestFileAsync(SPFile file);
        Task<bool> UploadFileAsync(SPFile file);
        Task<string> EnsureVectorizedIndexAsync(string indexName);
        Task<string> EnsureVectorizableBlobIndexAsync(string indexName);
        #endregion
    }
}
