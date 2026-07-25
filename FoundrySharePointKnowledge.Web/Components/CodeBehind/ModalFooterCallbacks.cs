using Microsoft.AspNetCore.Components;

namespace FoundrySharePointKnowledge.Web.Components.CodeBehind
{
    /// <summary>
    /// This holds the callbacks a caller-supplied FooterContent render fragment can invoke to affirm or cancel the modal.
    /// </summary>
    public class ModalFooterCallbacks
    {
        #region Properties
        public EventCallback OnOk { get; }

        public EventCallback OnCancel { get; }
        #endregion

        #region Initialization
        public ModalFooterCallbacks(EventCallback onOk, EventCallback onCancel)
        {
            //initialization
            this.OnOk = onOk;
            this.OnCancel = onCancel;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Returns a textual representation of an instance of this object.
        /// </summary>
        public override string ToString()
        {
            //return
            return nameof(ModalFooterCallbacks);
        }
        #endregion
    }
}
