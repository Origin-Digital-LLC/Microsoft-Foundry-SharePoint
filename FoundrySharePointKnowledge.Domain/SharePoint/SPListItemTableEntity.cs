using System;

using Azure;
using Azure.Data.Tables;

using FoundrySharePointKnowledge.Common;

namespace FoundrySharePointKnowledge.Domain.SharePoint
{
    /// <summary>
    /// Represents an extracted SharePoint list item in Azure Storage tables.
    /// </summary>
    public class SPListItemTableEntity : ITableEntity
    {
        #region Properties
        public ETag ETag { get; set; }
        public string URL { get; set; }
        public string Title { get; set; }
        public string SiteId { get; set; }
        public bool IsDeleted { get; set; }
        public string Description { get; set; }
        public DateTimeOffset? Timestamp { get; set; }

        /// <summary>
        /// SharePoint item id.
        /// </summary>
        public string RowKey { get; set; }

        /// <summary>
        /// SharePoint list id.
        /// </summary>
        public string PartitionKey { get; set; }

        public string UniqueId => FSPKUtilities.CreateUniqueId(this.SiteId, this.PartitionKey, this.RowKey);
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return this.RowKey;
        }
        #endregion
    }
}
