using System;
using System.Threading.Tasks;
using System.IdentityModel.Tokens.Jwt;

using Microsoft.Identity.Web;
using Microsoft.Identity.Client;
using Microsoft.Extensions.Logging;

using FoundrySharePointKnowledge.Common;
using FoundrySharePointKnowledge.Domain.Contracts;
using FoundrySharePointKnowledge.Domain.Foundry;

namespace FoundrySharePointKnowledge.Infrastructure.Services
{
    /// <summary>
    /// This manages Foundry tokens.
    /// </summary>
    public class TokenExchangeService : ITokenExchangeService
    {
        #region Members
        private readonly ITokenAcquisition _tokenAcquisition;
        private readonly ILogger<TokenExchangeService> _logger;
        #endregion
        #region Initialization
        public TokenExchangeService(ITokenAcquisition tokenAcquisition,
                                    ILogger<TokenExchangeService> logger)
        {
            //initialization
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this._tokenAcquisition = tokenAcquisition ?? throw new ArgumentNullException(nameof(tokenAcquisition));
        }
        #endregion
        #region Public Methods
        /// <summary>
        /// Exchanges the current user's token for one against the given scopes. If the scopes are excluded, it will default to Foundry's scope.
        /// </summary>
        public async Task<FoundryCredential> GetFoundryCredentialAsync(string userName, params string[] scopes)
        {
            //initialization
            string message = $" access token for user {userName} against: {string.Join(", ", scopes)}.";
            if ((scopes?.Length ?? 0) == 0)
                scopes = [FSPKConstants.Foundry.Scope];

            //exchange token
            this._logger.LogInformation($"Exchanging{message}");
            string token = await this._tokenAcquisition.GetAccessTokenForUserAsync(scopes);

            //check token
            JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(token))
            {
                //error
                string error = $"Failed to read{message}";
                this._logger.LogError(error);
                throw new Exception(error);
            }
            else
            {
                //get token expiration
                JwtSecurityToken jwt = handler.ReadJwtToken(token);
                TimeSpan expiration = jwt.ValidTo - DateTime.UtcNow.AddMinutes(FSPKConstants.Security.TokenValidation.ClockSkewMinutes);

                //check token expiration
                this._logger.LogInformation($"Acquired an{message}");
                if (expiration.TotalMinutes <= 0)
                {
                    //error
                    MsalUiRequiredException exception = new MsalUiRequiredException("invalid_token", "The access token expired.", null, UiRequiredExceptionClassification.AcquireTokenSilentFailed);
                    this._logger.LogError(exception, $"Acquired an expired{message}.");
                    throw exception;
                }
                else
                {
                    //return
                    return new FoundryCredential(token, expiration.TotalMinutes);
                }
            }
        }
        #endregion
    }
}