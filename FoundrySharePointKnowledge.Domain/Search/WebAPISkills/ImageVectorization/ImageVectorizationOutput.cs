namespace FoundrySharePointKnowledge.Domain.Search.WebAPISkill.ImageVectorization
{
    public record ImageVectorizationOutput
    {
        #region Properties
        public float[] Vector { get; set; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return this.Vector?.Length.ToString() ?? "N/A";
        }
        #endregion
    }
}
