using System;

namespace FoundrySharePointKnowledge.Domain.Upload
{
    /// <summary>
    /// This holds the editable metadata for a bulk upload tranche.
    /// </summary>
    public record EditTrancheRequest
    {
        #region Initialization
        public EditTrancheRequest(Guid trancheId, string name)
        {
            //initialization
            this.Name = name;
            this.TrancheId = trancheId;
        }
        #endregion
        #region Properties
        public string Name { get; init; }
        public Guid TrancheId { get; init; }
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
