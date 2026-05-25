using System;
using System.IO;
using System.Diagnostics;

using Microsoft.AspNetCore.Http;

using FoundrySharePointKnowledge.Common;

using OpenTelemetry;

namespace FoundrySharePointKnowledge.API.Middleware
{
    /// <summary>
    /// Exposes request bodies to Application Insights (via OpenTelemetry).
    /// </summary>
    public class RequestBodyTelemtryProcessor : BaseProcessor<Activity>
    {
        #region Members
        private readonly IHttpContextAccessor _httpContextAccessor;
        #endregion
        #region Initialization
        public RequestBodyTelemtryProcessor(IHttpContextAccessor httpContextAccessor)
        {
            //initialization
            this._httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }
        #endregion
        #region Public Methods
        /// <summary>
        /// In OnStart, the request body is still available.
        /// </summary>
        public override void OnStart(Activity activity)
        {
            //initialization
            HttpRequest request = this._httpContextAccessor.HttpContext?.Request;
            if (request == null || request.Method != FSPKConstants.HTTP.Post)
                return;

            //open request sream
            request.EnableBuffering();
            request.Body.Position = 0;
            using StreamReader streamReader = new StreamReader(request.Body, leaveOpen: true);

            //read body
            string body = streamReader.ReadToEndAsync().GetAwaiter().GetResult();
            request.Body.Position = 0;

            //truncate body
            int maxLength = FSPKConstants.OpenTelemetry.MaxBodyLength - 3;
            string truncatedBody = (body?.Length ?? 0) > maxLength ? $"{body.Substring(0, maxLength)}..." : body;

            //return
            activity.SetTag(FSPKConstants.OpenTelemetry.Tag, truncatedBody);
        }
        #endregion
    }
}