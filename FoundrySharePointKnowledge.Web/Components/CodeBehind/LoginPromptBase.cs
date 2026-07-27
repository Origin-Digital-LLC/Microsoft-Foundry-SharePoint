using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

using FoundrySharePointKnowledge.Common;

namespace FoundrySharePointKnowledge.Web.Components.CodeBehind
{
    /// <summary>
    /// This prompts an anonymous user to sign in rather than authenticating silently on the first render.
    /// </summary>
    public class LoginPromptBase : ComponentBase
    {
        #region Properties
        [Inject()]
        protected NavigationManager _navigationManager { get; set; }
        #endregion
        #region Protected Methods
        /// <summary>
        /// Sends the user through the interactive sign in flow, returning them to the page they requested.
        /// </summary>
        protected void Login()
        {
            //return
            this._navigationManager.NavigateToLogin(FSPKConstants.Routing.Blazor.Login);
        }
        #endregion
    }
}
