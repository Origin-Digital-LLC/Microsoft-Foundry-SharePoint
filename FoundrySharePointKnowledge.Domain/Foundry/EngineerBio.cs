namespace FoundrySharePointKnowledge.Domain.Foundry
{
    /// <summary>
    /// This is an engineer bio populated from a Foundry workflow.
    /// </summary>
    public record EngineerBio
    {
        #region Properties
        public string Email { get; init; }
        public string PhotoURL { get; set; }
        public string FullName { get; init; }
        public string Experience { get; init; }
        public string PhotoDescription { get; set; }
        public string[] SolutionSkills { get; init; }
        public string[] TechnicalSkills { get; init; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return this.Email;
        }
        #endregion
    }
}
