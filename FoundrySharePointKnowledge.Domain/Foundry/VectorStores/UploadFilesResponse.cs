using System.Collections.Generic;

using FoundrySharePointKnowledge.Common;

namespace FoundrySharePointKnowledge.Domain.Foundry.VectorStores
{
    /// <summary>
    /// This holds the result of a vector store upload operation.
    /// </summary>
    public record UploadFilesResponse
    {
        #region Initialization
        /// <summary>
        /// Success.
        /// </summary>
        public UploadFilesResponse(Dictionary<string, string> fileIds, string[] failedFiles, double totalSize)
        {
            //initialization
            this.FileIds = fileIds;
            this.TotalSize = totalSize;
            this.FailedFiles = failedFiles;
        }

        /// <summary>
        /// Failure.
        /// </summary>
        public UploadFilesResponse(string error)
        {
            //initialization
            this.Error = error;
        }
        #endregion
        #region Properties
        public string Error { get; init; }
        public double TotalSize { get; init; }
        public string[] FailedFiles { get; init; }
        public Dictionary<string, string> FileIds { get; init; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //retun
            return $"Uploaded {this.FileIds?.Count ?? 0} {this.FileIds.Pluralize("file")} with {this.FailedFiles?.Length ?? 0} {this.FailedFiles.Pluralize("error")}.";
        }
        #endregion
    }
}
