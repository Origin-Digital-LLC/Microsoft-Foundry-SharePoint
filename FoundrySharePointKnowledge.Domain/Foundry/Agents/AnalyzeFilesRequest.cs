namespace FoundrySharePointKnowledge.Domain.Foundry.Agents
{
    /// <summary>
    /// This holds the metadata needed to have an agent analyze files.
    /// </summary>
    public record AnalyzeFilesRequest
    {
        #region Initialization
        public AnalyzeFilesRequest(Agent agent, string containerName, string vectorStoreName, string filePrefix = null)
        {
            //initialization
            this.Agent = agent;
            this.FilePrefix = filePrefix;
            this.ContainerName = containerName;
            this.VectorStoreName = vectorStoreName;
        }
        #endregion
        #region Properties
        public Agent Agent { get; init; }
        public string FilePrefix { get; init; }
        public string ContainerName { get; init; }
        public string VectorStoreName { get; init; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //retun
            return $"Agent {this.Agent} will analyze files in container {this.ContainerName}.";
        }
        #endregion
    }
}
