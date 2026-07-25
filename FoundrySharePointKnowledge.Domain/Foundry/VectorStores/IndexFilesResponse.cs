using System.Text.Json.Serialization;

namespace FoundrySharePointKnowledge.Domain.Foundry.VectorStores
{
    /// <summary>
    /// Finishes an operation to index Foundry project files into a vector store.
    /// </summary>
    public record IndexFilesResponse
    {
        #region Initialization
        /// <summary>
        /// No wait.
        /// </summary>
        public IndexFilesResponse(string batchId)
        {
            //initialization
            this.BatchId = batchId;
        }

        /// <summary>
        /// Wait; this is also the constructor callers deserialize into, since a record with more than one
        /// constructor is ambiguous to System.Text.Json and Error is populated through its init accessor.
        /// </summary>
        [JsonConstructor()]
        public IndexFilesResponse(string batchId, int durationChecks, double durationMinutes) : this(batchId)
        {
            //initialization
            this.BatchId = batchId;
            this.DurationChecks = durationChecks;
            this.DurationMinutes = durationMinutes;
        }

        /// <summary>
        /// Error.
        /// </summary>
        public IndexFilesResponse(string batchId, string error) : this(batchId)
        {
            //initialization
            this.Error = error;
        }
        #endregion
        #region Properties
        public string Error { get; init; }
        public string BatchId { get; init; }
        public int DurationChecks { get; init; }
        public double DurationMinutes { get; init; }

        public bool IsError => !string.IsNullOrWhiteSpace(this.Error);
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return this.IsError ? this.Error : this.BatchId;
        }
        #endregion
    }
}
