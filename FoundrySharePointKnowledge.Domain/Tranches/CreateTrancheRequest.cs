namespace FoundrySharePointKnowledge.Domain.Tranches
{
    /// <summary>
    /// This holds the metadata needed to create a bulk upload tranche.
    /// </summary>
    public record CreateTrancheRequest
    {
        #region Initialization
        public CreateTrancheRequest(string name)
        {
            //initialization
            this.Name = name;
        }
        #endregion
        #region Properties
        public string Name { get; init; }
        #endregion
        #region Public Methods
        /// <summary>
        /// Returns a textual representation of an instance of this object.
        /// </summary>
        public override string ToString()
        {
            //return
            return $"Creation of tranche \"{this.Name}\".";
        }
        #endregion
    }
}
