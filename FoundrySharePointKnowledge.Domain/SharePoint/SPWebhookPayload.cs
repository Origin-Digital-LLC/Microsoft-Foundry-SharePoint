using System.Linq;
using System.Text.Json.Serialization;

using FoundrySharePointKnowledge.Common;

namespace FoundrySharePointKnowledge.Domain.SharePoint
{
    /// <summary>
    /// This represents the root payload of a SharePoint webhook notification.
    /// </summary>
    public record SPWebhookPayload
    {
        #region Properties
        [JsonPropertyName(FSPKConstants.JsonPropertyNames.Value)]
        public SPWebhookSubscription[] Value { get; init; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            if (!this.Value?.Any() ?? true)
                return "N/A";
            else
                return string.Join(", ", this.Value.Select(v => v.ToString()));
        }
        #endregion
    }
}
