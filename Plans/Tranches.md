# Tranches

Now we need to keep track of files so the user can manage their containers. Each bulk upload is called a "Tranche" in this system.

## Schema

A tranche will be persisted across two Azure Tables. Create the following entities (each implementing `ITableEntity` in the Domain project) to represent records in these tables.

### Tranche Table
- PartitionKey: user name
- RowKey: a guid representing the tranche
- TrancheId: a guid readonly property that casts and returns the RowKey.

### Files Table
- PartitionLey: TrancheId
- RowKey: file name (as uploaded with preceding folder slashes for a flat unique file structure)

## Functionality
- Follow existing patterns to add a DI dependency on `TableServiceClient` to `FoundryService`.
- Update the `CreateUploadContainerAsync` method to also add a row to the Tranche table. Separate out the TrancheId (new guid) and track it as a separate property on `UploadSession`.
- Have the UI call a new API POST endpoint on the `UplaodController` with route '/upload/complete' that has a TrancheId and an array of string file names as its payload.
- Add a method to FoundryService called "TrackTrancheFilesAsync" that this endpoint calls. This method should add records to the Files table.

Thanks!
