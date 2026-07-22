using Microsoft.AspNetCore.Components;

namespace FoundrySharePointKnowledge.Web.Components.CodeBehind
{
    /// <summary>
    /// This hosts the MSAL remote authentication operations.
    /// </summary>
    public class AuthenticationBase : ComponentBase
    {       
        #region Properties
        [Parameter()]
        public string Action { get; set; }
        #endregion       
    }
}
