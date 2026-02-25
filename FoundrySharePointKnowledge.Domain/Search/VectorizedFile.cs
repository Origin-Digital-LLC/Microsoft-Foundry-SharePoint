namespace FoundrySharePointKnowledge.Domain.Search
{
    /// <summary>
    /// Represents an indexed file.
    /// </summary>
    public abstract record VectorizedFile
    {
        #region Properties
        public string URL { get; init; }
        public string Text { get; init; }
        public string Email { get; init; }
        public string[] Emails { get; init; }
        public string FullName { get; init; }        
        public string ContentId { get; init; }
        public string[] FullNames { get; init; }        
        public float[] TextVector { get; init; }
        public float[] FullNameVector { get; init; }
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
