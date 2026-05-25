using System.Linq;
using System.Collections.Generic;

namespace FoundrySharePointKnowledge.Domain.Search
{
    /// <summary>
    /// This is the result of an Azure Storage migration.
    /// </summary>
    public record MigrateStorageAccountResult
    {
        #region Initialization
        public MigrateStorageAccountResult(IEnumerable<string> errors)
        {
            //initialization
            this.Errors = errors.ToArray();
        }
        #endregion
        #region Properties
        public string[] Errors { get; init; }

        public bool IsSuccessful => !this.Errors?.Any() ?? true;
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return this.IsSuccessful ? "Successful" : string.Join(", ", this.Errors);
        }
        #endregion
    }
}
