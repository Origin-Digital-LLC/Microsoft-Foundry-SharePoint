# Tranche Manager

Next we need a feature that lets the user manage their Tranches.

## Tranche Update

First, add more metadata to the Tranche entity that will be updated in Azure Tables after an upload.
- Name (string): defaults to "New Tranche {current date}"
- FileCount (int): the number of files in the Tranche
- TotalSize (double): the combined size of all files in the Tranche.

Then populate these fields in `TrackTrancheFilesAsync` for an existing new Tranche. We will need to decide how get the total size efficiently by querying the blobs and possibly adding FileSize to the `TrancheFileTableEntity` class.

## Backend

### Load
- Create a new method on `FoundryService` named "LoadTranches" that returns an array of Tranches for the current user by querying the Tranches Azure Table.
- Add a GET endpoint to the UploadController to wire this up.

### Delete
- Create a new method on `FoundryService` named "DeleteTranche" that deletes the blob container and all partitions in both Azure Tables - all keyed off of a container name and TrancheId.
- Add a DELETE endpoint to the UploadController to wire this up.

## UI

On the Blazor home page, add a new Razor component called "TrancheManager" that loads all tranches for the current user via the "Tranches" table. On component load, call the "LoadTranches" endpoint and render automatically.

Each tranche should be a row in a table with the following columns:
- Name
- Timestamp
- File Count
- Total Size
- Synchronize button
- Edit button
- Delete button

### Synchronize Button Click

Will be handled later

### Edit Button Click

Will be handled later

### Delete Button Click

This will actually use the Modal component with no AutoClose to render the button as the "ClosedContent" - then when clicked, it shows as basic "Are you Sure?" UI in the HeaderContent and MainContent.
The FooterContent will show "Yes" and "No" buttons. Yes calls the above "DeleteTranche" endpoint and then removes the corresponding row from the table.

Thanks!
