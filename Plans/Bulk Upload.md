# Bulk Upload

I'd like to add a new feature to the Blazor app that performs bulk uploads to an Azure Storage Account.

This will have many tasks.

The goal is to be able to handle a 1,000 file batch of PDFs as quickly and efficiently as possible.

## Backend

Every upload operation will create a new Azure Storage Blob Container in the storage account. Add methods to FoundryService that create a private container named "[Current User Name]-[Guid]" and provides progressive blob upload capabilities in bulk using the DI-provided `BlobServiceClient` (_blobClient).

Make a plan to support bi-directional communicate via UI polling to report on progress for each file. Also, determine if a batching strategy is useful - in other words, should the API handle individual files or groups of files?

Add a new "Upload" controller to the API project to connect to the front end. Here are the endpoints:
- POST /upload/start: starts a progressive upload. the payload should include all file names. this calls a method on FoundryService that creates the above Blob Container and returns the container name.
- PUT /upload/{containerName}/{fileName}: do we need one that uploads an individual file?
- GET /upload/progress/{fileName}: returns a nullalbe `double` percentage between zero and one based on the file's current upload progress. Return null if the file hasn't started yet or -1 to indicate an error. 
- GET /upload/cancel/{containerName}: uses cancellation tokens to stop all uploads.

Note: this API design is just from my experience with Azure STorage Blob Container progressive uploads. Please feel free to tweak this as you see fit to efficiently handle large batches of files - should the unit of work be individual files or groups of files? Then do what you have to do in FoundryService to implement this cleanly using established patterns.

## UI

Create a new authenticated page called Upload.razor following the existing Blazor code behind pattern. It should have a drop and drop zone that accepts files or folders. Make its placeholder say "Drop files or folders here".

After the drop, I want to see a flat table of all files; append the relative folder names for nested files like "[Folder]/[Sub Folder]/[File].[Extension]" so I can scroll and see them all.

Then, when files are present, show an "Upload" button with text that says "Upload [number of dropped files] Files" as well as a "Cancel" button next to it. Then show an overall progress bar under the buttons.
- Cancel's click action simply re-hides both buttons and clears the drop zone content, reverting it to the placeholder. It also stops any in progress uploads and calls /upload/cancel/{containerName} to stop any progressive uploads.
- Update's click action starts a progressive upload (/upload/start) with progress bars next to each file that are updated via /upload/progress/{fileName}. This should also disable the Upload button and drop zone. Once you design the file API and service shape, consume it here in the Blazor app.

Fan this out to agents as you see fit.

Thanks!