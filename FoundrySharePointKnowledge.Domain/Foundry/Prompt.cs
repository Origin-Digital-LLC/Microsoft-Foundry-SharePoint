namespace FoundrySharePointKnowledge.Domain.Foundry
{
    /// <summary>
    /// This represents a user talking to an agent.
    /// </summary>
    public record Prompt
    {
        #region Initialization
        public Prompt(Agent agent, string userMessage)
        {
            //initialization
            this.Agent = agent;
            this.UserMessage = userMessage;
        }
        #endregion
        #region Properties
        public Agent Agent { get; init; }
        public string UserMessage { get; init; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return this.Agent.ToString();
        }
        #endregion
    }
}
