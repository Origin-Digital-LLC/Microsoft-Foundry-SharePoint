using System;
using System.Net.Http;
using System.Threading;
using System.Net.Http.Json;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;

using FoundrySharePointKnowledge.Common;
using FoundrySharePointKnowledge.Domain.Tranches;

namespace FoundrySharePointKnowledge.Web.Components.CodeBehind
{
    /// <summary>
    /// This reports where a tranche stands in vector store indexing; the indexing runs on the API in the
    /// background, one batch of files at a time, so this polls the combined progress recorded against the
    /// tranche rather than any of the batches making it up.
    /// </summary>
    public class IndexingStatusBase : ComponentBase, IDisposable
    {
        #region Members
        private string _message;
        private bool _isDisposed;
        private Guid _polledTrancheId;
        private CancellationTokenSource _cancellation;
        #endregion
        #region Properties
        [Inject()]
        protected IHttpClientFactory _httpClientFactory { get; set; }

        [Parameter()]
        public TrancheTableEntity Tranche { get; set; }

        [Parameter()]
        public bool IsBusy { get; set; }

        protected string Message => this._message;

        /// <summary>
        /// Indicates whether the tranche's indexing is still running; the tranche's progress is the whole state
        /// machine, so indexing is running exactly while its progress sits between the value the API writes
        /// when it accepts the operation and the value it writes when the operation settles.
        /// </summary>
        protected bool IsIndexing
        {
            get { return this.Tranche != null && this.Tranche.IndexedFileProgress > 0 && this.Tranche.IndexedFileProgress < FSPKConstants.Blazor.Synchronization.CompletedProgress; }
        }

        /// <summary>
        /// Describes how much of the tranche has been indexed; the counts are derived from the progress, since
        /// the operation records one continuous fraction rather than each of its batches. This says nothing at
        /// all until the first batch has reported, leaving the bar's static caption to cover the wait.
        /// </summary>
        protected string ProgressCaption
        {
            get
            {
                //guard
                if (this.Tranche == null || this.Tranche.UploadedFileCount == 0)
                    return null;

                //the accepted operation writes its own progress before any batch of it has been read back
                if (this.Tranche.IndexedFileProgress <= FSPKConstants.Blazor.Synchronization.IndexingStarted)
                    return null;

                //return
                return string.Format(FSPKConstants.Blazor.Synchronization.IndexProgressFormat, (int)(this.Tranche.IndexedFileProgress * this.Tranche.UploadedFileCount), this.Tranche.UploadedFileCount);
            }
        }
        #endregion
        #region Events
        [Parameter()]
        public EventCallback IndexRequested { get; set; }

        [Parameter()]
        public EventCallback Updated { get; set; }
        #endregion
        #region Public Methods
        /// <summary>
        /// Cleans up object memory.
        /// </summary>
        public void Dispose()
        {
            //return
            this._isDisposed = true;
            this.StopPolling();
        }
        #endregion
        #region Protected Methods
        /// <summary>
        /// Starts polling once the supplied tranche is indexing, and stops polling as soon as it is not.
        /// </summary>
        protected override void OnParametersSet()
        {
            //drop the state belonging to a previous tranche
            if (this.Tranche != null && this._polledTrancheId != Guid.Empty && this._polledTrancheId != this.Tranche.TrancheId)
            {
                //reset
                this.StopPolling();
                this._message = null;
            }

            //only an operation that has been accepted and not yet settled can be polled
            if (!this.IsIndexing)
            {
                //return
                this.StopPolling();
                return;
            }

            //guard against a second loop for the tranche already being polled
            if (this._cancellation != null)
                return;

            //poll on a background loop, since the component renders while it runs
            this._polledTrancheId = this.Tranche.TrancheId;
            this._cancellation = new CancellationTokenSource();
            _ = this.PollProgressAsync(this._cancellation.Token);
        }

        /// <summary>
        /// Asks the parent to start the tranche's indexing.
        /// </summary>
        protected async Task RequestIndexingAsync()
        {
            //return
            this._message = null;
            await this.IndexRequested.InvokeAsync();
        }
        #endregion
        #region Private Methods
        /// <summary>
        /// Reads the progress recorded against the tranche every few seconds until the indexing settles.
        /// </summary>
        private async Task PollProgressAsync(CancellationToken cancellationToken)
        {
            try
            {
                //read until the indexing settles or this component goes away
                while (!cancellationToken.IsCancellationRequested)
                {
                    //wait first, since the API has only just accepted the operation and has nothing to add yet
                    await Task.Delay(FSPKConstants.Blazor.Synchronization.PollingWaitMilliseconds, cancellationToken);

                    //a request that could not be read at all is indistinguishable from failed indexing
                    TrancheProgressResponse progress = await this.GetProgressAsync(cancellationToken);
                    if (progress == null || progress.IsError)
                    {
                        //error
                        await this.FailAsync(progress?.Error ?? FSPKConstants.Blazor.Synchronization.IndexFailed);
                        return;
                    }

                    //the API owns this tranche's indexing, so take everything it reports rather than deriving it
                    this.Tranche.Status = progress.Status;
                    this.Tranche.IndexedFileProgress = progress.Progress;

                    //stop once the indexing has either finished or failed
                    if (!this.IsIndexing)
                    {
                        //return
                        this.StopPolling();
                        await this.NotifyAsync();

                        return;
                    }

                    //show this reading
                    await this.NotifyAsync();
                }
            }
            catch (OperationCanceledException)
            {
                //the component was disposed or rebound while polling, so there is nothing left to report
            }
            catch (Exception ex)
            {
                //error, unless this component is already gone and has nowhere to report it
                if (!this._isDisposed)
                    await this.FailAsync(ex.Message);
            }
        }

        /// <summary>
        /// Reads the current progress of the tranche's indexing.
        /// </summary>
        private async Task<TrancheProgressResponse> GetProgressAsync(CancellationToken cancellationToken)
        {
            //call the api
            HttpClient client = this._httpClientFactory.CreateClient(nameof(FSPKConstants.Settings.Blazor.API));
            string route = FSPKConstants.Routing.API.IndexProgress.Replace("{trancheId}", this.Tranche.TrancheId.ToString());
            HttpResponseMessage response = await client.GetAsync($"{FSPKConstants.Routing.API.Tranche}/{route}", cancellationToken);

            //return
            if (!response.IsSuccessStatusCode)
                return new TrancheProgressResponse(await response.Content.ReadAsStringAsync(cancellationToken));
            else
                return await response.Content.ReadFromJsonAsync<TrancheProgressResponse>(cancellationToken);
        }

        /// <summary>
        /// Records that the indexing could not be followed through to its end and stops polling it.
        /// </summary>
        private async Task FailAsync(string error)
        {
            //notify
            this.StopPolling();
            this._message = error;
            this.Tranche.IndexedFileProgress = FSPKConstants.Blazor.Synchronization.FailedProgress;

            //return
            await this.NotifyAsync();
        }

        /// <summary>
        /// Re-renders this component and notifies the parent, since its other controls show the same tranche.
        /// </summary>
        private async Task NotifyAsync()
        {
            //notify
            await this.Updated.InvokeAsync();

            //return
            await this.InvokeAsync(StateHasChanged);
        }

        /// <summary>
        /// Cancels and releases the polling loop, if one is running.
        /// </summary>
        private void StopPolling()
        {
            //guard
            if (this._cancellation == null)
                return;

            //return
            this._cancellation.Cancel();
            this._cancellation.Dispose();
            this._cancellation = null;
        }
        #endregion
    }
}
