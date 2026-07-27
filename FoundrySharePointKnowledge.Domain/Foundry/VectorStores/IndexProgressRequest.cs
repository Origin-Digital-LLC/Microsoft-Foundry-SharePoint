using System;

namespace FoundrySharePointKnowledge.Domain.Foundry.VectorStores
{
    /// <summary>
    /// Gets the combined progress of an ongoing vector store indexing operation, which spans as many batches
    /// as its files needed.
    /// </summary>
    public record IndexProgressRequest
    {
        #region Initialization
        public IndexProgressRequest(string vectorStoreId, string[] batchIds)
        {
            //initialization
            this.BatchIds = batchIds;
            this.VectorStoreId = vectorStoreId;
        }
        #endregion
        #region Properties
        public string[] BatchIds { get; init; }
        public string VectorStoreId { get; init; }
        #endregion
        #region Public Methods
        /// <summary>
        /// Returns a textual representation of an instance of this object.
        /// </summary>
        public override string ToString()
        {
            //return
            return string.Join(", ", this.BatchIds ?? Array.Empty<string>());
        }
        #endregion
    }
}
