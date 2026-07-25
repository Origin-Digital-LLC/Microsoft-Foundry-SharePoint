namespace FoundrySharePointKnowledge.Domain.Foundry.VectorStores
{
    /// <summary>
    /// Gets the progress of an ongoing vector store indexing operation.
    /// </summary>
    public record IndexProgressRequest
    {
        #region Initialization
        public IndexProgressRequest(string vectorStoreId, string batchId)
        {
            //initialization
            this.BatchId = batchId;
            this.VectorStoreId = vectorStoreId;
        }
        #endregion
        #region Properties
        public string BatchId { get; init; }
        public string VectorStoreId { get; init; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return this.BatchId;
        }
        #endregion
    }
}
