using System;
using System.Linq;
using System.Text.Json.Serialization;

using OpenAI.VectorStores;

#pragma warning disable OPENAI001

namespace FoundrySharePointKnowledge.Domain.Foundry.VectorStores
{
    /// <summary>
    /// This holds a vector store's server-side account of itself: the counts the service keeps and every file
    /// attached to it. These counts are the authority on what an indexing run actually landed, since anything
    /// this solution records alongside them is only ever a copy that can fall behind.
    /// </summary>
    public record VectorStoreDetail
    {
        #region Initialization
        /// <summary>
        /// Success.
        /// </summary>
        public VectorStoreDetail(VectorStore vectorStore, VectorStoreFile[] files)
        {
            //initialization
            ArgumentNullException.ThrowIfNull(vectorStore);
            this.Id = vectorStore.Id;
            this.Name = vectorStore.Name;
            this.CreatedAt = vectorStore.CreatedAt;
            this.UsageBytes = vectorStore.UsageBytes;

            //carry the service's own tally rather than counting the files, which a caller asking for the
            //store alone would have no way of reproducing
            this.Total = vectorStore.FileCounts?.Total ?? 0;
            this.Failed = vectorStore.FileCounts?.Failed ?? 0;
            this.Cancelled = vectorStore.FileCounts?.Cancelled ?? 0;
            this.Completed = vectorStore.FileCounts?.Completed ?? 0;
            this.InProgress = vectorStore.FileCounts?.InProgress ?? 0;

            //map the files, of which there may legitimately be none
            this.Files = files?.Select(file => new VectorStoreFileDetail(file)).ToArray() ?? Array.Empty<VectorStoreFileDetail>();
        }

        /// <summary>
        /// Failure; this is also the constructor callers deserialize into, since a record with more than one
        /// constructor is ambiguous to System.Text.Json and every other value is populated through its init
        /// accessor.
        /// </summary>
        [JsonConstructor()]
        public VectorStoreDetail(string error)
        {
            //initialization
            this.Error = error;
        }
        #endregion
        #region Properties
        public int Total { get; init; }
        public int Failed { get; init; }
        public string Id { get; init; }
        public int Cancelled { get; init; }
        public int Completed { get; init; }
        public int InProgress { get; init; }
        public string Name { get; init; }
        public string Error { get; init; }
        public long UsageBytes { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public VectorStoreFileDetail[] Files { get; init; }

        public bool IsError => !string.IsNullOrWhiteSpace(this.Error);
        #endregion
        #region Public Methods
        /// <summary>
        /// Returns a textual representation of an instance of this object.
        /// </summary>
        public override string ToString()
        {
            //return
            return this.Name ?? this.Id ?? "N/A";
        }
        #endregion
    }
}
