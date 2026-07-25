# Vector Store Synchronize

Now let's wire up the "Synchronize" button in the "TrancheManager" component.

## Backend

This will create a Microsoft Foundry vector store synchronized from a Tranche's blob container.

### Changes I Made Without You

- Refactored some Foundry domain object namespaces into the "VectorStores" folder.
- Created new FoundryService methods and API endpoints for VectorStore uploads and indexing operations (UploadVectorStoreFilesAsync and IndexVectorStoreFilesAsync).

### Changes For You To Make

- Add a new property of type string called "VectorStoreId" to TrancheTableEntity and EditTrancheRequest.
- Create a new enum in Domain/Tranches called "TrancheStatus" with the following members and values:
  - Pending = 0
  - VectorStoreCreated = 1
  - FilesUploaded = 2
  - FilesIndexed = 3
- Add this enum as a new property called "Status" to the TrancheTableEntity and EditTrancheRequest.
- Add a new record under Domain/VectorStores called "ResetFilesRequest" that takes in a string VectorStoreId property.
- Add a new record under Domain/VectorStores called "ResetFilesResponse" that is currently empty.
- Add a new method to FoundryService named "RestFilesAsync" that takes in a ResetFilesRequest and returns a ResetFilesResponse. Just stub this method out for now, but call it from a new API endpoint named "ResetFiles" under the /reset-files route.
- Rename TrancheTableEntity.FileCount to "BlobFileCount."
- Rename TrancheTableEntity.TotalSize to "BlobTotalSize."
- Add a new integer property to TrancheTableEntity named "UploadedFileCount."
- Add a new double property to TrancheTableEntity named "UploadedFileSize."
- Add a new double property to TrancheTableEntity named "IndexedFileProgress."
- Add a new string property to TrancheFileTableEntity named "FileId."
- Add a new record under Domain/Tranches called "UpdateFilesRequest" that takes in a Guid TrancheId property and a `Dictionary<string, string>` property named FileIds.
- Add a new record under Domain/Tranches called "UpdateFilesResponse" that is currently empty.
- Add a new method to TrancheService called "UpdateFilesAsync" that accepts an UpdateFilesRequest parameter and returns a UpdateFilesResponse. This method should:
  - Pull all `TrancheFileTableEntity` from Azure Tables entities by the TrancheId partition key.
  - Loop through all FileIds and match the corresponding `TrancheFileTableEntity`. If one is found, update it's FileId with the value of corresponding key in FileIds. If not, log a warning.
  - Make this as parallel and efficient as possible.
- Wire up UpdateFilesAsync behind a new API endpoint in TrancheController under the route /update-tranche-files. 

## Frontend

Now we wire this all up in the UI.

### Accordion

Create a new reusable Blazor component called "Accordion" that renders a dynamic collection of collapsible panes as render fragments bound to a `Dictionary<string, object>` parameter.
- Each key is a dynamic header with a button that toggles the visibility if its render fragment. This visibility needs to be tracked in memory for each pane as a boolean where true is the "Open" (visible) state and false is the "Closed" (hidden) state.
- Each value is a .NET object that is bound to the render fragment so that the calling component can add Razor markup with HTML data binding against properties of that object.
- Default the first pane to be "Open" and the rest to be "Closed."
- Add a boolean parameter called "AutoClose" where, if set to true, only one pane can be open at a time; as the user toggles a pane to be open, the rest are automatically closed.

### Synchronization Manager

This is a new Blazor component that acts performs a synchronization of a blob container files to a vector store. The UI will guide the user through the process step-by-step where each step represents a progression "through" the TrancheStatus enum. 
- Each step is therefore mapped to a TrancheStatus enum value, so use the above "Accordion" control for this.
- Here is what we need in each resulting pane:
  1. Pending
    - Header: Vector Store
    - Content:
      - If the contextual Tranche's VectorStoreId is null or whitespace, render a "Create Vector Store" button. This click will call the /ensure-vector-store endpoint on the FoundryController to create a vector store using the Tranche's RowKey as the name. This endpoint will return the vector store id, which gets set back to the Tranche in memory. Finally, call the /edit endpoint on the TrancheController to set the TrancheStatus to "VectorStoreCreated" and populate the VectorStoreId property in the corresponding Tranches Azure Table record.
      - Otherwise, just render the text "Vector Store: {Tranche's VectorStoreId}."
  2. VectorStoreCreated
    - Header: Upload Files
    - Content:
      - If the contextual Tranche's UploadedFileCount is 0, render a form bound a new instance of UploadFilesRequest. This will have an optional textbox bound to the "Prefix" property above button labeled "Upload." 
        - This button click will call the /upload-files endpoint on the FoundryController with the UploadFilesRequest object as its payload, which should also carry the Tranche's TrancheId and VectorStoreId.
        - If the UploadFilesResponse has a null error...
          - Call the /edit endpoint on the TrancheController to update the Tranche's Azure table entity to have TrancheStatus=FilesUploaded, UploadedFileCount=(length of UploadFilesResponse.FileIds), and UploadedFileSize=UploadFilesResponse.TotalSize.
          - Call the /update-tranche-files endpoint on the TrancheController with the UploadFilesResponse.FileIds
      - Otherwise, just render the text "Files uploaded to AI: {Tranche's UploadedFileCount}."
  3. FilesUploaded
    - Header: Index Files
    - Content: 
      - If the contextual Tranche's IndexedFileProgress is 0, render a button labeled "Index." This click will call the /index-files endpoint on the FoundryController.
      - If the contextual Tranche's IndexedFileProgress is -1, render text saying "TODO: index failed"
      - If the contextual Tranche's IndexedFileProgress is >=1, render text saying "Indexing completed."
      - Otherwise, render text saying "TODO: index progress"
  4. FilesIndexed
    - Header: Reset Files
    - Content: Render a button labeled "Reset." This click will call the /reset-files endpoint on the FoundryController, guarded by another "Are You Sure" Modal like you did for Tranche deletion. Modal might need to be updated to support "nested" popups...

### Tranche Manager

Replace the "Synchronize" button in each row with a Modal component that pops up an instance of Synchronization Manager.

Fan this out to sub agents as you see fit. Thanks!