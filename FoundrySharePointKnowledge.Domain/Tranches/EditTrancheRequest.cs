using System;
using System.Text.Json.Serialization;

namespace FoundrySharePointKnowledge.Domain.Tranches
{
    /// <summary>
    /// This holds the editable metadata for a bulk upload tranche.
    /// </summary>
    public record EditTrancheRequest
    {
        #region Initialization
        /// <summary>
        /// This is also the constructor callers deserialize into, since a record with more than one
        /// constructor is ambiguous to System.Text.Json.
        /// </summary>
        [JsonConstructor()]
        public EditTrancheRequest(Guid trancheId,
                                  string name,
                                  string vectorStoreId = null,
                                  string indexBatchIds = null,
                                  TrancheStatus status = TrancheStatus.Pending,
                                  int blobFileCount = 0,
                                  double blobTotalSize = 0,
                                  int uploadedFileCount = 0,
                                  int uploadingFileCount = 0,
                                  double uploadedFileSize = 0,
                                  double indexedFileProgress = 0,
                                  double uploadedFileProgress = 0)
        {
            //initialization
            this.Name = name;
            this.Status = status;
            this.TrancheId = trancheId;
            this.IndexBatchIds = indexBatchIds;
            this.VectorStoreId = vectorStoreId;
            this.BlobFileCount = blobFileCount;
            this.BlobTotalSize = blobTotalSize;
            this.UploadedFileSize = uploadedFileSize;
            this.UploadedFileCount = uploadedFileCount;
            this.UploadingFileCount = uploadingFileCount;
            this.IndexedFileProgress = indexedFileProgress;
            this.UploadedFileProgress = uploadedFileProgress;
        }

        /// <summary>
        /// Carries every editable field of a tranche forward, so a caller changing one of them with a "with"
        /// expression never clears the rest; the editing endpoint replaces the whole entity.
        /// </summary>
        public EditTrancheRequest(TrancheTableEntity tranche) : this(tranche == null ? Guid.Empty : tranche.TrancheId,
                                                                    tranche?.Name,
                                                                    tranche?.VectorStoreId,
                                                                    tranche?.IndexBatchIds,
                                                                    tranche?.Status ?? TrancheStatus.Pending,
                                                                    tranche?.BlobFileCount ?? 0,
                                                                    tranche?.BlobTotalSize ?? 0,
                                                                    tranche?.UploadedFileCount ?? 0,
                                                                    tranche?.UploadingFileCount ?? 0,
                                                                    tranche?.UploadedFileSize ?? 0,
                                                                    tranche?.IndexedFileProgress ?? 0,
                                                                    tranche?.UploadedFileProgress ?? 0)
        {
            //initialization
            ArgumentNullException.ThrowIfNull(tranche);
        }
        #endregion
        #region Properties
        public string Name { get; init; }
        public Guid TrancheId { get; init; }
        public int BlobFileCount { get; init; }
        public TrancheStatus Status { get; init; }
        public string IndexBatchIds { get; init; }
        public string VectorStoreId { get; init; }
        public double BlobTotalSize { get; init; }
        public int UploadedFileCount { get; init; }
        public int UploadingFileCount { get; init; }
        public double UploadedFileSize { get; init; }
        public double IndexedFileProgress { get; init; }
        public double UploadedFileProgress { get; init; }
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
