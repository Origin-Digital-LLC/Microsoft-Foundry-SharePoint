using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

using FoundrySharePointKnowledge.Common;

namespace FoundrySharePointKnowledge.Web.Components.CodeBehind
{
    /// <summary>
    /// This enforces authentication.
    /// </summary>
    public class RedirectToLoginBase : ComponentBase
    {
        #region Members
        [Inject()]
        protected NavigationManager _navigationManager { get; set; }
        #endregion      
        #region Events
        protected override void OnInitialized()
        {
            //return
            this._navigationManager.NavigateToLogin(FSPKConstants.Routing.Blazor.Login);
        }
        #endregion
    }
}
