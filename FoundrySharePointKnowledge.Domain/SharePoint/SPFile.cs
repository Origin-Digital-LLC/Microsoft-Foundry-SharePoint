namespace FoundrySharePointKnowledge.Domain.SharePoint
{
    /// <summary>
    /// This represents a raw SharePoint document.
    /// </summary>
    public record SPFile
    {
        #region Properties
        public string URL { get; init; }
        public string Name { get; init; }
        public string Title { get; init; }
        public string ItemId { get; init; }
        public string DriveId { get; init; }
        public string SecurityData { get; init; }
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
