using System;

namespace FoundrySharePointKnowledge.Domain.Foundry.VectorStores
{
    /// <summary>
    /// This holds the metadata needed to upload files to a vector store.
    /// </summary>
    public record UploadFilesRequest
    {
        #region Initialization
        public UploadFilesRequest(Guid trancheId, string containerName, string filePrefix = null, string vectorStoreId = null)
        {
            //initialization
            this.TrancheId = trancheId;
            this.FilePrefix = filePrefix;
            this.ContainerName = containerName;
            this.VectorStoreId = vectorStoreId;
        }
        #endregion
        #region Properties
        public Guid TrancheId { get; init; }
        public string FilePrefix { get; init; }
        public string ContainerName { get; init; }
        public string VectorStoreId { get; init; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //retun
            return this.ContainerName;
        }
        #endregion
    }
}
