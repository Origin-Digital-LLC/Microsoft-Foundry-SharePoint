using System;

namespace FoundrySharePointKnowledge.Domain.Upload
{
    /// <summary>
    /// This holds the metadata needed to record a bulk upload's completed files.
    /// </summary>
    public record CompleteUploadRequest
    {
        #region Initialization
        public CompleteUploadRequest(Guid trancheId, string containerName, string[] fileNames)
        {
            //initialization
            this.TrancheId = trancheId;
            this.ContainerName = containerName;
            this.FileNames = fileNames ?? Array.Empty<string>();
        }
        #endregion
        #region Properties
        public Guid TrancheId { get; init; }
        public string[] FileNames { get; init; }
        public string ContainerName { get; init; }
        #endregion
        #region Public Methods
        /// <summary>
        /// Returns a textual representation of an instance of this object.
        /// </summary>
        public override string ToString()
        {
            //return
            return $"Tranche {this.TrancheId} completed with {this.FileNames.Length} file(s).";
        }
        #endregion
    }
}
