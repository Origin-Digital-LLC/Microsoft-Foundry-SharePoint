using System;

namespace FoundrySharePointKnowledge.Domain.Foundry.VectorStores
{
    /// <summary>
    /// This holds everything needed to turn one source blob into markdown; the size and tag are carried from
    /// the container listing so the converter can reject and cache without a round trip of its own.
    /// </summary>
    public record MarkdownConversionRequest
    {
        #region Initialization
        public MarkdownConversionRequest(string containerName, string blobName, string markdownName, string sourceETag, long sourceSize)
        {
            //initialization
            this.SourceSize = sourceSize;
            this.SourceETag = sourceETag;
            this.BlobName = string.IsNullOrWhiteSpace(blobName) ? throw new ArgumentNullException(nameof(blobName)) : blobName;
            this.MarkdownName = string.IsNullOrWhiteSpace(markdownName) ? throw new ArgumentNullException(nameof(markdownName)) : markdownName;
            this.ContainerName = string.IsNullOrWhiteSpace(containerName) ? throw new ArgumentNullException(nameof(containerName)) : containerName;
        }
        #endregion
        #region Properties
        public string BlobName { get; init; }
        public long SourceSize { get; init; }
        public string SourceETag { get; init; }
        public string MarkdownName { get; init; }
        public string ContainerName { get; init; }
        #endregion
        #region Public Methods
        /// <summary>
        /// Returns a textual representation of an instance of this object.
        /// </summary>
        public override string ToString()
        {
            //return
            return this.BlobName ?? "N/A";
        }
        #endregion
    }
}
