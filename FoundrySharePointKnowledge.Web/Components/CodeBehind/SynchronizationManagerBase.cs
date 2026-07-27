using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.AspNetCore.Components;

using FoundrySharePointKnowledge.Common;
using FoundrySharePointKnowledge.Domain.Tranches;
using FoundrySharePointKnowledge.Domain.Foundry.VectorStores;

namespace FoundrySharePointKnowledge.Web.Components.CodeBehind
{
    /// <summary>
    /// This walks a tranche through the vector store synchronization steps described by TrancheStatus: creating
    /// the vector store, uploading the tranche's blobs into the Foundry project, indexing them, and resetting them.
    /// </summary>
    public class SynchronizationManagerBase : ComponentBase
    {
        #region Members
        private bool _isBusy;
        private string _message;
        private Guid _paneTrancheId;
        private Spinner _activeSpinner;
        private UploadFilesRequest _uploadFilesRequest;
        private Dictionary<string, object> _panes = new Dictionary<string, object>();
        #endregion
        #region Properties
        [Inject()]
        protected IHttpClientFactory _httpClientFactory { get; set; }

        [Parameter()]
        public TrancheTableEntity Tranche { get; set; }

        protected bool IsBusy => this._isBusy;

        protected string Message => this._message;

        protected Dictionary<string, object> Panes => this._panes;

        /// <summary>
        /// The spinner covering the vector store creation button while its step runs.
        /// </summary>
        protected Spinner VectorStoreSpinner { get; set; }

        /// <summary>
        /// The spinner covering the file reset button while its step runs; this wraps the confirmation modal
        /// rather than living inside it, since a modal discards its trigger content while it is open.
        /// </summary>
        protected Spinner ResetFilesSpinner { get; set; }

        /// <summary>
        /// The optional blob name prefix limiting which of the tranche's files are uploaded; the bound request is
        /// a record, so each edit rebuilds it rather than mutating an init accessor.
        /// </summary>
        protected string FilePrefix
        {
            get { return this._uploadFilesRequest?.FilePrefix; }
            set { this._uploadFilesRequest = this._uploadFilesRequest == null ? null : this._uploadFilesRequest with { FilePrefix = value }; }
        }
        #endregion
        #region Events
        [Parameter()]
        public EventCallback Updated { get; set; }
        #endregion
        #region Protected Methods
        /// <summary>
        /// Rebuilds the accordion panes and the bound upload request whenever a different tranche is supplied.
        /// </summary>
        protected override void OnParametersSet()
        {
            //guard
            if (this.Tranche == null || this._paneTrancheId == this.Tranche.TrancheId)
                return;

            //bind one pane per synchronization step, each carrying the status it advances the tranche through
            this._paneTrancheId = this.Tranche.TrancheId;
            this._panes = new Dictionary<string, object>()
            {
                //assemble object
                { FSPKConstants.Blazor.Synchronization.VectorStore, new SynchronizationStep(TrancheStatus.Pending, this.Tranche) },
                { FSPKConstants.Blazor.Synchronization.ProcessFiles, new SynchronizationStep(TrancheStatus.VectorStoreCreated, this.Tranche) },
                { FSPKConstants.Blazor.Synchronization.IndexFiles, new SynchronizationStep(TrancheStatus.FilesUploaded, this.Tranche) },
                { FSPKConstants.Blazor.Synchronization.ResetFiles, new SynchronizationStep(TrancheStatus.FilesIndexed, this.Tranche) }
            };

            //reset the transient state belonging to the previous tranche
            this._message = null;
            this._uploadFilesRequest = new UploadFilesRequest(this.Tranche.TrancheId, this.Tranche.ContainerName, null, this.Tranche.VectorStoreId);
        }

        /// <summary>
        /// Creates the tranche's vector store, then records its identifier and advanced status against the tranche.
        /// </summary>
        protected async Task CreateVectorStoreAsync()
        {
            //call the api, naming the vector store after the tranche's row key
            await this.BeginWorkAsync(this.VectorStoreSpinner);
            HttpClient client = this._httpClientFactory.CreateClient(nameof(FSPKConstants.Settings.Blazor.API));
            string route = $"{FSPKConstants.Routing.API.Foundry}/{FSPKConstants.Routing.API.EnsureVectorStore}?name={Uri.EscapeDataString(this.Tranche.RowKey)}";
            HttpResponseMessage response = await client.PutAsync(route, null);

            //the endpoint returns the identifier as a bare string
            string vectorStoreId = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(vectorStoreId))
            {
                //error
                await this.EndWorkAsync(vectorStoreId);
                return;
            }

            //apply the new vector store to the tranche in memory, then persist it
            this.Tranche.VectorStoreId = vectorStoreId.Trim('"');
            this.Tranche.Status = TrancheStatus.VectorStoreCreated;
            this._uploadFilesRequest = this._uploadFilesRequest with { VectorStoreId = this.Tranche.VectorStoreId };

            //return
            await this.SaveTrancheAsync();
            await this.EndWorkAsync(null);
        }

        /// <summary>
        /// Starts uploading the tranche's blobs into the Foundry project; the upload runs on the API in the
        /// background, so this only marks it as started and leaves the rest to the polled progress.
        /// </summary>
        protected async Task UploadFilesAsync()
        {
            //call the api
            await this.BeginWorkAsync(null);
            HttpClient client = this._httpClientFactory.CreateClient(nameof(FSPKConstants.Settings.Blazor.API));
            HttpResponseMessage response = await client.PostAsJsonAsync($"{FSPKConstants.Routing.API.Foundry}/{FSPKConstants.Routing.API.UploadFiles}", this._uploadFilesRequest);

            //check the response
            if (!response.IsSuccessStatusCode)
            {
                //error
                await this.EndWorkAsync(await response.Content.ReadAsStringAsync());
                return;
            }

            //mark the upload as started in memory; the API has already recorded the same against the tranche,
            //so there is nothing to persist here and the status advances only once the upload finishes
            this.Tranche.UploadedFileProgress = FSPKConstants.Blazor.Synchronization.UploadStarted;

            //return
            await this.EndWorkAsync(null);
        }

        /// <summary>
        /// Starts indexing the tranche's uploaded files into its vector store; the indexing runs on the API in
        /// the background, one batch at a time, so this only marks it as started and leaves the rest to the
        /// polled progress.
        /// </summary>
        protected async Task IndexFilesAsync()
        {
            //call the api, which reads the files to index back from the tranche itself
            await this.BeginWorkAsync(null);
            HttpClient client = this._httpClientFactory.CreateClient(nameof(FSPKConstants.Settings.Blazor.API));
            IndexFilesRequest indexFilesRequest = new IndexFilesRequest(this.Tranche.TrancheId, this.Tranche.VectorStoreId);
            HttpResponseMessage response = await client.PostAsJsonAsync($"{FSPKConstants.Routing.API.Foundry}/{FSPKConstants.Routing.API.IndexFiles}", indexFilesRequest);

            //check the response
            if (!response.IsSuccessStatusCode)
            {
                //error
                await this.EndWorkAsync(await response.Content.ReadAsStringAsync());
                return;
            }

            //mark the indexing as started in memory; the API has already recorded the same against the tranche,
            //so there is nothing to persist here and the status advances only once the indexing finishes
            this.Tranche.IndexedFileProgress = FSPKConstants.Blazor.Synchronization.IndexingStarted;

            //return
            await this.EndWorkAsync(null);
        }

        /// <summary>
        /// Resets the files indexed into the tranche's vector store.
        /// </summary>
        protected async Task ResetFilesAsync()
        {
            //call the api
            await this.BeginWorkAsync(this.ResetFilesSpinner);
            HttpClient client = this._httpClientFactory.CreateClient(nameof(FSPKConstants.Settings.Blazor.API));
            ResetFilesRequest resetFilesRequest = new ResetFilesRequest(this.Tranche.VectorStoreId);
            HttpResponseMessage response = await client.PostAsJsonAsync($"{FSPKConstants.Routing.API.Foundry}/{FSPKConstants.Routing.API.ResetFiles}", resetFilesRequest);

            //return
            await this.EndWorkAsync(response.IsSuccessStatusCode ? null : await response.Content.ReadAsStringAsync());
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Marks a synchronization step as running so its controls disable themselves, and spins the supplied
        /// spinner over the control that started it; the steps whose progress is already polled and reported
        /// through a progress bar supply no spinner.
        /// </summary>
        private async Task BeginWorkAsync(Spinner spinner)
        {
            //mark the step as running
            this._isBusy = true;
            this._message = null;
            this._activeSpinner = spinner;

            //return
            if (spinner != null)
                await spinner.StartSpinningAsync();
        }

        /// <summary>
        /// Records a step's outcome, then re-renders this component and notifies the parent, since the parent's
        /// other rows and columns show the same tranche and will not re-render just because this state changed.
        /// </summary>
        private async Task EndWorkAsync(string error)
        {
            //stop spinning first, since an advanced status can take the spun control off the screen
            if (this._activeSpinner != null)
                await this._activeSpinner.StopSpinningAsync();

            this._activeSpinner = null;

            //notify
            this._isBusy = false;
            this._message = error;
            await this.Updated.InvokeAsync();

            //return
            await this.InvokeAsync(StateHasChanged);
        }

        /// <summary>
        /// Persists the tranche's synchronization metadata through the tranche editing endpoint.
        /// </summary>
        private async Task SaveTrancheAsync()
        {
            //call the api
            HttpClient client = this._httpClientFactory.CreateClient(nameof(FSPKConstants.Settings.Blazor.API));
            EditTrancheRequest request = new EditTrancheRequest(this.Tranche);

            //return
            await client.PutAsJsonAsync($"{FSPKConstants.Routing.API.Tranche}/{FSPKConstants.Routing.API.EditTranche}", request);
        }
        #endregion
    }
}
