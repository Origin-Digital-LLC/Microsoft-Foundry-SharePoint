using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

using FoundrySharePointKnowledge.Common;

namespace FoundrySharePointKnowledge.Web.Components.CodeBehind
{
    /// <summary>
    /// This hosts the shell chrome: the signed in user's name, the navigation links, and the sign out action.
    /// </summary>
    public class MasterPageBase : LayoutComponentBase
    {
        #region Properties
        [Inject()]
        protected NavigationManager _navigationManager { get; set; }
        #endregion
        #region Protected Methods
        /// <summary>
        /// Sends the user through the sign out flow, which lands them back on the sign in prompt.
        /// </summary>
        protected void Logout()
        {
            //return
            this._navigationManager.NavigateToLogout(FSPKConstants.Routing.Blazor.Logout);
        }
        #endregion
    }
}
