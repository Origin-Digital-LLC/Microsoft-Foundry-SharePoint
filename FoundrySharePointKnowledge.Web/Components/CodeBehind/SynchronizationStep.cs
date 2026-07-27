using FoundrySharePointKnowledge.Domain.Tranches;

namespace FoundrySharePointKnowledge.Web.Components.CodeBehind
{
    /// <summary>
    /// This holds the context bound into a single Synchronization Manager accordion pane: the tranche being
    /// synchronized and the status that the pane advances it through.
    /// </summary>
    public class SynchronizationStep
    {
        #region Properties
        public TrancheStatus Status { get; }

        public TrancheTableEntity Tranche { get; }
        #endregion
        #region Initialization
        public SynchronizationStep(TrancheStatus status, TrancheTableEntity tranche)
        {
            //initialization
            this.Status = status;
            this.Tranche = tranche;
        }
        #endregion
        #region Public Methods
        /// <summary>
        /// Returns a textual representation of an instance of this object.
        /// </summary>
        public override string ToString()
        {
            //return
            return this.Status.ToString();
        }
        #endregion
    }
}
