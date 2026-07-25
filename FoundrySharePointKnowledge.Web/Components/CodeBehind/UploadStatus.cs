namespace FoundrySharePointKnowledge.Web.Components.CodeBehind
{
    /// <summary>
    /// This describes the lifecycle state of a single file being uploaded.
    /// </summary>
    public enum UploadStatus
    {
        Pending = 0,
        Uploading = 1,
        Completed = 2,
        Failed = 3
    }
}
