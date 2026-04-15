using System.Threading.Tasks;

using FoundrySharePointKnowledge.Domain.Search;
using FoundrySharePointKnowledge.Domain.SharePoint;
using FoundrySharePointKnowledge.Domain.Search.WebAPISkill;
using FoundrySharePointKnowledge.Domain.Search.WebAPISkill.ProperCase;
using FoundrySharePointKnowledge.Domain.Search.WebAPISkill.ImageVectorization;

namespace FoundrySharePointKnowledge.Domain.Contracts
{
    public interface ISearchService
    {
        #region Methods
        Task<bool> DeleteFileAsync(SPFile file);
        Task<bool> InjestFileAsync(SPFile file);
        Task<bool> UploadFileAsync(SPFile file);        
        Task<VectorizedChunk[]> SearchAsync(string query);
        Task<string> EnsureVectorizedIndexAsync(string indexName);
        Task<string> EnsureSharePointListItemsSearchTopographyAsync(bool deleteExisting = true);
        Task<string> EnsureSharePointDocumentsSearchTopographyAsync(bool deleteExisting = true);
        Task<WebAPISkillPayload<ProperCaseOutput>> ToProperCaseAsync(WebAPISkillPayload<ProperCaseInput> payload);
        Task<MigrateStorageAccountResult> MigrateStorageAccountAsync(MigrateStorageAccountRequest migrateStorageAccountsRequest);
        Task<WebAPISkillPayload<ImageVectorizationOutput>> VectorizeImagesAsync(WebAPISkillPayload<ImageVectorizationInput> payload);
        #endregion
    }
}
