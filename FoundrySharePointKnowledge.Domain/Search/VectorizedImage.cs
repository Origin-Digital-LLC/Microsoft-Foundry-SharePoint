namespace FoundrySharePointKnowledge.Domain.Search
{
    /// <summary>
    /// Represents an extraced document image.
    /// </summary>
    public record VectorizedImage : VectorizedFile
    {
        #region Properties
        public string Base64URL { get; init; }
        public float[] ImageVector { get; init; }
        public float[] ImageURLVector { get; init; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return this.URL;
        }
        #endregion
    }
}
