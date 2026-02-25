using System.Text.Json.Serialization;

using FoundrySharePointKnowledge.Common;

namespace FoundrySharePointKnowledge.Domain.Search
{
    /// <summary>
    /// Document intelligence usage.
    /// </summary>
    public class VectorizedUsage
    {
        #region Properties
        [JsonPropertyName(FSPKConstants.JsonPropertyNames.TotalTokens)]
        public int TotalTokens { get; set; }

        [JsonPropertyName(FSPKConstants.JsonPropertyNames.PromptTokens)]
        public int PromptTokens { get; set; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return this.TotalTokens.ToString();
        }
        #endregion
    }
}
