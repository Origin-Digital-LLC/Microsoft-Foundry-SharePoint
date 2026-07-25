using System;

using OpenAI.VectorStores;

#pragma warning disable OPENAI001

namespace FoundrySharePointKnowledge.Domain.Foundry.VectorStores
{
    /// <summary>
    /// Holds metadata of an in-progress Microsoft Foundry indexing
    /// </summary>
    public record IndexProgressResponse
    {
        #region Initialization
        /// <summary>
        /// Success.
        /// </summary>
        public IndexProgressResponse(VectorStoreFileBatch batch)
        {
            //initialization
            ArgumentNullException.ThrowIfNull(batch);

            //map status
            if (batch.Status == VectorStoreFileBatchStatus.Failed)
                this.Status = IndexStatus.Failed;
            else if (batch.Status == VectorStoreFileBatchStatus.Cancelled)
                this.Status = IndexStatus.Cancelled;
            else if (batch.Status == VectorStoreFileBatchStatus.Completed)
                this.Status = IndexStatus.Completed;
            else
                this.Status = IndexStatus.InProgress;

            //return
            this.Total = batch.FileCounts?.Total ?? 0;
            this.Failed = batch.FileCounts?.Failed ?? 0;
            this.Cancelled = batch.FileCounts?.Cancelled ?? 0;
            this.Completed = batch.FileCounts?.Completed ?? 0;
            this.InProgress = batch.FileCounts?.InProgress ?? 0;
        }

        /// <summary>
        /// Error.
        /// </summary>
        public IndexProgressResponse(string error)
        {
            //initialization
            this.Error = error;
        }
        #endregion
        #region Propertes
        public string Error { get; init; }
        public double Total { get; init; }
        public double Failed { get; init; }
        public double Cancelled { get; init; }
        public double Completed { get; init; }
        public double InProgress { get; init; }
        public IndexStatus Status { get; init; }

        public bool IsError => !string.IsNullOrWhiteSpace(this.Error);
        public double TotalProgress => (this.Failed + this.Cancelled + this.Completed + this.InProgress) / this.Total;
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return this.IsError ? this.Error : this.TotalProgress.ToString("P1");
        }
        #endregion
    }
}
