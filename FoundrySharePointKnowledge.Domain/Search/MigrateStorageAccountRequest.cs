namespace FoundrySharePointKnowledge.Domain.Search
{
    /// <summary>
    /// This represents a request to migrate blobs and tables from one Azure Storage Account to another.
    /// </summary>
    public record MigrateStorageAccountRequest
    {
        #region Properties
        public string[] TableNames { get; init; }
        public string[] ContainerNames { get; init; }
        public string SourceKeyVaultURL { get; init; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //initialization
            string tables = (this.TableNames?.Length ?? 0) == 0 ? "(N/A)" : string.Join(", ", this.TableNames);
            string containers = (this.ContainerNames?.Length ?? 0) == 0 ? "(N/A)" : string.Join(", ", this.ContainerNames);

            //return
            return $"migration of tables {tables} and containers {containers}";
        }
        #endregion
    }
}
