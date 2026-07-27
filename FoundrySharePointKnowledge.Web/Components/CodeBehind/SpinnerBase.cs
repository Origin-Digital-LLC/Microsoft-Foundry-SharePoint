using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;

namespace FoundrySharePointKnowledge.Web.Components.CodeBehind
{
    /// <summary>
    /// This hosts a reusable spinner that overlays a loading indicator on top of caller-supplied content.
    /// Both templates stay rendered for the lifetime of the component and the spin state only toggles a CSS
    /// class, so the content is never torn down and any background work it owns keeps running while spinning.
    /// </summary>
    public class SpinnerBase : ComponentBase
    {
        #region Members
        private bool _isSpinning;
        #endregion
        #region Properties
        [Parameter()]
        public RenderFragment ChildContent { get; set; }

        [Parameter()]
        public RenderFragment Indicator { get; set; }

        public bool IsSpinning => this._isSpinning;
        #endregion
        #region Public Methods
        /// <summary>
        /// Reveals the loading indicator over the content.
        /// </summary>
        public async Task StartSpinningAsync()
        {
            //return
            await this.SetSpinStateAsync(true);
        }

        /// <summary>
        /// Hides the loading indicator, leaving the content interactive again.
        /// </summary>
        public async Task StopSpinningAsync()
        {
            //return
            await this.SetSpinStateAsync(false);
        }
        #endregion
        #region Protected Methods
        /// <summary>
        /// Returns the accessible busy state rendered onto the content area.
        /// </summary>
        protected string GetAriaBusy()
        {
            //return
            return this._isSpinning ? "true" : "false";
        }
        #endregion
        #region Private Methods
        /// <summary>
        /// Applies a spin state, redrawing on the renderer's thread so background callers are safe.
        /// </summary>
        private async Task SetSpinStateAsync(bool isSpinning)
        {
            //guard
            if (this._isSpinning == isSpinning)
                return;

            //return
            this._isSpinning = isSpinning;
            await this.InvokeAsync(StateHasChanged);
        }
        #endregion
    }
}
