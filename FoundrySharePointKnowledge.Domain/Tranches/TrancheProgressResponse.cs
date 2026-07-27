using System.Text.Json.Serialization;

namespace FoundrySharePointKnowledge.Domain.Tranches
{
    /// <summary>
    /// Holds the progress of one of a tranche's background operations, along with the totals recorded against
    /// the tranche; the caller asking for the progress decides which of the tranche's operations it describes.
    /// </summary>
    public record TrancheProgressResponse
    {
        #region Initialization
        /// <summary>
        /// Success.
        /// </summary>
        public TrancheProgressResponse(TrancheTableEntity tranche, double progress)
        {
            //initialization
            this.Progress = progress;
            this.Status = tranche.Status;
            this.UploadedFileSize = tranche.UploadedFileSize;
            this.UploadedFileCount = tranche.UploadedFileCount;
            this.UploadingFileCount = tranche.UploadingFileCount;
        }

        /// <summary>
        /// Error; this is also the constructor callers deserialize into, since a record with more than one
        /// constructor is ambiguous to System.Text.Json and every other value is populated through its
        /// init accessor.
        /// </summary>
        [JsonConstructor()]
        public TrancheProgressResponse(string error)
        {
            //initialization
            this.Error = error;
        }
        #endregion
        #region Properties
        public string Error { get; init; }
        public double Progress { get; init; }
        public TrancheStatus Status { get; init; }
        public int UploadedFileCount { get; init; }
        public int UploadingFileCount { get; init; }
        public double UploadedFileSize { get; init; }

        public bool IsError => !string.IsNullOrWhiteSpace(this.Error);
        #endregion
        #region Public Methods
        /// <summary>
        /// Returns a textual representation of an instance of this object.
        /// </summary>
        public override string ToString()
        {
            //return
            return this.IsError ? this.Error : this.Progress.ToString("P1");
        }
        #endregion
    }
}
