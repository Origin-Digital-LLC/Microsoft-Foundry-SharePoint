using System.Collections.Generic;

using Azure.AI.Projects.Agents;

namespace FoundrySharePointKnowledge.Domain.Foundry.Agents
{
    /// <summary>
    /// This holds the metadata needed to migrate an angent from one Foundry project to another.
    /// </summary>
    public record MigratableAgent
    {
        #region Initialization
        public MigratableAgent(ProjectsAgentVersion agent)
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
        public string ModelName { get; set; }
        public string Description { get; init; }
        public string WorkflowYaml { get; set; }
        public HashSet<string> ToolNames { get; init; }
        public ProjectsAgentDefinition Definition { get; init; }

        public bool IsWorkflow => !string.IsNullOrWhiteSpace(this.WorkflowYaml);
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
