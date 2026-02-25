using System;

namespace FoundrySharePointKnowledge.Domain.SharePoint
{
    /// <summary>
    /// This represents an indexed SharePoint document.
    /// </summary>
    public record SPFileChunk : SPFile
    {
        #region Initialization
        public SPFileChunk() : base() 
        {
            //initialization
            this.Id = Guid.NewGuid();
        }
        public SPFileChunk(SPFile file, int pageNumber, string content, double[] titleVector, double[] contentVector) : this()
        {
            //initialization
            this.URL = file.URL;
            this.Name = file.Name;
            this.Title = file.Title;
            this.ItemId = file.ItemId;
            this.DriveId = file.DriveId;
            this.SecurityData = file.SecurityData;

            //return
            this.Content = content;
            this.PageNumber = pageNumber;
            this.TitleVector = titleVector;
            this.ContentVector = contentVector;
        }
        #endregion
        #region Properties
        public Guid Id { get; init; }
        public int PageNumber { get; init; }
        public string Content { get; init; }
        public double[] TitleVector { get; init; }
        public double[] ContentVector { get; init; }
        #endregion       
    }
}
