using System.Text.Json.Serialization;

using FoundrySharePointKnowledge.Common;

namespace FoundrySharePointKnowledge.Domain.Search
{
    /// <summary>
    /// Document intelligence content.
    /// </summary>
    public class VectorizedContent
    {
        #region Properties
        [JsonPropertyName(FSPKConstants.JsonPropertyNames.Object)]
        public string Type { get; set; }

        [JsonPropertyName(FSPKConstants.JsonPropertyNames.Data)]
        public VectorizedDocument[] Data { get; set; }

        [JsonPropertyName(FSPKConstants.JsonPropertyNames.Model)]
        public string Model { get; set; }

        [JsonPropertyName(FSPKConstants.JsonPropertyNames.Usage)]
        public VectorizedUsage Usage { get; set; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return $"{this.Type}@{this.Model}";
        }
        #endregion
    }
}
