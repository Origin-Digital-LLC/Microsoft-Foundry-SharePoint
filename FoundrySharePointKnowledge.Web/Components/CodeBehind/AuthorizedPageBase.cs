using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace FoundrySharePointKnowledge.Web.Components.CodeBehind
{
    /// <summary>
    /// This is the base class for pages that require an authenticated user.
    /// </summary>
    [Authorize()]
    public abstract class AuthorizedPageBase : ComponentBase { }
}
