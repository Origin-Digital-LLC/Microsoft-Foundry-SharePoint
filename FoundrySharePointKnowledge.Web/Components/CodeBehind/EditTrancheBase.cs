using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;

using FoundrySharePointKnowledge.Common;
using FoundrySharePointKnowledge.Domain.Tranches;

namespace FoundrySharePointKnowledge.Web.Components.CodeBehind
{
    /// <summary>
    /// This hosts a tranche's name in one of three modes: a readonly view, an editable modal that saves
    /// via the API, or a bare input bound to an in-memory draft for a tranche that has not yet been created.
    /// </summary>
    public class EditTrancheBase : ComponentBase
    {
        #region Members
        private string _editedName;
        #endregion

        #region Properties
        [Inject()]
        protected IHttpClientFactory _httpClientFactory { get; set; }

        [Parameter()]
        public EditTrancheMode Mode { get; set; }

        [Parameter()]
        public TrancheTableEntity Tranche { get; set; }

        [Parameter()]
        public TrancheDraft Draft { get; set; }

        [Parameter()]
        public EventCallback Updated { get; set; }

        protected string EditedName => this._editedName;
        #endregion

        #region Protected Methods
        /// <summary>
        /// Resets the in-progress edit to the tranche's currently saved name.
        /// </summary>
        protected void BeginEdit()
        {
            //return
            this._editedName = this.Tranche?.Name;
        }

        /// <summary>
        /// Records the in-progress edit for the tranche's name.
        /// </summary>
        protected void SetEditedName(string name)
        {
            //return
            this._editedName = name;
        }

        /// <summary>
        /// Updates the in-memory draft's name and notifies the parent, since the parent may depend on
        /// the draft's validity (e.g. to enable or disable other controls) and won't re-render on its own.
        /// </summary>
        protected async Task SetDraftNameAsync(string name)
        {
            //return
            this.Draft.Name = name;
            await this.Updated.InvokeAsync();
        }

        /// <summary>
        /// Saves the edited name and updates the tranche on success.
        /// </summary>
        protected async Task SaveAsync()
        {
            //call the api
            HttpClient client = this._httpClientFactory.CreateClient(nameof(FSPKConstants.Settings.Blazor.API));
            EditTrancheRequest request = new EditTrancheRequest(this.Tranche.TrancheId, this._editedName);
            HttpResponseMessage response = await client.PutAsJsonAsync($"{FSPKConstants.Routing.API.Tranche}/{FSPKConstants.Routing.API.EditTranche}", request);

            //update the tranche and notify the parent on success, since sibling components (e.g. a View-mode
            //instance showing the same tranche) won't re-render just because this instance's state changed
            if (response.IsSuccessStatusCode)
            {
                //notify
                this.Tranche.Name = request.Name;
                await this.Updated.InvokeAsync();
            }

            //return
            await this.InvokeAsync(StateHasChanged);
        }
        #endregion
    }
}
