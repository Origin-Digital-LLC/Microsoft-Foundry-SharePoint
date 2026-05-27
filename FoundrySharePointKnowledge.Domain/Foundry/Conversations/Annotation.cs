namespace FoundrySharePointKnowledge.Domain.Foundry.Conversations
{
    /// <summary>
    /// This is an annotation returned by a Foundry agent.
    /// </summary>
    public record Annotation
    {
        #region Initialization
        public Annotation(string title, string url)
        {
            //initialization
            this.Title = title;
            this.URL = url.ToLowerInvariant();
        }
        #endregion
        #region Properties
        public string URL { get; init; }
        public string Title { get; init; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return this.Title;
        }
        #endregion
    }
}