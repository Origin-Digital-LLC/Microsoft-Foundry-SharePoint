using System.Globalization;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;

namespace FoundrySharePointKnowledge.Web.Components.CodeBehind
{
    /// <summary>
    /// This hosts a reusable modal dialog that toggles between a closed, inline trigger and an opened
    /// overlay containing caller-supplied header, main, and footer content.
    /// </summary>
    public class ModalBase : ComponentBase
    {
        #region Members
        private ModalState _state;
        private ModalState _previousState;
        #endregion

        #region Properties
        [Parameter()]
        public string Title { get; set; }

        [Parameter()]
        public double Width { get; set; } = 0.5;

        [Parameter()]
        public bool AutoClose { get; set; }

        [Parameter()]
        public string ButtonText { get; set; } = "Open";

        [Parameter()]
        public RenderFragment<EventCallback> ClosedContent { get; set; }

        [Parameter()]
        public RenderFragment HeaderContent { get; set; }

        [Parameter()]
        public RenderFragment MainContent { get; set; }

        [Parameter()]
        public RenderFragment<ModalFooterCallbacks> FooterContent { get; set; }

        protected bool IsOpen => this._state == ModalState.Open;

        protected string WidthPercentageStyle => string.Format(CultureInfo.InvariantCulture, "{0}%", this.Width * 100);

        protected EventCallback OpeningCallback => EventCallback.Factory.Create(this, this.HandleOpeningAsync);

        protected ModalFooterCallbacks FooterCallbacks => new ModalFooterCallbacks(EventCallback.Factory.Create(this, this.HandleOkAsync), EventCallback.Factory.Create(this, this.HandleCancelAsync));
        #endregion

        #region Events
        [Parameter()]
        public EventCallback Opening { get; set; }

        [Parameter()]
        public EventCallback Opened { get; set; }

        [Parameter()]
        public EventCallback Affirmed { get; set; }

        [Parameter()]
        public EventCallback Canceled { get; set; }

        [Parameter()]
        public EventCallback Closed { get; set; }
        #endregion

        #region Protected Methods
        /// <summary>
        /// Fires the Opened or Closed event once the corresponding state has finished rendering.
        /// </summary>
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            //guard
            if (this._previousState == this._state)
                return;

            //capture the transition before advancing it, then fire the matching event
            ModalState previousState = this._previousState;
            this._previousState = this._state;

            if (this._state == ModalState.Open)
                await this.Opened.InvokeAsync();
            else if (previousState == ModalState.Open)
                await this.Closed.InvokeAsync();
        }

        /// <summary>
        /// Opens the modal after notifying subscribers that it is about to open.
        /// </summary>
        protected async Task HandleOpeningAsync()
        {
            //notify, then open
            await this.Opening.InvokeAsync();
            this._state = ModalState.Open;
        }

        /// <summary>
        /// Raises the Affirmed event, then closes the modal.
        /// </summary>
        protected async Task HandleOkAsync()
        {
            //blocking notification, then close
            await this.Affirmed.InvokeAsync();
            this._state = ModalState.Closed;
        }

        /// <summary>
        /// Raises the Canceled event, then closes the modal.
        /// </summary>
        protected async Task HandleCancelAsync()
        {
            //blocking notification, then close
            await this.Canceled.InvokeAsync();
            this._state = ModalState.Closed;
        }

        /// <summary>
        /// Closes the modal with no further interaction when the backdrop is clicked and AutoClose is enabled.
        /// </summary>
        protected void HandleBackdropClick()
        {
            //guard
            if (!this.AutoClose)
                return;

            //close
            this._state = ModalState.Closed;
        }
        #endregion
    }
}
