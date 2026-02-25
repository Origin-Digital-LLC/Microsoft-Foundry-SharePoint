namespace FoundrySharePointKnowledge.Domain.Search.WebAPISkill.ProperCase
{
    public record ProperCaseOutput
    {
        #region Properties
        public string ProperText { get; set; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return this.ProperText;
        }
        #endregion
    }
}
