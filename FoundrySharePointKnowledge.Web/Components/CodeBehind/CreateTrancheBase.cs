using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;

using FoundrySharePointKnowledge.Common;
using FoundrySharePointKnowledge.Domain.Upload;

namespace FoundrySharePointKnowledge.Web.Components.CodeBehind
{
    /// <summary>
    /// This hosts the create-tranche page: files are dropped in the browser, a container plus SAS URL is
    /// requested from the API, and each file is uploaded directly to Azure Storage from the browser.
    /// </summary>
    public class CreateTrancheBase : AuthorizedPageBase, IAsyncDisposable
    {
        #region Members
        protected ElementReference _dropZone;

        private bool _uploading;
        private Guid _trancheId;
        private string _containerName;
        private double _overallProgress;
        private IJSObjectReference _module;
        private DotNetObjectReference<CreateTrancheBase> _selfRef;
        private List<UploadFile> _files = new List<UploadFile>();
        private readonly TrancheDraft _draft = new TrancheDraft();
        #endregion

        #region Properties
        [Inject()]
        protected IJSRuntime _jsRuntime { get; set; }

        [Inject()]
        protected IHttpClientFactory _httpClientFactory { get; set; }

        protected TrancheDraft Draft => this._draft;

        protected IReadOnlyList<UploadFile> Files => this._files;

        protected bool Uploading => this._uploading;

        protected bool HasFiles => this._files.Count > 0;

        protected double OverallProgress => this._overallProgress;

        protected int FileCount => this._files.Count;
        #endregion

        #region Public Methods
        /// <summary>
        /// Receives the files dropped in the browser and marks each one as pending.
        /// </summary>
        [JSInvokable()]
        public async Task OnFilesDropped(UploadFile[] droppedFiles)
        {
            //guard
            if (droppedFiles == null)
                return;

            //replace the working set
            this._files = new List<UploadFile>();

            foreach (UploadFile droppedFile in droppedFiles)
            {
                //mark pending
                droppedFile.Progress = 0;
                droppedFile.Status = UploadStatus.Pending;
                this._files.Add(droppedFile);
            }

            //reset overall progress
            this._overallProgress = 0;

            //return
            await this.InvokeAsync(StateHasChanged);
        }

        /// <summary>
        /// Records byte-level progress for a single file and recomputes overall progress.
        /// </summary>
        [JSInvokable()]
        public async Task OnFileProgress(string id, long loaded, long total)
        {
            //locate the file
            UploadFile file = this._files.FirstOrDefault(f => f.Id == id);

            if (file != null)
            {
                //update the file
                file.Status = UploadStatus.Uploading;
                file.Progress = total > 0 ? (double)loaded / total : 0;
            }

            //recompute overall progress
            this.RecomputeOverallProgress();

            //return
            await this.InvokeAsync(StateHasChanged);
        }

        /// <summary>
        /// Marks a single file as completed or failed.
        /// </summary>
        [JSInvokable()]
        public async Task OnFileComplete(string id, bool success)
        {
            //locate the file
            UploadFile file = this._files.FirstOrDefault(f => f.Id == id);

            if (file != null)
            {
                //update the file
                file.Status = success ? UploadStatus.Completed : UploadStatus.Failed;

                if (success)
                    file.Progress = 1;
            }

            //recompute overall progress
            this.RecomputeOverallProgress();

            //if the whole batch just finished with no failures, record the tranche's files, give the
            //user a moment to see the completed progress bar, then reset the page for the next batch
            if (this._files.All(f => f.Status == UploadStatus.Completed))
            {
                //return
                await this.InvokeAsync(StateHasChanged);
                await this.CompleteUploadAsync();
                await Task.Delay(FSPKConstants.AzureStorage.Blobs.UploadCompleteResetDelayMilliseconds);
                await this.ResetAsync();
                return;
            }

            //return
            await this.InvokeAsync(StateHasChanged);
        }
        #endregion

        #region Protected Methods
        /// <summary>
        /// Imports the JavaScript upload module and registers the drop zone on first render.
        /// </summary>
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            //only wire up once
            if (!firstRender)
                return;

            //import the module and register the drop zone
            this._selfRef = DotNetObjectReference.Create(this);
            this._module = await this._jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/upload.js");
            await this._module.InvokeVoidAsync("registerDropZone", this._dropZone, this._selfRef);
        }

        /// <summary>
        /// Requests a tranche container and SAS URL from the API and starts the direct-to-storage uploads.
        /// </summary>
        protected async Task CreateTrancheAsync()
        {
            //guard
            if (this._uploading || !this.HasFiles)
                return;

            //build the request
            CreateTrancheRequest request = new CreateTrancheRequest(this._draft.Name);

            //start the upload session
            HttpClient client = this._httpClientFactory.CreateClient(nameof(FSPKConstants.Settings.Blazor.API));
            HttpResponseMessage response = await client.PostAsJsonAsync($"{FSPKConstants.Routing.API.Tranche}/{FSPKConstants.Routing.API.Create}", request);

            if (!response.IsSuccessStatusCode)
            {
                //surface the API's error message instead of a generic status code
                string errorBody = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to create tranche ({(int)response.StatusCode} {response.ReasonPhrase}): {errorBody}");
            }

            UploadSession session = await response.Content.ReadFromJsonAsync<UploadSession>();

            //store session state and disable inputs
            this._uploading = true;
            this._trancheId = session.TrancheId;
            this._containerName = session.ContainerName;

            //hand off to the browser to upload each file directly to storage
            await this._module.InvokeVoidAsync("startUpload", session.SasURI, FSPKConstants.AzureStorage.Blobs.UploadConcurrency, this._selfRef);
        }

        /// <summary>
        /// Aborts in-flight uploads, deletes the container, and resets the page.
        /// </summary>
        protected async Task CancelAsync()
        {
            //abort any in-flight browser uploads
            if (this._module != null)
                await this._module.InvokeVoidAsync("cancelUpload");

            //delete the container if one was created
            if (!string.IsNullOrWhiteSpace(this._containerName))
            {
                //call the API
                HttpClient client = this._httpClientFactory.CreateClient(nameof(FSPKConstants.Settings.Blazor.API));
                await client.DeleteAsync($"{FSPKConstants.Routing.API.Tranche}/{FSPKConstants.Routing.API.Cancel.Replace("{containerName}", this._containerName)}");
            }

            //return
            await this.ResetAsync();
        }

        /// <summary>
        /// Resets the page back to its empty starting state, ready for the next batch.
        /// </summary>
        protected async Task ResetAsync()
        {
            //reset page state
            this._uploading = false;
            this._trancheId = Guid.Empty;
            this._overallProgress = 0;
            this._containerName = null;
            this._draft.Name = null;
            this._files = new List<UploadFile>();

            //return
            await this.InvokeAsync(StateHasChanged);
        }

        /// <summary>
        /// Cleans up object memory.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            //dispose the module
            if (this._module != null)
                await this._module.DisposeAsync();

            //dispose the self reference
            if (this._selfRef != null)
                this._selfRef.Dispose();
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Reports the completed tranche's files to the API for tracking.
        /// </summary>
        private async Task CompleteUploadAsync()
        {
            //guard
            if (this._trancheId == Guid.Empty)
                return;

            //build the request using the relative paths so folder structure is preserved
            string[] fileNames = this._files.Select(f => f.RelativePath).ToArray();
            CompleteUploadRequest request = new CompleteUploadRequest(this._trancheId, this._containerName, fileNames);

            //notify the api
            HttpClient client = this._httpClientFactory.CreateClient(nameof(FSPKConstants.Settings.Blazor.API));
            await client.PostAsJsonAsync($"{FSPKConstants.Routing.API.Tranche}/{FSPKConstants.Routing.API.Complete}", request);
        }

        /// <summary>
        /// Recomputes overall progress as the ratio of bytes uploaded across all files.
        /// </summary>
        private void RecomputeOverallProgress()
        {
            //guard
            if (this._files.Count == 0)
            {
                //nothing to do
                this._overallProgress = 0;
                return;
            }

            //sum bytes across all files
            double totalBytes = this._files.Sum(f => (double)f.Size);
            double loadedBytes = this._files.Sum(f => f.Progress * f.Size);

            //return
            this._overallProgress = totalBytes > 0 ? loadedBytes / totalBytes : 0;
        }
        #endregion
    }
}
