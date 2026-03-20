using System.Threading.Tasks;

using FoundrySharePointKnowledge.Domain.SharePoint;

namespace FoundrySharePointKnowledge.Domain.Contracts
{
    public interface ISharePointService
    {
        #region Methods
        Task<string> HandleWebhookAsync(SPWebhookPayload webhook);
        Task<byte[]> GetFileContentsMostPrivilegedAsync(SPFile file);
        Task<byte[]> GetFileContentsLeastPrivilegedAsync(SPFile file);
        #endregion
    }
}
