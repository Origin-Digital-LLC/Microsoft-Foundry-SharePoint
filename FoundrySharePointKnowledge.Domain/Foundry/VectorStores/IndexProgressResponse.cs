using System;
using System.Text.Json.Serialization;

using OpenAI.VectorStores;

#pragma warning disable OPENAI001

namespace FoundrySharePointKnowledge.Domain.Foundry.VectorStores
{
    /// <summary>
    /// Holds metadata of an in-progress Microsoft Foundry indexing, combining every batch the operation spans
    /// into a single set of counts so callers never have to know it was split up.
    /// </summary>
    public record IndexProgressResponse
    {
        #region Initialization
        /// <summary>
        /// Success.
        /// </summary>
        public IndexProgressResponse(VectorStoreFileBatch[] batches)
        {
            //initialization
            ArgumentNullException.ThrowIfNull(batches);
            bool isFailed = false;
            bool isCancelled = false;
            bool isInProgress = false;

            //add every batch's counts together, tracking which statuses turned up along the way
            foreach (VectorStoreFileBatch batch in batches)
            {
                //collect this batch's status
                if (batch.Status == VectorStoreFileBatchStatus.Failed)
                    isFailed = true;
                else if (batch.Status == VectorStoreFileBatchStatus.Cancelled)
                    isCancelled = true;
                else if (batch.Status != VectorStoreFileBatchStatus.Completed)
                    isInProgress = true;

                //collect this batch's counts
                this.Total += batch.FileCounts?.Total ?? 0;
                this.Failed += batch.FileCounts?.Failed ?? 0;
                this.Cancelled += batch.FileCounts?.Cancelled ?? 0;
                this.Completed += batch.FileCounts?.Completed ?? 0;
                this.InProgress += batch.FileCounts?.InProgress ?? 0;
            }

            //the operation is only as settled as its least settled batch, and an operation with no batches at
            //all has nothing left to wait for
            if (isInProgress)
                this.Status = IndexStatus.InProgress;
            else if (isFailed)
                this.Status = IndexStatus.Failed;
            else if (isCancelled)
                this.Status = IndexStatus.Cancelled;
            else
                this.Status = IndexStatus.Completed;
        }

        /// <summary>
        /// Error; this is also the constructor callers deserialize into, since a record with more than one
        /// constructor is ambiguous to System.Text.Json and every other value is populated through its
        /// init accessor.
        /// </summary>
        [JsonConstructor()]
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

        /// <summary>
        /// The share of every batch's files that have reached a terminal state; files still in progress are
        /// deliberately excluded, since a batch's total is the sum of every count and including them
        /// would report a full operation as complete the moment it started.
        /// </summary>
        public double TotalProgress => this.Total <= 0 ? 0 : (this.Failed + this.Cancelled + this.Completed) / this.Total;
        #endregion
        #region Public Methods
        /// <summary>
        /// Returns a textual representation of an instance of this object.
        /// </summary>
        public override string ToString()
        {
            //return
            return this.IsError ? this.Error : this.TotalProgress.ToString("P1");
        }
        #endregion
    }
}
