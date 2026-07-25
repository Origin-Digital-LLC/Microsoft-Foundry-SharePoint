using System;
using System.Collections.Generic;

using FoundrySharePointKnowledge.Common;

namespace FoundrySharePointKnowledge.Domain.Tranches
{
    /// <summary>
    /// This holds the vector store file identifiers to record against a tranche's tracked files.
    /// </summary>
    public record UpdateFilesRequest
    {
        #region Initialization
        public UpdateFilesRequest(Guid trancheId, Dictionary<string, string> fileIds)
        {
            //initialization
            this.FileIds = fileIds;
            this.TrancheId = trancheId;
        }
        #endregion
        #region Properties
        public Guid TrancheId { get; init; }
        public Dictionary<string, string> FileIds { get; init; }
        #endregion
        #region Public Methods
        /// <summary>
        /// Returns a textual representation of an instance of this object.
        /// </summary>
        public override string ToString()
        {
            //return
            return $"{this.FileIds.Pluralize("file")} to update for tranche {this.TrancheId}.";
        }
        #endregion
    }
}
