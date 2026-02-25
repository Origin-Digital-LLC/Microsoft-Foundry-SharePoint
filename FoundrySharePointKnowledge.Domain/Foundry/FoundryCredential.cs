using System;
using System.Threading;
using System.Threading.Tasks;

using Azure.Core;

namespace FoundrySharePointKnowledge.Domain.Foundry
{
    /// <summary>
    /// This wraps an auth token in a credential for use in Foundry.
    /// </summary>
    public class FoundryCredential : TokenCredential
    {
        #region Members
        private readonly string _token;
        private readonly double _expirationMinutes;
        #endregion
        #region Initialization
        public FoundryCredential(string token, double expirationMinutes)
        {
            //initialization
            this._token = token;
            this._expirationMinutes = expirationMinutes;
        }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return $"Token expires in {this._expirationMinutes} minutes.";
        }

        /// <summary>
        /// Get token sychronously.
        /// </summary>
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            //return
            return new AccessToken(this._token, DateTimeOffset.UtcNow.AddHours(this._expirationMinutes));
        }

        /// <summary>
        /// Get token asychronously.
        /// </summary>
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            //return
            return new ValueTask<AccessToken>(new AccessToken(this._token, DateTimeOffset.UtcNow.AddHours(this._expirationMinutes)));
        }
        #endregion
    }
}
