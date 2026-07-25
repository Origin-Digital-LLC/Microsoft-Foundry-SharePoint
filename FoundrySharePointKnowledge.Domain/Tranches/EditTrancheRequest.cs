using System;

namespace FoundrySharePointKnowledge.Domain.Tranches
{
    /// <summary>
    /// This holds the editable metadata for a bulk upload tranche.
    /// </summary>
    public record EditTrancheRequest
    {
        #region Initialization
        public EditTrancheRequest(Guid trancheId,
                                  string name,
                                  string vectorStoreId = null,
                                  TrancheStatus status = TrancheStatus.Pending,
                                  int uploadedFileCount = 0,
                                  double uploadedFileSize = 0)
        {
            //initialization
            this.Name = name;
            this.Status = status;
            this.TrancheId = trancheId;
            this.VectorStoreId = vectorStoreId;
            this.UploadedFileSize = uploadedFileSize;
            this.UploadedFileCount = uploadedFileCount;
        }
        #endregion
        #region Properties
        public string Name { get; init; }
        public Guid TrancheId { get; init; }
        public TrancheStatus Status { get; init; }
        public string VectorStoreId { get; init; }
        public int UploadedFileCount { get; init; }
        public double UploadedFileSize { get; init; }
        public double IndexedFileProgress { get; init; }
        #endregion
        #region Public Methods
        /// <summary>
        /// Returns a textual representation of an instance of this object.
        /// </summary>
        public override string ToString()
        {
            //return
            return $"Tranche {this.TrancheId} renamed to \"{this.Name}\".";
        }
        #endregion
    }
}
