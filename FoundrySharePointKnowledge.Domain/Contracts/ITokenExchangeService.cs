using System.Threading.Tasks;

using FoundrySharePointKnowledge.Domain.Foundry;

namespace FoundrySharePointKnowledge.Domain.Contracts
{
    public interface ITokenExchangeService
    {
        #region Methods
        Task<FoundryCredential> GetFoundryCredentialAsync(string userName, params string[] scopes);
        #endregion
    }
}
