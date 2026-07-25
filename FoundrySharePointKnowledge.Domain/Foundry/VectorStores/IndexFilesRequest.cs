using System.Collections.Generic;

namespace FoundrySharePointKnowledge.Domain.Foundry.VectorStores
{
    /// <summary>
    /// Starts an operation to index Foundry project files into a vector store.
    /// </summary>
    public record IndexFilesRequest
    {
        #region Initialization
        public IndexFilesRequest(string vectorStoreId, Dictionary<string, string> fileIds, bool waitForCompletion)
        {
            //initialization
            this.FileIds = fileIds;
            this.VectorStoreId = vectorStoreId;
            this.WaitForCompletion = waitForCompletion;
        }
        #endregion
        #region Properties
        public string VectorStoreId { get; init; }
        public bool WaitForCompletion { get; init; }
        public Dictionary<string, string> FileIds { get; init; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return this.VectorStoreId;
        }
        #endregion
    }
}
