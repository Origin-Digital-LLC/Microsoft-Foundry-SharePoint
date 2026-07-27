namespace FoundrySharePointKnowledge.Domain.Tranches
{
    /// <summary>
    /// Identifies how far a bulk upload tranche has progressed through vector store synchronization.
    /// </summary>
    public enum TrancheStatus
    {
        Pending = 0,
        FilesIndexed = 3,
        FilesUploaded = 2,
        VectorStoreCreated = 1
    }
}
