using System.Collections.Generic;

using Azure.AI.Projects.Agents;

namespace FoundrySharePointKnowledge.Domain.Foundry
{
    /// <summary>
    /// This holds the metadata needed to migrate an angent from one Foundry project to another.
    /// </summary>
    public record MigratableAgent
    {
        #region Initialization
        public MigratableAgent(AgentVersion agent)
        {
            //initialization
            this.Name = agent.Name;
            this.Definition = agent.Definition;
            this.Description = agent.Description;

            //return
            this.ToolNames = new HashSet<string>();
        }
        #endregion
        #region Properties
        public string Name { get; init; }
        public string Description { get; init; }
        public HashSet<string> ToolNames { get; init; }
        public AgentDefinition Definition { get; init; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return this.Name;
        }
        #endregion
    }
}
