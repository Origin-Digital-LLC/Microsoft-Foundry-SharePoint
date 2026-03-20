using System;
using System.Text.Json.Serialization;

using FoundrySharePointKnowledge.Common;

namespace FoundrySharePointKnowledge.Domain.SharePoint
{
    /// <summary>
    /// This represents a SharePoint webhook subscription notification.
    /// </summary>
    public record SPWebhookSubscription
    {
        #region Properties
        [JsonPropertyName(FSPKConstants.JsonPropertyNames.WebId)]
        public string WebId { get; init; }

        [JsonPropertyName(FSPKConstants.JsonPropertyNames.Resource)]
        public string ListId { get; init; }

        [JsonPropertyName(FSPKConstants.JsonPropertyNames.SiteUrl)]
        public string SiteURL { get; init; }

        [JsonPropertyName(FSPKConstants.JsonPropertyNames.TenantId)]
        public string TenantId { get; init; }

        [JsonPropertyName(FSPKConstants.JsonPropertyNames.ClientState)]
        public string WebhookSecret { get; init; }

        [JsonPropertyName(FSPKConstants.JsonPropertyNames.SubscriptionId)]
        public string SubscriptionId { get; init; }

        [JsonPropertyName(FSPKConstants.JsonPropertyNames.ExpirationDateTime)]
        public DateTimeOffset Expiration { get; init; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //initialization
            string webId = string.IsNullOrWhiteSpace(this.WebId) ? "N/A" : this.WebId;
            string listId = string.IsNullOrWhiteSpace(this.ListId) ? "N/A" : this.ListId;
            string siteURL = string.IsNullOrWhiteSpace(this.SiteURL) ? "N/A" : this.SiteURL;

            //return
            return $"Site={siteURL}; Web={webId}; List: {listId}";
        }
        #endregion
    }
}
