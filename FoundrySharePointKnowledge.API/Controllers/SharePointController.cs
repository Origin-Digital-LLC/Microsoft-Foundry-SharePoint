using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;

using FoundrySharePointKnowledge.Common;
using FoundrySharePointKnowledge.Domain.Contracts;
using FoundrySharePointKnowledge.Domain.SharePoint;

namespace FoundrySharePointKnowledge.API.Controllers
{
    /// <summary>
    /// This handles all SharePoint integrations.
    /// </summary>
    public class SharePointController : BaseController<SharePointController>
    {
        #region Members
        private readonly ISharePointService _sharePointService;
        #endregion
        #region Initialization
        public SharePointController(ISearchService searchService,
                                    ILogger<SharePointController> logger,
                                    ISharePointService sharePointService) : base(logger, searchService)
        {
            //initialization
            this._sharePointService = sharePointService ?? throw new ArgumentNullException(nameof(sharePointService));
        }
        #endregion
        #region Endpoints
        [AllowAnonymous()]
        [HttpPost(FSPKConstants.Routing.API.Webook)]
        public async Task<IActionResult> WebhookAsync([FromQuery] string validationToken = null)
        {
            //initialization
            this._logger.LogInformation($"Handling request to {nameof(this.WebhookAsync)} from {this.HttpContext.Connection.RemoteIpAddress}.");

            //handle sharepoint validation handshake on subscription creation
            if (!string.IsNullOrWhiteSpace(validationToken))
                return this.Ok(validationToken);

            //get payload
            using StreamReader reader = new StreamReader(this.Request.Body);
            string body = await reader.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(body))
                throw new Exception("An empty SharePoint webhook body was detected.");

            //responses need to be sent back to sharepoint within 5 seconds, so always return 200 and then process the deltas in a different thread (which is fine here since webhooks are fire and forget)
            _ = Task.Run(async () =>
            {
                try
                {
                    //process payload
                    SPWebhookPayload payload = JsonSerializer.Deserialize<SPWebhookPayload>(body);
                    string result = await this._sharePointService.HandleWebhookAsync(payload);

                    //return
                    if (string.IsNullOrWhiteSpace(result))
                        this._logger.LogInformation($"Processed webhook {payload} successfully.");
                }
                catch (Exception ex)
                {
                    //error               
                    this._logger.LogError(ex, "Unable to handle SharePoint webhook.");
                }
            });

            //return
            return this.Ok();
        }
        #endregion
    }
}
