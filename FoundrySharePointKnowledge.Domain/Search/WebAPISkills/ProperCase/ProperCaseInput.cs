namespace FoundrySharePointKnowledge.Domain.Search.WebAPISkill.ProperCase
{
    public record ProperCaseInput
    {
        #region Properties
        public string RawText { get; init; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return this.RawText;
        }
        #endregion
    }
}
