using System.ComponentModel.DataAnnotations;
using FoundrySharePointKnowledge.Common;

namespace FoundrySharePointKnowledge.Domain.Foundry.Agents
{
    /// <summary>
    /// These are the well-known Foundry agents.
    /// </summary>
    public enum Agent
    {
        None = -1,

        [Display(Name = FSPKConstants.Foundry.Agents.HR.DisplayName, ShortName = FSPKConstants.Foundry.Agents.HR.ShortName)]
        HR = 0,

        [Display(Name = FSPKConstants.Foundry.Workflows.ExpertiseFinder.DisplayName, ShortName = FSPKConstants.Foundry.Workflows.ExpertiseFinder.ShortName)]
        Bios = 1,

        [Display(Name = FSPKConstants.Foundry.Agents.CSVAnalyzer.DisplayName, ShortName = FSPKConstants.Foundry.Agents.CSVAnalyzer.ShortName)]
        CSVAnalyzer = 2,

        [Display(Name = FSPKConstants.Foundry.Agents.XMLAnalyzer.DisplayName, ShortName = FSPKConstants.Foundry.Agents.XMLAnalyzer.ShortName)]
        XMLAnalyzer = 3
    }
}
