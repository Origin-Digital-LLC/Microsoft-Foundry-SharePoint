namespace FoundrySharePointKnowledge.Domain.Search.WebAPISkill.ImageVectorization
{
    public record ImageVectorizationInput
    {
        #region Properties
        public string URL { get; init; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return this.URL;
        }
        #endregion
    }
}
