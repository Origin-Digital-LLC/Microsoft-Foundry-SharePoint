using System;

using Azure;
using Azure.Data.Tables;

namespace FoundrySharePointKnowledge.Domain.SharePoint
{
    /// <summary>
    /// Represents an Graph SharePoint delta token in Azure Storage tables.
    /// </summary>
    public class SPDeltaTokenTableEntity : ITableEntity
    {
        #region Properties
        public ETag ETag { get; set; }
        public string Token { get; set; }
        public DateTimeOffset? Timestamp { get; set; }

        /// <summary>
        /// The SharePoint list id for this delta token
        /// </summary>
        public string RowKey { get; set; }

        /// <summary>
        /// The SharePoint site id for this delta token
        /// </summary>
        public string PartitionKey { get; set; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return $"Site={this.PartitionKey}; List={this.RowKey}";
        }
        #endregion
    }
}
