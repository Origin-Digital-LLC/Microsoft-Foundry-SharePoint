namespace FoundrySharePointKnowledge.Domain.Foundry
{
    /// <summary>
    /// This shapes the request to migrate Foundry agents.
    /// </summary>
    public record MigrateAgentsRequest
    {
        #region Properties
        public bool ForceChanges { get; init; }
        public string SourceProjectEndpoint { get; init; }
        public string DestinationKeyVaultURL { get; init; }
        public string SourceResourceGroupName { get; init; }
        public string DestinationProjectEndpoint { get; init; }
        public string DestinationResourceGroupName { get; init; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return $"{this.SourceProjectEndpoint}->{this.DestinationProjectEndpoint}";
        }
        #endregion
    }
}
