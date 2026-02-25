using System;
using System.Linq;
using System.Collections.Generic;

namespace FoundrySharePointKnowledge.Domain.Foundry
{
    /// <summary>
    /// This represents a Foundry agent's response in a conversation.
    /// </summary>
    public record AgentResponse<T>
    {
        #region Initialization
        public AgentResponse(T message, string conversationId = null, IEnumerable<string> annotations = null)
        {
            //initialization
            this.Message = message;
            this.ConversationId = conversationId;
            this.Annotations = annotations?.ToArray() ?? Array.Empty<string>();
        }
        #endregion
        #region Properties
        public T Message { get; init; }
        public string[] Annotations { get; init; }
        public string ConversationId { get; init; }
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
