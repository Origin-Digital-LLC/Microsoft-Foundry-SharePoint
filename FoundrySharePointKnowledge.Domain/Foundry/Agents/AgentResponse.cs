using System;
using System.Linq;
using System.Collections.Generic;

using FoundrySharePointKnowledge.Domain.Foundry.Conversations;

namespace FoundrySharePointKnowledge.Domain.Foundry.Agents
{
    /// <summary>
    /// This represents a Foundry agent's response in a conversation.
    /// </summary>
    public record AgentResponse<T>
    {
        #region Initialization
        public AgentResponse(T message, string conversationId = null, IEnumerable<Annotation> annotations = null)
        {
            //initialization
            this.Message = message;
            this.ConversationId = conversationId;
            this.Annotations = annotations?.ToArray() ?? Array.Empty<Annotation>();
        }
        #endregion
        #region Properties
        public T Message { get; init; }
        public string ConversationId { get; init; }
        public Annotation[] Annotations { get; init; }
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
