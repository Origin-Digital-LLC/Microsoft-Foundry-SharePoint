using System;

namespace FoundrySharePointKnowledge.Domain.Foundry.VectorStores
{
    /// <summary>
    /// Starts an operation to index a tranche's uploaded files into its vector store; the files themselves are
    /// read from the tranche, since the operation runs in the background long after this is handed over.
    /// </summary>
    public record IndexFilesRequest
    {
        #region Initialization
        public IndexFilesRequest(Guid trancheId, string vectorStoreId)
        {
            //initialization
            this.TrancheId = trancheId;
            this.VectorStoreId = vectorStoreId;
        }
        #endregion
        #region Properties
        public Guid TrancheId { get; init; }
        public string VectorStoreId { get; init; }
        #endregion
        #region Public Methods
        /// <summary>
        /// Returns a textual representation of an instance of this object.
        /// </summary>
        public override string ToString()
        {
            //return
            return this.VectorStoreId;
        }
        #endregion
    }
}
