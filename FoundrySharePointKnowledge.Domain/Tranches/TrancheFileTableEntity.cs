using System;

using Azure;
using Azure.Data.Tables;

namespace FoundrySharePointKnowledge.Domain.Tranches
{
    /// <summary>
    /// Represents an uploaded file belonging to a tranche in Azure Storage tables.
    /// </summary>
    public class TrancheFileTableEntity : ITableEntity
    {
        #region Properties
        public ETag ETag { get; set; }
        public long FileSize { get; set; }
        public string RowKey { get; set; }
        public string FileId { get; set; }
        public string ContentType { get; set; }
        public string PartitionKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
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
