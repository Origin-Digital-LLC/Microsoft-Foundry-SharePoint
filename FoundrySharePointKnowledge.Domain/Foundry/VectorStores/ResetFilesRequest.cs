namespace FoundrySharePointKnowledge.Domain.Foundry.VectorStores
{
    /// <summary>
    /// This holds the metadata needed to reset a vector store's files.
    /// </summary>
    public record ResetFilesRequest
    {
        #region Initialization
        public ResetFilesRequest(string vectorStoreId)
        {
            //initialization
            this.VectorStoreId = vectorStoreId;
        }
        #endregion
        #region Properties
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
