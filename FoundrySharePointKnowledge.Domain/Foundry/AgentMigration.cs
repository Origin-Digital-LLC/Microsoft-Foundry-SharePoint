using System;
using System.Collections.Generic;
using System.Text;

namespace FoundrySharePointKnowledge.Domain.Foundry
{
    /// <summary>
    /// This holds the metadata needed to move an angent from one Foundry project to another.
    /// </summary>
    public record AgentMigration
    {
        #region Properties
        public float? TopP { get; init; }
        public float? Temperature { get; init; }
        #endregion
    }
}
