// Direct-to-Azure-Storage bulk upload helper for the Blazor Create Tranche page.
// The browser reads dropped files/folders, uploads each blob directly to Azure Storage using a
// container SAS URL, and reports byte-level progress back to .NET.

// Module-level state.
const fileMap = new Map();      // id -> { file, relativePath }
let activeXhrs = [];            // in-flight XMLHttpRequest instances
let dotNetRef = null;           // reference to the .NET CreateTrancheBase component
let isFollowing = true;         // whether the file list should keep the batch's current row in view
let lastFollowScrollTop = 0;    // the scroll position this module last set, to tell its own scrolling apart

// how far the list may drift from this module's own scrolling before a scroll counts as the user's
const followToleranceInPixels = 4;

/**
 * Registers drag-and-drop handlers on the drop zone element.
 */
export function registerDropZone(dropZoneEl, dotNetReference)
{
    dotNetRef = dotNetReference;

    // A drag the page never cancels is a navigation: the browser opens the dropped file itself, losing
    // whatever the user was doing. Cancel every drag reaching the document so only the drop zone can
    // ever act on one.
    document.addEventListener("dragover", (event) => event.preventDefault());
    document.addEventListener("drop", (event) => event.preventDefault());

    dropZoneEl.addEventListener("dragover", (event) =>
    {
        // always cancel, so the browser never handles the drag itself
        event.preventDefault();

        // a disabled zone reports that it will not accept the drag, which is what puts the "no drop"
        // cursor under the pointer; a CSS cursor is ignored while a drag is in progress
        if (isDisabled(dropZoneEl))
        {
            if (event.dataTransfer)
                event.dataTransfer.dropEffect = "none";

            return;
        }

        dropZoneEl.classList.add("dragover");
    });

    dropZoneEl.addEventListener("dragleave", () =>
    {
        dropZoneEl.classList.remove("dragover");
    });

    dropZoneEl.addEventListener("drop", async (event) =>
    {
        // always cancel, so the browser never handles the drop itself
        event.preventDefault();
        dropZoneEl.classList.remove("dragover");

        // a disabled zone swallows the drop entirely
        if (isDisabled(dropZoneEl))
            return;

        await handleDrop(event);
    });
}

/**
 * Indicates whether the drop zone is currently refusing files.
 */
function isDisabled(dropZoneEl)
{
    return dropZoneEl.classList.contains("disabled");
}

/**
 * Reads all files (recursing into folders) from a drop event and sends them to .NET.
 */
async function handleDrop(event)
{
    // A drop replaces the working set rather than adding to it, which is what .NET does with the list it
    // is handed. Leaving the previous drop's files in the map would upload them alongside this one's,
    // into a container that never hears about them.
    fileMap.clear();
    isFollowing = true;
    lastFollowScrollTop = 0;

    const items = event.dataTransfer ? event.dataTransfer.items : null;
    const collected = [];

    if (items && items.length > 0)
    {
        const roots = [];

        for (let i = 0; i < items.length; i++)
        {
            const entry = items[i].webkitGetAsEntry ? items[i].webkitGetAsEntry() : null;

            if (entry)
                roots.push(entry);
        }

        for (const root of roots)
            await traverseEntry(root, collected);
    }
    else if (event.dataTransfer && event.dataTransfer.files)
    {
        // Fallback for browsers without entry support: plain files only.
        for (const file of event.dataTransfer.files)
            addFile(file, file.name, collected);
    }

    if (collected.length > 0 && dotNetRef)
        await dotNetRef.invokeMethodAsync("OnFilesDropped", collected);
}

/**
 * Recursively walks a FileSystemEntry, collecting files with their relative paths.
 */
async function traverseEntry(entry, collected)
{
    if (entry.isFile)
    {
        const file = await getFile(entry);
        const relativePath = entry.fullPath ? entry.fullPath.replace(/^\/+/, "") : file.name;
        addFile(file, relativePath, collected);
    }
    else if (entry.isDirectory)
    {
        const reader = entry.createReader();
        const entries = await readAllEntries(reader);

        for (const child of entries)
            await traverseEntry(child, collected);
    }
}

/**
 * Wraps FileSystemFileEntry.file in a Promise.
 */
function getFile(entry)
{
    return new Promise((resolve, reject) =>
    {
        entry.file(resolve, reject);
    });
}

/**
 * Reads every batch from a FileSystemDirectoryReader until it returns an empty batch.
 */
async function readAllEntries(reader)
{
    const all = [];

    while (true)
    {
        const batch = await readEntriesBatch(reader);

        if (!batch || batch.length === 0)
            break;

        for (const entry of batch)
            all.push(entry);
    }

    return all;
}

/**
 * Wraps FileSystemDirectoryReader.readEntries in a Promise.
 */
function readEntriesBatch(reader)
{
    return new Promise((resolve, reject) =>
    {
        reader.readEntries(resolve, reject);
    });
}

/**
 * Registers a File in the map and appends its metadata to the collected list.
 */
function addFile(file, relativePath, collected)
{
    const id = crypto.randomUUID();
    fileMap.set(id, { file: file, relativePath: relativePath });

    collected.push(
    {
        id: id,
        relativePath: relativePath,
        name: file.name,
        size: file.size
    });
}

/**
 * Uploads the requested files directly to Azure Storage using the container SAS URL, with a
 * concurrency-limited pool. Reports progress and completion back to .NET.
 *
 * The caller names the files to upload rather than this module uploading everything it happens to be
 * holding: .NET owns the list the page shows and the list the tranche is told about, so anything it did
 * not ask for would land in the container without ever being tracked against the tranche.
 */
export async function startUpload(sasUri, fileIds, concurrency, dotNetReference)
{
    dotNetRef = dotNetReference;
    activeXhrs = [];
    isFollowing = true;

    // Split the SAS URI into the container base URL and the query string.
    const queryIndex = sasUri.indexOf("?");
    const baseUrl = queryIndex >= 0 ? sasUri.substring(0, queryIndex) : sasUri;
    const query = queryIndex >= 0 ? sasUri.substring(queryIndex) : "";

    // Build the work queue from what .NET asked for, in the order it asked for it.
    const jobs = [];

    for (const id of fileIds)
    {
        const entry = fileMap.get(id);

        if (entry)
        {
            jobs.push({ id: id, file: entry.file, relativePath: entry.relativePath });
        }
        else if (dotNetRef)
        {
            // A file the browser no longer holds can never upload, and .NET waits on every file it listed,
            // so it has to be failed here rather than left pending forever.
            await dotNetRef.invokeMethodAsync("OnFileComplete", id, false);
        }
    }

    let cursor = 0;

    // A single worker pulls jobs until the queue is drained.
    async function worker()
    {
        while (cursor < jobs.length)
        {
            const index = cursor++;
            const job = jobs[index];
            await uploadOne(job.id, job.file, job.relativePath, baseUrl, query);
        }
    }

    const limit = Math.max(1, Math.min(concurrency, jobs.length));
    const workers = [];

    for (let w = 0; w < limit; w++)
        workers.push(worker());

    await Promise.all(workers);
}

/**
 * Builds the blob URL from a relative path, preserving virtual folders while encoding segments.
 */
function buildBlobUrl(baseUrl, relativePath, query)
{
    const segments = relativePath.split("/").map((segment) => encodeURIComponent(segment));
    return `${baseUrl}/${segments.join("/")}${query}`;
}

/**
 * Uploads a single file to its blob URL and reports progress and completion to .NET.
 */
function uploadOne(id, file, relativePath, baseUrl, query)
{
    return new Promise((resolve) =>
    {
        const url = buildBlobUrl(baseUrl, relativePath, query);
        const xhr = new XMLHttpRequest();

        xhr.open("PUT", url, true);
        xhr.setRequestHeader("x-ms-blob-type", "BlockBlob");
        xhr.setRequestHeader("Content-Type", file.type || "application/pdf");

        xhr.upload.onprogress = (e) =>
        {
            if (dotNetRef)
                dotNetRef.invokeMethodAsync("OnFileProgress", id, e.loaded, e.total);
        };

        xhr.onload = () =>
        {
            const success = xhr.status >= 200 && xhr.status < 300;

            if (dotNetRef)
                dotNetRef.invokeMethodAsync("OnFileComplete", id, success);

            resolve();
        };

        xhr.onerror = () =>
        {
            if (dotNetRef)
                dotNetRef.invokeMethodAsync("OnFileComplete", id, false);

            resolve();
        };

        xhr.onabort = () =>
        {
            if (dotNetRef)
                dotNetRef.invokeMethodAsync("OnFileComplete", id, false);

            resolve();
        };

        activeXhrs.push(xhr);
        xhr.send(file);
    });
}

/**
 * Keeps the row a batch has reached in view as its uploads walk down the list, so a batch too long to fit
 * on screen still shows what is being worked on. The row is centred rather than pushed to the top, which
 * keeps it clear of the table's sticky header and leaves the files either side of it visible.
 */
export function followUploadRow(dropZoneEl, rowIndex)
{
    const wrapper = dropZoneEl ? dropZoneEl.querySelector(".upload-table-wrapper") : null;

    if (!wrapper || rowIndex < 0)
        return;

    // the user takes precedence over this, so watch for scrolling this module did not do
    bindFollowSuspension(wrapper);

    // a list that fits has nothing to follow, and a user who scrolled away asked to be left alone
    if (!isFollowing || wrapper.scrollHeight <= wrapper.clientHeight)
        return;

    const row = wrapper.querySelectorAll("tbody tr")[rowIndex];

    if (!row)
        return;

    // move by the gap between where the row is and where the middle of the list is
    const rowRect = row.getBoundingClientRect();
    const wrapperRect = wrapper.getBoundingClientRect();
    const delta = (rowRect.top - wrapperRect.top) - ((wrapper.clientHeight - rowRect.height) / 2);

    wrapper.scrollTop += delta;

    // read the position back, since the browser clamps it at either end of the list
    lastFollowScrollTop = wrapper.scrollTop;
}

/**
 * Stops following the batch as soon as the user scrolls the list themselves, and picks it up again if they
 * scroll back to the bottom, which is the usual way of asking to be carried along again.
 */
function bindFollowSuspension(wrapper)
{
    if (wrapper.dataset.followBound === "true")
        return;

    wrapper.dataset.followBound = "true";
    wrapper.addEventListener("scroll", () =>
    {
        // a position this module did not set is the user's doing
        if (Math.abs(wrapper.scrollTop - lastFollowScrollTop) <= followToleranceInPixels)
            return;

        const distanceFromBottom = wrapper.scrollHeight - wrapper.scrollTop - wrapper.clientHeight;
        isFollowing = distanceFromBottom <= followToleranceInPixels;
    });
}

/**
 * Clears module state once a batch has finished, so the files it uploaded cannot follow the page into
 * whatever tranche is created next.
 */
export function resetUpload()
{
    activeXhrs = [];
    fileMap.clear();
    isFollowing = true;
    lastFollowScrollTop = 0;
}

/**
 * Aborts all in-flight uploads and clears module state.
 */
export function cancelUpload()
{
    for (const xhr of activeXhrs)
    {
        try
        {
            xhr.abort();
        }
        catch (e)
        {
            // Ignore abort failures.
        }
    }

    activeXhrs = [];
    fileMap.clear();
}
