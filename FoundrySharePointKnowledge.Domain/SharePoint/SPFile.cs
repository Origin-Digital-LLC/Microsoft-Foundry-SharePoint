namespace FoundrySharePointKnowledge.Domain.SharePoint
{
    /// <summary>
    /// This represents a raw SharePoint document.
    /// </summary>
    public class SPFile
    {
        #region Properties
        public string URL { get; set; }
        public string Name { get; set; }
        public string Title { get; set; }
        public string ItemId { get; set; }
        public string DriveId { get; set; }
        public string SecurityData { get; set; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            return this.URL;
        }
        #endregion
    }
}
