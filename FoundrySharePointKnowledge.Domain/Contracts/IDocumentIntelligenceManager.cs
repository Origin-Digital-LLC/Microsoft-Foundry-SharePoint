using System.Threading;
using System.Threading.Tasks;

using FoundrySharePointKnowledge.Domain.Foundry.VectorStores;

namespace FoundrySharePointKnowledge.Domain.Contracts
{
    public interface IDocumentIntelligenceManager
    {
        #region Methods
        /// <summary>
        /// Converts one source blob into markdown, serving a previously converted copy whenever the source
        /// has not changed since it was written.
        /// </summary>
        Task<MarkdownConversion> ConvertToMarkdownAsync(MarkdownConversionRequest conversionRequest, CancellationToken cancellationToken);
        #endregion
    }
}
