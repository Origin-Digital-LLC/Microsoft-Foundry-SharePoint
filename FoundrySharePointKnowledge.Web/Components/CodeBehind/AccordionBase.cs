using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.AspNetCore.Components;

namespace FoundrySharePointKnowledge.Web.Components.CodeBehind
{
    /// <summary>
    /// This hosts a reusable accordion that renders one collapsible pane per supplied header, binding
    /// each pane's caller-supplied content template against that pane's context object.
    /// </summary>
    public class AccordionBase : ComponentBase
    {
        #region Members
        private const string OpenIndicator = "▾";
        private const string ClosedIndicator = "▸";

        private bool _isInitialized;

        private readonly Dictionary<string, bool> _paneStates = new Dictionary<string, bool>();
        #endregion

        #region Properties
        [Parameter()]
        public Dictionary<string, object> Panes { get; set; }

        [Parameter()]
        public RenderFragment<object> PaneContent { get; set; }

        [Parameter()]
        public bool AutoClose { get; set; }

        protected string[] PaneHeaders => this.Panes == null ? Array.Empty<string>() : this.Panes.Keys.ToArray();
        #endregion

        #region Events
        [Parameter()]
        public EventCallback<string> PaneOpened { get; set; }

        [Parameter()]
        public EventCallback<string> PaneClosed { get; set; }
        #endregion

        #region Public Methods
        /// <summary>
        /// Indicates whether the pane with the supplied header is currently open.
        /// </summary>
        public bool IsOpen(string header)
        {
            //guard
            if (string.IsNullOrEmpty(header))
                return false;

            //return
            return this._paneStates.TryGetValue(header, out bool isOpen) && isOpen;
        }

        /// <summary>
        /// Returns the context object bound into the pane with the supplied header.
        /// </summary>
        public object GetPaneValue(string header)
        {
            //guard
            if (this.Panes == null || string.IsNullOrEmpty(header))
                return null;

            //return
            return this.Panes.TryGetValue(header, out object paneValue) ? paneValue : null;
        }
        #endregion

        #region Protected Methods
        /// <summary>
        /// Synchronizes the in-memory open and closed state with the current set of pane headers.
        /// </summary>
        protected override void OnParametersSet()
        {
            //synchronize
            this.SynchronizePaneStates();
        }

        /// <summary>
        /// Opens a closed pane or closes an open one, honoring the AutoClose single-pane restriction.
        /// </summary>
        protected async Task TogglePane(string header)
        {
            //guard
            if (string.IsNullOrEmpty(header) || !this._paneStates.ContainsKey(header))
                return;

            //invert the target pane, closing the others first when only one may be open
            bool isOpening = !this._paneStates[header];

            if (isOpening && this.AutoClose)
                this.CloseAllPanes();

            this._paneStates[header] = isOpening;

            //notify
            if (isOpening)
                await this.PaneOpened.InvokeAsync(header);
            else
                await this.PaneClosed.InvokeAsync(header);
        }

        /// <summary>
        /// Returns the accessible expanded state rendered onto a pane header button.
        /// </summary>
        protected string GetAriaExpanded(string header)
        {
            //return
            return this.IsOpen(header) ? "true" : "false";
        }

        /// <summary>
        /// Returns the chevron shown in a pane header for its current open or closed state.
        /// </summary>
        protected string GetIndicator(string header)
        {
            //return
            return this.IsOpen(header) ? AccordionBase.OpenIndicator : AccordionBase.ClosedIndicator;
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Adds newly supplied headers as closed, drops removed headers, and preserves the state of the
        /// headers that persisted so a caller's parameter change never discards the user's choices.
        /// </summary>
        private void SynchronizePaneStates()
        {
            //guard
            if (this.Panes == null || this.Panes.Count == 0)
            {
                this._paneStates.Clear();
                this._isInitialized = false;

                return;
            }

            //drop any header that is no longer supplied
            string[] removedHeaders = this._paneStates.Keys.Where(header => !this.Panes.ContainsKey(header)).ToArray();

            foreach (string removedHeader in removedHeaders)
                this._paneStates.Remove(removedHeader);

            //add any new header as closed, leaving the persisted headers untouched
            foreach (string header in this.Panes.Keys)
                if (!this._paneStates.ContainsKey(header))
                    this._paneStates[header] = false;

            //open the first pane only on the initial synchronization so later parameter changes never
            //reopen a pane the user deliberately collapsed
            if (this._isInitialized)
                return;

            this._isInitialized = true;
            this._paneStates[this.Panes.Keys.First()] = true;
        }

        /// <summary>
        /// Closes every pane.
        /// </summary>
        private void CloseAllPanes()
        {
            //close
            foreach (string header in this._paneStates.Keys.ToArray())
                this._paneStates[header] = false;
        }
        #endregion
    }
}
