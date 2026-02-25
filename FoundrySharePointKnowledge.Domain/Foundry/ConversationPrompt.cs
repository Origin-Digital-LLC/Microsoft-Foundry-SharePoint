namespace FoundrySharePointKnowledge.Domain.Foundry
{
    /// <summary>
    /// This represents a user talking to an agent in a conversation.
    /// </summary>
    public record ConversationPrompt : Prompt
    {
        #region Initialization
        public ConversationPrompt(Agent agent, string userMessage, string conversationId) : base(agent, userMessage)
        {
            //initialization
            this.ConversationId = conversationId;
        }
        #endregion
        #region Properties
        public string ConversationId { get; set; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return string.IsNullOrWhiteSpace(this.ConversationId) ? "New Conversation" : this.ConversationId;
        }
        #endregion
    }
}
