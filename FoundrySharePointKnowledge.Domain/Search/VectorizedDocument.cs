using System.Text.Json.Serialization;

using FoundrySharePointKnowledge.Common;

namespace FoundrySharePointKnowledge.Domain.Search
{
    /// <summary>
    /// This is a vectorized document returned from Document Intelligence.
    /// </summary>
    public class VectorizedDocument
    {
        #region Properties
        [JsonPropertyName(FSPKConstants.JsonPropertyNames.Object)]
        public string Type { get; set; }

        [JsonPropertyName(FSPKConstants.JsonPropertyNames.Index)]
        public int Index { get; set; }

        [JsonPropertyName(FSPKConstants.JsonPropertyNames.Embedding)]
        public double[] Embedding { get; set; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return this.Type;
        }
        #endregion
    }
}
