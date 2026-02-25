using System.Text.Json.Serialization;

using FoundrySharePointKnowledge.Common;

namespace FoundrySharePointKnowledge.Domain.Search.WebAPISkill
{
    /// <summary>
    /// This is the wrapper for a web api skill payload.
    /// </summary>
    public record WebAPISkillData<T>
    {
        #region Properties
        [JsonPropertyName(FSPKConstants.JsonPropertyNames.RecordId)]
        public string RecordId { get; init; }

        [JsonPropertyName(FSPKConstants.JsonPropertyNames.Data)]
        public T Payload { get; set; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return $"{this.RecordId ?? "N/A"}={this.Payload?.ToString() ?? "N/A"}";
        }
        #endregion
    }
}
