using System.Linq;
using System.Text.Json.Serialization;

using FoundrySharePointKnowledge.Common;

namespace FoundrySharePointKnowledge.Domain.Search.WebAPISkill
{
    /// <summary>
    /// This is the payload for a custom skill.
    /// </summary>
    public record WebAPISkillPayload<T>
    {
        #region Public Methods
        [JsonPropertyName(FSPKConstants.JsonPropertyNames.Values)]
        public WebAPISkillData<T>[] Values { get; init; }
        #endregion
        #region Public Methods        
        public override string ToString()
        {
            //return
            if (!this.Values?.Any() ?? true)
                return "N/A";
            else
                return string.Join("; ", this.Values.Select(v => $"{v.RecordId}: {v.Payload}"));
        }
        #endregion
    }
}
