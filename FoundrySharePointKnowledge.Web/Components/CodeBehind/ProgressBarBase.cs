using System;
using System.Globalization;

using Microsoft.AspNetCore.Components;

using FoundrySharePointKnowledge.Common;

namespace FoundrySharePointKnowledge.Web.Components.CodeBehind
{
    /// <summary>
    /// This hosts a reusable progress bar rendering a caller-supplied fraction of completed work, along with
    /// an optional caption describing it.
    /// </summary>
    public class ProgressBarBase : ComponentBase
    {
        #region Properties
        /// <summary>
        /// The share of the work that is complete, expressed as a fraction between zero and one.
        /// </summary>
        [Parameter()]
        public double Progress { get; set; }

        [Parameter()]
        public string Caption { get; set; }

        /// <summary>
        /// The caption shown while the caller has nothing of its own to say, which is typically the stretch
        /// between an operation starting and the first reading coming back from it.
        /// </summary>
        [Parameter()]
        public string DefaultCaption { get; set; }

        /// <summary>
        /// The supplied fraction as a whole percentage, clamped so a caller reporting partial or overrun work
        /// never renders a bar outside its track.
        /// </summary>
        protected double Percentage
        {
            get { return Math.Clamp(this.Progress * 100, 0, 100); }
        }

        /// <summary>
        /// The rendered bar's inline width; this is formatted invariantly, since a culture using a decimal
        /// comma would produce a width the browser discards.
        /// </summary>
        protected string Width
        {
            get { return this.Percentage.ToString("F2", CultureInfo.InvariantCulture); }
        }

        /// <summary>
        /// The accessible progress reported onto the rendered bar.
        /// </summary>
        protected string AriaValue
        {
            get { return this.Percentage.ToString("F0", CultureInfo.InvariantCulture); }
        }

        /// <summary>
        /// The supplied fraction as the whole percentage rendered beside the bar.
        /// </summary>
        protected string PercentageLabel
        {
            get { return string.Format(FSPKConstants.Blazor.ProgressLabelFormat, this.Percentage); }
        }

        /// <summary>
        /// The caption to render beneath the bar, falling back to the static one while the caller has supplied
        /// none of its own; a caller supplying neither renders no caption at all.
        /// </summary>
        protected string RenderedCaption
        {
            get { return string.IsNullOrWhiteSpace(this.Caption) ? this.DefaultCaption : this.Caption; }
        }
        #endregion
    }
}
