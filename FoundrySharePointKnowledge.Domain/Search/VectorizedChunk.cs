namespace FoundrySharePointKnowledge.Domain.Search
{
    /// <summary>
    /// Represents a chunk of data for search indexing.
    /// </summary>
    public record VectorizedChunk : VectorizedFile
    {
        #region Properties
        public string DocumentId { get; init; }
        #endregion
    }
}
