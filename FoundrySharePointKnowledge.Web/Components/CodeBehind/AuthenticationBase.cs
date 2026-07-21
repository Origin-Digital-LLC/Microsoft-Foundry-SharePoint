using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

using FoundrySharePointKnowledge.Common;

namespace FoundrySharePointKnowledge.Web.Components.CodeBehind
{
    /// <summary>
    /// This hosts the MSAL remote authentication operations.
    /// </summary>
    public class AuthenticationBase : ComponentBase
    {
        #region Members
        [Inject()]
        protected NavigationManager _navigationManager { get; set; }
        #endregion
        #region Properties
        [Parameter()]
        public string Action { get; set; }
        #endregion
        #region Protected Methods
        /// <summary>
        /// Handles a successful login.
        /// </summary>
        protected Task OnLogInSucceeded(RemoteAuthenticationState state)
        {
            //initialization
            this._navigationManager.NavigateTo(FSPKConstants.Routing.Blazor.Home, false);

            //return
            return Task.CompletedTask;
        }
        #endregion
    }
}
