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
        /// Wait.
        /// </summary>
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
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return string.IsNullOrWhiteSpace(this.Error) ? this.BatchId : this.Error;
        }
        #endregion
    }
}
