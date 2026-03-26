using System.Collections.Generic;
using System.Linq;

namespace FoundrySharePointKnowledge.Domain.Foundry
{
    /// <summary>
    /// Tracks the results of a Foundry agent migration.
    /// </summary>
    public record MigrateAgentsResponse
    {
        #region Initialization
        public MigrateAgentsResponse()
        {
            //initialization
            this.Errors = new HashSet<string>();
            this.Warnings = new HashSet<string>();
            this.SuccessfulAgents = new HashSet<string>();
            this.SuccessfulModels = new HashSet<string>();
            this.SuccessfulWorkflows = new HashSet<string>();
            this.SuccessfulConnections = new HashSet<string>();
        }
        #endregion
        #region Properties
        public HashSet<string> Errors { get; init; }
        public HashSet<string> Warnings { get; init; }
        public HashSet<string> SuccessfulAgents { get; init; }
        public HashSet<string> SuccessfulModels { get; init; }
        public HashSet<string> SuccessfulWorkflows { get; init; }
        public HashSet<string> SuccessfulConnections { get; init; }

        public bool IsSuccessful => !this.Errors.Any() && (this.SuccessfulAgents.Any() || this.SuccessfulWorkflows.Any());
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return $"Successful agents: {this.SuccessfulAgents.Count}; Successful workflows: {this.SuccessfulWorkflows.Count}; Successful models: {this.SuccessfulModels.Count}; Successful connections: {this.SuccessfulConnections.Count}; Warnings: {this.Warnings.Count}; Errors: {this.Errors.Count}";
        }
        #endregion
    }
}
