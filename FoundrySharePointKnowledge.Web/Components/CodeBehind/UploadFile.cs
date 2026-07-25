namespace FoundrySharePointKnowledge.Web.Components.CodeBehind
{
    /// <summary>
    /// This is the UI model for a single file selected for bulk upload. It is populated from
    /// JavaScript via JSInterop, so its property names map from the camelCase JSON sent by the browser.
    /// </summary>
    public class UploadFile
    {
        #region Properties
        public string Id { get; set; }
        public string Name { get; set; }
        public long Size { get; set; }
        public double Progress { get; set; }
        public string RelativePath { get; set; }
        public UploadStatus Status { get; set; }
        #endregion

        #region Public Methods
        /// <summary>
        /// Returns a textual representation of an instance of this object.
        /// </summary>
        public override string ToString()
        {
            //return
            return this.RelativePath ?? "N/A";
        }
        #endregion
    }
}
