namespace FoundrySharePointKnowledge.Domain.Foundry.VectorStores
{
    /// <summary>
    /// Tracks the status of a Foundry vector store indexing operation.
    /// </summary>
    public enum IndexStatus
    {
        Failed = 0,
        Cancelled = 1,
        Completed = 3,
        InProgress = 2
    }
}
