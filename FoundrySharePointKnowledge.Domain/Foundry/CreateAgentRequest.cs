namespace FoundrySharePointKnowledge.Domain.Foundry
{
    /// <summary>
    /// Holds the metadata to create a foundry agent.
    /// </summary>
    public record CreateAgentRequest
    {
        #region Properties
        public string Name { get; init; }
        public string Model { get; init; }
        public string Description { get; init; }
        public string Instructions { get; init; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return string.IsNullOrWhiteSpace(this.Name) ? "N/A" : this.Name;
        }
        #endregion
    }
}
