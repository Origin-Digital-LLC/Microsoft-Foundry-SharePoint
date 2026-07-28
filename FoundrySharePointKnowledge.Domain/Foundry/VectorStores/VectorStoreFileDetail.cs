using System;
using System.Text.Json.Serialization;

using OpenAI.VectorStores;

#pragma warning disable OPENAI001

namespace FoundrySharePointKnowledge.Domain.Foundry.VectorStores
{
    /// <summary>
    /// This holds what a vector store knows about one of the files attached to it, flattened out of the SDK's
    /// own model so a caller reading this over the API never has to take a dependency on it.
    /// </summary>
    public record VectorStoreFileDetail
    {
        #region Initialization
        /// <summary>
        /// Maps one of a vector store's files.
        /// </summary>
        public VectorStoreFileDetail(VectorStoreFile file)
        {
            //initialization
            ArgumentNullException.ThrowIfNull(file);
            this.Size = file.Size;
            this.FileId = file.FileId;
            this.CreatedAt = file.CreatedAt;
            this.Error = file.LastError?.Message;

            //the store's own vocabulary for a file's state is the same set of outcomes a batch reports, so it
            //is carried as the status this solution already speaks rather than a second one beside it
            this.Status = file.Status switch
            {
                VectorStoreFileStatus.Failed => IndexStatus.Failed,
                VectorStoreFileStatus.Cancelled => IndexStatus.Cancelled,
                VectorStoreFileStatus.Completed => IndexStatus.Completed,
                _ => IndexStatus.InProgress
            };
        }

        /// <summary>
        /// This is the constructor callers deserialize into, since every value is populated through its init
        /// accessor and a record with more than one constructor is ambiguous to System.Text.Json.
        /// </summary>
        [JsonConstructor()]
        public VectorStoreFileDetail()
        {
        }
        #endregion
        #region Properties
        public int Size { get; init; }
        public string Error { get; init; }
        public string FileId { get; init; }
        public IndexStatus Status { get; init; }
        public DateTimeOffset CreatedAt { get; init; }

        public bool IsIndexed => this.Status == IndexStatus.Completed;
        #endregion
        #region Public Methods
        /// <summary>
        /// Returns a textual representation of an instance of this object.
        /// </summary>
        public override string ToString()
        {
            //return
            return this.FileId ?? "N/A";
        }
        #endregion
    }
}
