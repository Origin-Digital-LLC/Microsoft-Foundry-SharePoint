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
        private UploadFilesRequest _uploadFilesRequest;
        private Dictionary<string, object> _panes = new Dictionary<string, object>();
        private Dictionary<string, string> _uploadedFileIds = new Dictionary<string, string>();
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
                { FSPKConstants.Blazor.Synchronization.UploadFiles, new SynchronizationStep(TrancheStatus.VectorStoreCreated, this.Tranche) },
                { FSPKConstants.Blazor.Synchronization.IndexFiles, new SynchronizationStep(TrancheStatus.FilesUploaded, this.Tranche) },
                { FSPKConstants.Blazor.Synchronization.ResetFiles, new SynchronizationStep(TrancheStatus.FilesIndexed, this.Tranche) }
            };

            //reset the transient state belonging to the previous tranche
            this._message = null;
            this._uploadedFileIds = new Dictionary<string, string>();
            this._uploadFilesRequest = new UploadFilesRequest(this.Tranche.TrancheId, this.Tranche.ContainerName, null, this.Tranche.VectorStoreId);
        }

        /// <summary>
        /// Creates the tranche's vector store, then records its identifier and advanced status against the tranche.
        /// </summary>
        protected async Task CreateVectorStoreAsync()
        {
            //call the api, naming the vector store after the tranche's row key
            this.BeginWork();
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
        /// Uploads the tranche's blobs into the Foundry project, then records the resulting file identifiers
        /// against both the tranche and each of its tracked files.
        /// </summary>
        protected async Task UploadFilesAsync()
        {
            //call the api
            this.BeginWork();
            HttpClient client = this._httpClientFactory.CreateClient(nameof(FSPKConstants.Settings.Blazor.API));
            HttpResponseMessage response = await client.PostAsJsonAsync($"{FSPKConstants.Routing.API.Foundry}/{FSPKConstants.Routing.API.UploadFiles}", this._uploadFilesRequest);

            //check the response
            if (!response.IsSuccessStatusCode)
            {
                //error
                await this.EndWorkAsync(await response.Content.ReadAsStringAsync());
                return;
            }

            //an upload that failed outright reports itself through the response's error
            UploadFilesResponse uploadFilesResponse = await response.Content.ReadFromJsonAsync<UploadFilesResponse>();
            if (uploadFilesResponse == null || !string.IsNullOrWhiteSpace(uploadFilesResponse.Error))
            {
                //error
                await this.EndWorkAsync(uploadFilesResponse?.Error);
                return;
            }

            //apply the upload to the tranche in memory, keeping the file identifiers for the indexing step
            this._uploadedFileIds = uploadFilesResponse.FileIds ?? new Dictionary<string, string>();
            this.Tranche.Status = TrancheStatus.FilesUploaded;
            this.Tranche.UploadedFileSize = uploadFilesResponse.TotalSize;
            this.Tranche.UploadedFileCount = this._uploadedFileIds.Count;

            //persist the tranche, then stamp each tracked file with its uploaded identifier
            await this.SaveTrancheAsync();
            UpdateFilesRequest updateFilesRequest = new UpdateFilesRequest(this.Tranche.TrancheId, this._uploadedFileIds);
            await client.PostAsJsonAsync($"{FSPKConstants.Routing.API.Tranche}/{FSPKConstants.Routing.API.UpdateTrancheFiles}", updateFilesRequest);

            //return
            await this.EndWorkAsync(null);
        }

        /// <summary>
        /// Indexes the tranche's uploaded files into its vector store.
        /// </summary>
        protected async Task IndexFilesAsync()
        {
            //the identifiers come from this session's upload, since they are not loaded back from the API
            if (this._uploadedFileIds.Count == 0)
            {
                //error
                this._message = FSPKConstants.Blazor.Synchronization.MissingFileIds;
                await this.InvokeAsync(StateHasChanged);
                return;
            }

            //call the api
            this.BeginWork();
            HttpClient client = this._httpClientFactory.CreateClient(nameof(FSPKConstants.Settings.Blazor.API));
            IndexFilesRequest indexFilesRequest = new IndexFilesRequest(this.Tranche.VectorStoreId, this._uploadedFileIds, false);
            HttpResponseMessage response = await client.PostAsJsonAsync($"{FSPKConstants.Routing.API.Foundry}/{FSPKConstants.Routing.API.IndexFiles}", indexFilesRequest);

            //check the response
            if (!response.IsSuccessStatusCode)
            {
                //error
                this.Tranche.IndexedFileProgress = FSPKConstants.Blazor.Synchronization.FailedProgress;
                await this.EndWorkAsync(await response.Content.ReadAsStringAsync());
                return;
            }

            //a failed indexing run reports itself through the response's error
            IndexFilesResponse indexFilesResponse = await response.Content.ReadFromJsonAsync<IndexFilesResponse>();
            if (indexFilesResponse?.IsError ?? true)
            {
                //error
                this.Tranche.IndexedFileProgress = FSPKConstants.Blazor.Synchronization.FailedProgress;
                await this.EndWorkAsync(indexFilesResponse?.Error);
                return;
            }

            //advance the tranche in memory, since indexing progress is not part of the tranche's editable metadata
            this.Tranche.Status = TrancheStatus.FilesIndexing;
            this.Tranche.IndexedFileProgress = FSPKConstants.Blazor.Synchronization.CompletedProgress;

            //return
            await this.EndWorkAsync(null);
        }

        /// <summary>
        /// Resets the files indexed into the tranche's vector store.
        /// </summary>
        protected async Task ResetFilesAsync()
        {
            //call the api
            this.BeginWork();
            HttpClient client = this._httpClientFactory.CreateClient(nameof(FSPKConstants.Settings.Blazor.API));
            ResetFilesRequest resetFilesRequest = new ResetFilesRequest(this.Tranche.VectorStoreId);
            HttpResponseMessage response = await client.PostAsJsonAsync($"{FSPKConstants.Routing.API.Foundry}/{FSPKConstants.Routing.API.ResetFiles}", resetFilesRequest);

            //return
            await this.EndWorkAsync(response.IsSuccessStatusCode ? null : await response.Content.ReadAsStringAsync());
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Marks a synchronization step as running and re-renders so its controls disable themselves.
        /// </summary>
        private void BeginWork()
        {
            //return
            this._isBusy = true;
            this._message = null;
        }

        /// <summary>
        /// Records a step's outcome, then re-renders this component and notifies the parent, since the parent's
        /// other rows and columns show the same tranche and will not re-render just because this state changed.
        /// </summary>
        private async Task EndWorkAsync(string error)
        {
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
            EditTrancheRequest request = new EditTrancheRequest(this.Tranche.TrancheId,
                                                               this.Tranche.Name,
                                                               this.Tranche.VectorStoreId,
                                                               this.Tranche.Status,
                                                               this.Tranche.UploadedFileCount,
                                                               this.Tranche.UploadedFileSize);

            //return
            await client.PutAsJsonAsync($"{FSPKConstants.Routing.API.Tranche}/{FSPKConstants.Routing.API.EditTranche}", request);
        }
        #endregion
    }
}
