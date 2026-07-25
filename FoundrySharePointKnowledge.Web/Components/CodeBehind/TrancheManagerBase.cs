using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.AspNetCore.Components;

using FoundrySharePointKnowledge.Common;
using FoundrySharePointKnowledge.Domain.Tranches;

namespace FoundrySharePointKnowledge.Web.Components.CodeBehind
{
    /// <summary>
    /// This hosts the tranche management table: loads the current user's tranches from the API on initial
    /// render, and lets them delete a tranche via a confirmation modal.
    /// </summary>
    public class TrancheManagerBase : ComponentBase
    {
        #region Members
        private TrancheTableEntity[] _tranches = Array.Empty<TrancheTableEntity>();
        #endregion

        #region Properties
        [Inject()]
        protected IHttpClientFactory _httpClientFactory { get; set; }

        protected IReadOnlyList<TrancheTableEntity> Tranches => this._tranches;
        #endregion

        #region Protected Methods
        /// <summary>
        /// Loads the current user's tranches as soon as the component is initialized.
        /// </summary>
        protected override async Task OnInitializedAsync()
        {
            //return
            await this.LoadTranchesAsync();
        }

        /// <summary>
        /// Deletes a tranche's container and table records, then removes its row from the table.
        /// </summary>
        protected async Task DeleteTrancheAsync(TrancheTableEntity tranche)
        {
            //call the api
            HttpClient client = this._httpClientFactory.CreateClient(nameof(FSPKConstants.Settings.Blazor.API));
            string route = FSPKConstants.Routing.API.DeleteTranche.Replace("{containerName}", tranche.ContainerName)
                                                                  .Replace("{trancheId}", tranche.TrancheId.ToString());
            HttpResponseMessage response = await client.DeleteAsync($"{FSPKConstants.Routing.API.Tranche}/{route}");

            //remove the row on success
            if (response.IsSuccessStatusCode)
                this._tranches = this._tranches.Where(t => t.TrancheId != tranche.TrancheId).ToArray();

            //return
            await this.InvokeAsync(StateHasChanged);
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Loads all tranches for the current user from the API.
        /// </summary>
        private async Task LoadTranchesAsync()
        {
            //call the api
            HttpClient client = this._httpClientFactory.CreateClient(nameof(FSPKConstants.Settings.Blazor.API));
            HttpResponseMessage response = await client.GetAsync($"{FSPKConstants.Routing.API.Tranche}/{FSPKConstants.Routing.API.Tranches}");

            //capture the result
            if (response.IsSuccessStatusCode)
                this._tranches = await response.Content.ReadFromJsonAsync<TrancheTableEntity[]>() ?? Array.Empty<TrancheTableEntity>();

            //return
            await this.InvokeAsync(StateHasChanged);
        }
        #endregion
    }
}
