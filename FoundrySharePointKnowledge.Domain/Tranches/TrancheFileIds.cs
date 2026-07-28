using System.Collections.Generic;

namespace FoundrySharePointKnowledge.Domain.Tranches
{
    /// <summary>
    /// This holds the vector store file identifiers recorded against a tranche, split by whether each one
    /// still needs indexing; a tranche with nothing left to index is a very different thing from one with
    /// nothing uploaded at all, and only the counts tell them apart.
    /// </summary>
    public record TrancheFileIds
    {
        #region Initialization
        public TrancheFileIds(Dictionary<string, string> pendingFileIds, int indexedCount)
        {
            //initialization
            this.IndexedCount = indexedCount;
            this.PendingFileIds = pendingFileIds ?? new Dictionary<string, string>();
        }
        #endregion
        #region Properties
        public int IndexedCount { get; init; }
        public Dictionary<string, string> PendingFileIds { get; init; }
        public int PendingCount => this.PendingFileIds.Count;
        public int UploadedCount => this.PendingCount + this.IndexedCount;
        #endregion
        #region Public Methods
        /// <summary>
        /// Returns a textual representation of an instance of this object.
        /// </summary>
        public override string ToString()
        {
            //return
            return $"{this.PendingCount} of {this.UploadedCount} files left to index.";
        }
        #endregion
    }
}
