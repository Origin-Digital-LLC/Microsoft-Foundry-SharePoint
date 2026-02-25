using System.Linq;

namespace FoundrySharePointKnowledge.Domain.Foundry
{
    /// <summary>
    /// This is a wrapper around a JSON array of engineer bios
    /// </summary>
    public class EngineerBiosWrapper
    {
        #region Properties
        public EngineerBio[] Engineers { get; set; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            if (this.Engineers?.Any() ?? false)
                return $"{this.Engineers.Length} eningeer(s)";
            else
                return "No engineers";
        }
        #endregion
    }
}
