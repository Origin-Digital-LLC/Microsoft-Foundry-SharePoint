using System;

using Azure;
using Azure.Data.Tables;

namespace FoundrySharePointKnowledge.Domain.Search
{
    /// <summary>
    /// Represents an extraced document image storage in Azure Storage tables.
    /// </summary>
    public class ImageTableEntity : ITableEntity
    {
        #region Properties
        public ETag ETag { get; set ; }
        public string RowKey { get; set; }
        public string PartitionKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return $"{this.RowKey}@{this.PartitionKey}";
        }
        #endregion
    }
}
