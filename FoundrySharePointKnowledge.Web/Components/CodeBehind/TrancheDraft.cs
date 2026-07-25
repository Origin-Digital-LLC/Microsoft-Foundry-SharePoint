namespace FoundrySharePointKnowledge.Web.Components.CodeBehind
{
    /// <summary>
    /// This holds the in-memory metadata for a tranche that has not yet been created.
    /// </summary>
    public class TrancheDraft
    {
        #region Properties
        public string Name { get; set; }

        public bool IsValid => !string.IsNullOrWhiteSpace(this.Name);
        #endregion
        #region Public Methods
        /// <summary>
        /// Returns a textual representation of an instance of this object.
        /// </summary>
        public override string ToString()
        {
            //return
            return this.Name ?? "N/A";
        }
        #endregion
    }
}
