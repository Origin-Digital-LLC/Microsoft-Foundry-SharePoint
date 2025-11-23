namespace FoundrySharePointKnowledge.Domain.Search
{
    /// <summary>
    /// Represents a chunk of data for search indexing.
    /// </summary>
    public class VectorizedChunk
    {
        #region Properties
        public string URL { get; set; }
        public string Title { get; set; }
        public string Chunk { get; set; }
        public string ChunkId { get; set; }
        public string ParentId { get; set; }
        public double[] TextVector { get; set; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return this.URL;
        }
        #endregion
    }
}
