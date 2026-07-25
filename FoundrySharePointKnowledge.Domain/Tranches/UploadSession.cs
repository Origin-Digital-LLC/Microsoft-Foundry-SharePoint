using System;

namespace FoundrySharePointKnowledge.Domain.Tranches
{
    /// <summary>
    /// This holds the result of starting a bulk upload: the created container and a short-lived SAS URL
    /// the browser uses to upload blobs directly to Azure Storage.
    /// </summary>
    public record UploadSession
    {
        #region Initialization
        public UploadSession(string containerName, string sasURI, DateTimeOffset expiresOn, Guid trancheId)
        {
            //initialization
            this.SasURI = sasURI;
            this.TrancheId = trancheId;
            this.ExpiresOn = expiresOn;
            this.ContainerName = containerName;
        }
        #endregion
        #region Properties
        public string SasURI { get; init; }
        public Guid TrancheId { get; init; }
        public string ContainerName { get; init; }
        public DateTimeOffset ExpiresOn { get; init; }
        #endregion
        #region Public Methods
        /// <summary>
        /// Returns a textual representation of an instance of this object.
        /// </summary>
        public override string ToString()
        {
            //return
            return this.ContainerName ?? "N/A";
        }
        #endregion
    }
}
