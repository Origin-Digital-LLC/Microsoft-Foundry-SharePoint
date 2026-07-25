// Direct-to-Azure-Storage bulk upload helper for the Blazor Create Tranche page.
// The browser reads dropped files/folders, uploads each blob directly to Azure Storage using a
// container SAS URL, and reports byte-level progress back to .NET.

// Module-level state.
const fileMap = new Map();      // id -> { file, relativePath }
let activeXhrs = [];            // in-flight XMLHttpRequest instances
let dotNetRef = null;           // reference to the .NET CreateTrancheBase component

/**
 * Registers drag-and-drop handlers on the drop zone element.
 */
export function registerDropZone(dropZoneEl, dotNetReference)
{
    dotNetRef = dotNetReference;

    dropZoneEl.addEventListener("dragover", (event) =>
    {
        event.preventDefault();
        dropZoneEl.classList.add("dragover");
    });

    dropZoneEl.addEventListener("dragleave", () =>
    {
        dropZoneEl.classList.remove("dragover");
    });

    dropZoneEl.addEventListener("drop", async (event) =>
    {
        event.preventDefault();
        dropZoneEl.classList.remove("dragover");
        await handleDrop(event);
    });
}

/**
 * Reads all files (recursing into folders) from a drop event and sends them to .NET.
 */
async function handleDrop(event)
{
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
 * Uploads all mapped files directly to Azure Storage using the container SAS URL, with a
 * concurrency-limited pool. Reports progress and completion back to .NET.
 */
export async function startUpload(sasUri, concurrency, dotNetReference)
{
    dotNetRef = dotNetReference;
    activeXhrs = [];

    // Split the SAS URI into the container base URL and the query string.
    const queryIndex = sasUri.indexOf("?");
    const baseUrl = queryIndex >= 0 ? sasUri.substring(0, queryIndex) : sasUri;
    const query = queryIndex >= 0 ? sasUri.substring(queryIndex) : "";

    // Build the work queue.
    const jobs = [];

    for (const [id, entry] of fileMap.entries())
        jobs.push({ id: id, file: entry.file, relativePath: entry.relativePath });

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
