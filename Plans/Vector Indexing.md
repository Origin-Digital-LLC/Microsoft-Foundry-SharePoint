# Vector Store Indexing

Now let's wire up the "Index" button in the "TrancheManager" component.

## Backend

This will be a mechanism to create a Microsoft Foundry vector store indexing operation and then poll it to update a real time progress bar.

### Components I have added
- FoundryService.IndexVectorStoreFilesAsync
- FoundryService.GetIndexOperationProgressAsync
- FoundryController.IndexVectorStoreFilesAsync (and route: /index-files)
- FoundryController.GetIndexOperationProgressAsync (and route: /index-files-progress)

Give these a quick code review.

### Other changes I have made

- Added a new enum value and step to the TrancheStatus: FilesIndexing (between FilesUploaded and FilesIndexed). 
- Added IndexedFileProgress to EditTrancheRequest and updated EditTrancheAsync accordingly.
- Added a new constant: `FSPKConstants.Blazor.Synchronization.IndexingStarted` = 0.0001.
- Added a new constant: `FSPKConstants.Blazor.Synchronization.CancelledProgress` = -2.
- Added a new constant: `FSPKConstants.Blazor.Synchronization.IndexCancelled` = "Indexing Cancelled."

### What you need to add

- Let's make the "edit tranche" flow (payload and backend) pieces able to update more of the Tranche Azure Table entity. 

## Frontend

These are the UI updates for you to make.

### Indexing Status

Make a new reusable Razor component called "IndexingStatus" with the following logic:
- It is bound to a `TrancheTableEntity`.
- Move the "Index" button from SynchronizationManager to this component.
- UI behavior:
  - If IndexedFileProgress == -1, show error text that says "Indexing Failed"
  - If IndexedFileProgress == -2, show error text that says "Indexing Cancelled"
  - If IndexedFileProgress >= 1, show success text that says "Indexing Completed"  
  - If TrancheStatus == FilesUploaded...
    - ...and IndexedFileProgress == 0: show the "Index" button. Add a callback to wire this button click to IndexFilesAsync.
    - ...otherwise, show text that says "Indexing started..."
  - If TrancheStatus == FilesIndexing, we need to set up an auto-polling mechanism.
    - Call /index-files-progress for the Tranche and track the resulting IndexProgressResponse in memory.
    - Render a progress bar that's bound to IndexProgressResponse.TotalProgress.
    - Then every 5 seconds, call /index-files-progress again and rehydrate the IndexProgressResponse to the progress bar automatically grows incrementally.
    - After each poll, call /edit to update the Tranche in Azure tables with the latest TotalProgress assigned to IndexFileProgress.
    - Keep polling until you reach a halting signal, which is IndexProgressResponse.Status != IndexStatus.InProgress. Here are the rules:
      - IndexProgressResponse.Status == IndexStatus.Failed: update the Tranche's status (in memory and in Azure Tables to the -1 "FailedProgress" constant). Show error text that says "Indexing Failed."
      - IndexProgressResponse.Status == IndexStatus.Cancelled: update the Tranche's status (in memory and in Azure Tables to the -2 "CancelledProgress" constant). Show error text that says "Indexing Cancelled."
      - IndexProgressResponse.Status == IndexStatus.Completed: update the Tranche's status (in memory and in Azure Tables to the 1 "CompletedProgress" constant). Show success text that says "Indexing Completed."

### Synchronization Manager

In SynchronizationManagerBase, we need to change the way we deal with IndexFilesAsync.
- I changed the "waitForCompletion" parameter to false on the IndexFilesRequest, so this call will now only start the indexing operation.
- After clicking the "Index" button the first time and IndexFilesAsync succeeds, in addition to the "advance the tranche in memory" bits, call /edit to update the tranche entity. Set the TrancheStatus to FilesIndexing and the IndexedFileProgress to `FSPKConstants.Blazor.Synchronization.IndexingStarted`.
- Replace the third pane content with an instance of IndexingStatus bound the the Tranche.
- Then add a new pane after this one (so the 4th of 5) to the accordion for the new FilesIndexing enum value.
  - Header: Indexing Progress
  - Content: An instance of IndexingStatus bound the the Tranche.
