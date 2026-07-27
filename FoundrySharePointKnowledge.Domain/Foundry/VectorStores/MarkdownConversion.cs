using System;

namespace FoundrySharePointKnowledge.Domain.Foundry.VectorStores
{
    /// <summary>
    /// This holds the markdown one source blob was converted into, or the reason that file could not be
    /// converted; a file that fails here is reported against its tranche rather than uploaded as-is.
    /// </summary>
    public record MarkdownConversion
    {
        #region Initialization
        /// <summary>
        /// Success.
        /// </summary>
        public MarkdownConversion(string sourceName, string markdownName, BinaryData content, bool wasCached)
        {
            //initialization
            this.Content = content;
            this.WasCached = wasCached;
            this.SourceName = sourceName;
            this.MarkdownName = markdownName;
        }

        /// <summary>
        /// Failure.
        /// </summary>
        public MarkdownConversion(string sourceName, string error)
        {
            //initialization
            this.Error = error;
            this.SourceName = sourceName;
        }
        #endregion
        #region Properties
        public string Error { get; init; }
        public bool WasCached { get; init; }
        public BinaryData Content { get; init; }
        public string SourceName { get; init; }
        public string MarkdownName { get; init; }
        public int Size => this.Content?.ToMemory().Length ?? 0;
        public bool IsSuccessful => string.IsNullOrWhiteSpace(this.Error) && this.Content != null;
        #endregion
        #region Public Methods
        /// <summary>
        /// Returns a textual representation of an instance of this object.
        /// </summary>
        public override string ToString()
        {
            //return
            return this.SourceName ?? "N/A";
        }
        #endregion
    }
}
