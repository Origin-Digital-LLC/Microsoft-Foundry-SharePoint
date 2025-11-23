using System;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;

using FoundrySharePointKnowledge.Common;
using FoundrySharePointKnowledge.Domain.Contracts;

namespace FoundrySharePointKnowledge.API.Controllers
{
    /// <summary>
    /// This is the Foundry SharePoint Knowledge API.
    /// </summary>
#if DEBUG
    [AllowAnonymous()]
#else
    [Authorize()]
#endif
    [ApiController()]
    [Route(FSPKConstants.Routing.Controller)]
    public abstract class BaseController<T> : ControllerBase where T : BaseController<T>
    {
        #region Members
        protected readonly ILogger<T> _logger;        
        protected readonly ISearchService _searchService;
        #endregion
        #region Initialization
        public BaseController(ILogger<T> logger,
                              ISearchService searchService)
                                                              
        {
            //initialization
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this._searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));            
        }
        #endregion      
    }
}
