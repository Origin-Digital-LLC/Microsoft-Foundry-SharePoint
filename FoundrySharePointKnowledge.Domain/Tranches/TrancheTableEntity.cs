using System;

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
        public int FileCount { get; set; }
        public double TotalSize { get; set; }
        public string PartitionKey { get; set; }
        public string ContainerName { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public string Name { get; set; } = string.Format(FSPKConstants.AzureStorage.Tables.DefaultTrancheNameFormat, DateTime.Now);

        /// <summary>
        /// The tranche's unique identifier.
        /// </summary>
        public Guid TrancheId => Guid.Parse(this.RowKey);
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
