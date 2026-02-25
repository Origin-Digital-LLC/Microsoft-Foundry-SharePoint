using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

using FoundrySharePointKnowledge.Common;
using FoundrySharePointKnowledge.Domain.Settings;

namespace FoundrySharePointKnowledge.API.Utilities
{
    /// <summary>
    /// Handles JWT bearer token validation and signing key management.
    /// </summary>
    [Obsolete("This is no longer used. Leaving it here for posterity.")]
    public static class JWTUtilities
    {
        #region Members
        private static SecurityKey[] _cachedSigningKeys;
        private static DateTime _cacheExpiration = DateTime.MinValue;
        private static readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);
        private static readonly TimeSpan _cacheLifetime = TimeSpan.FromMinutes(FSPKConstants.Security.TokenExpirationMinutes + 1);
        #endregion
        #region Public Methods
        /// <summary>
        /// Configures JWT bearer authentication events.
        /// </summary>
        public static JwtBearerEvents ConfigureTokenValidation<T>(EntraIDSettings entraIDSettings)
        {
            //initialization
            TimeSpan tokenExpiration = TimeSpan.FromMinutes(FSPKConstants.Security.TokenExpirationMinutes);
            string issuer = $"{FSPKConstants.Security.TokenValidation.Issuer.CombineURL(entraIDSettings.TenantId.ToString())}/";

            //return
            return new JwtBearerEvents
            {
                //intercept message received to set validation parameters
                OnMessageReceived = async (context) =>
                {
                    //ignore anonymous requests
                    if (context.HttpContext.IsAnonymousRequest())
                        return;

                    //get signing keys
                    SecurityKey[] signingKeys = await JWTUtilities.GetCachedIssuerSigningKeysAsync(entraIDSettings);

                    //set validation parameters
                    context.Options.TokenValidationParameters = new TokenValidationParameters()
                    {
                        //assemble object
                        ValidIssuer = issuer,
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ClockSkew = tokenExpiration,
                        IssuerSigningKeys = signingKeys,
                        ValidateIssuerSigningKey = true,
                        ValidAudience = entraIDSettings.Scope
                    };                    
                },

                //refresh keys on authentication failure (handles key rotation)
                OnAuthenticationFailed = async (context) =>
                {
                    //log error
                    context.GetLogger<T>().LogError(context.Exception, $"Authentication failed for request {context.Request.GetDisplayUrl()}.");

                    //invalidate cache if token validation fails
                    if (context.Exception is SecurityTokenSignatureKeyNotFoundException)
                        await JWTUtilities.InvalidateCacheAsync();
                }
            };
        }
        #endregion
        #region Private Methods
        /// <summary>
        /// Skips token validation for anonymous requests.
        /// </summary>
        private static bool IsAnonymousRequest(this HttpContext context)
        {
            //return
            return context?.GetEndpoint()?.Metadata?.GetMetadata<IAllowAnonymous>() != null;
        }

        /// <summary>
        /// Gets a logger for the API entry point.
        /// </summary>
        private static ILogger<T> GetLogger<T>(this ResultContext<JwtBearerOptions> context)
        {
            //return
            return context.HttpContext.RequestServices.GetRequiredService<ILogger<T>>();
        }

        /// <summary>
        /// Invalidates the signing keys cache.
        /// </summary>
        private static async Task InvalidateCacheAsync()
        {
            //initialization
            await JWTUtilities._cacheLock.WaitAsync();

            try
            {
                //force refresh on next request
                JWTUtilities._cacheExpiration = DateTime.MinValue;
            }
            finally
            {
                //return
                JWTUtilities._cacheLock.Release();
            }
        }

        /// <summary>
        /// Gets cached signing keys or fetches fresh ones if cache expired.
        /// </summary>
        private static async Task<SecurityKey[]> GetCachedIssuerSigningKeysAsync(EntraIDSettings entraIDSettings)
        {
            //initialization
            if (JWTUtilities._cachedSigningKeys != null && DateTime.UtcNow < JWTUtilities._cacheExpiration)
                return JWTUtilities._cachedSigningKeys;

            //use lock to prevent multiple simultaneous fetches
            await JWTUtilities._cacheLock.WaitAsync();

            try
            {
                //double-check after acquiring lock
                if (JWTUtilities._cachedSigningKeys != null && DateTime.UtcNow < JWTUtilities._cacheExpiration)
                    return JWTUtilities._cachedSigningKeys;

                //fetch fresh keys
                JWTUtilities._cacheExpiration = DateTime.UtcNow.Add(JWTUtilities._cacheLifetime);
                JWTUtilities._cachedSigningKeys = await JWTUtilities.GetIssuerSigningKeysAsync(entraIDSettings.TenantId.ToString());

                //return
                return JWTUtilities._cachedSigningKeys;
            }
            finally
            {
                //clean up
                JWTUtilities._cacheLock.Release();
            }
        }

        /// <summary>
        /// Fetches issuer signing keys from the JWKS endpoint.
        /// </summary>
        private static async Task<SecurityKey[]> GetIssuerSigningKeysAsync(string tenantId)
        {
            //initialization
            string metadataAddress = FSPKConstants.Security.TokenValidation.Instance.CombineURL(tenantId).CombineURL(FSPKConstants.Security.TokenValidation.ConfigurationEndpoint);
            ConfigurationManager<OpenIdConnectConfiguration> openidConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(metadataAddress, new OpenIdConnectConfigurationRetriever(), new HttpDocumentRetriever());

            //return
            OpenIdConnectConfiguration config = await openidConfigurationManager.GetConfigurationAsync();
            return config.SigningKeys.ToArray();
        }
        #endregion
    }
}