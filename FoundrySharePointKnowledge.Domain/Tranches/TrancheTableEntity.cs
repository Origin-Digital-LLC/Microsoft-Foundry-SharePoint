using System;
using System.Runtime.Serialization;

using Azure;
using Azure.Data.Tables;

using FoundrySharePointKnowledge.Common;

namespace FoundrySharePointKnowledge.Domain.Tranches
{
    /// <summary>
    /// Represents a bulk upload tranche in Azure Storage tables.
    /// </summary>
    public class TrancheTableEntity : ITableEntity
    {
        #region Properties
        public ETag ETag { get; set; }
        public string RowKey { get; set; }
        public int StatusValue { get; set; }
        public int BlobFileCount { get; set; }
        public string PartitionKey { get; set; }
        public string VectorStoreId { get; set; }
        public double BlobTotalSize { get; set; }
        public string ContainerName { get; set; }
        public string IndexBatchIds { get; set; }
        public int UploadedFileCount { get; set; }
        public int UploadingFileCount { get; set; }
        public double UploadedFileSize { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public double IndexedFileProgress { get; set; }
        public double UploadedFileProgress { get; set; }
        public string Name { get; set; } = string.Format(FSPKConstants.AzureStorage.Tables.DefaultTrancheNameFormat, DateTime.Now);

        /// <summary>
        /// The tranche's unique identifier.
        /// </summary>
        [IgnoreDataMember()]
        public Guid TrancheId => Guid.Parse(this.RowKey);

        /// <summary>
        /// The tranche's synchronization progress; this is ignored during serialization because Azure Storage
        /// Tables cannot persist enumerations, so the underlying integer is stored in StatusValue instead.
        /// </summary>
        [IgnoreDataMember()]
        public TrancheStatus Status
        {
            get { return (TrancheStatus)this.StatusValue; }
            set { this.StatusValue = (int)value; }
        }

        /// <summary>
        /// The Foundry batches indexing the tranche's files, since a vector store caps how many files a single
        /// batch may carry; this is ignored during serialization because Azure Storage Tables cannot persist
        /// arrays, so the delimited list is stored in IndexBatchIds instead.
        /// </summary>
        [IgnoreDataMember()]
        public string[] IndexBatches
        {
            get
            {
                //return
                if (string.IsNullOrWhiteSpace(this.IndexBatchIds))
                    return Array.Empty<string>();
                else
                    return this.IndexBatchIds.Split(FSPKConstants.Foundry.VectorStores.BatchIdDelimiter, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }
            set
            {
                //return
                this.IndexBatchIds = value == null ? null : string.Join(FSPKConstants.Foundry.VectorStores.BatchIdDelimiter, value);
            }
        }
        #endregion
        #region Public Methods
        /// <summary>
        /// Returns a textual representation of an instance of this object.
        /// </summary>
        public override string ToString()
        {
            //return
            return this.RowKey ?? "N/A";
        }
        #endregion
    }
}
