using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.AI.DocumentIntelligence;

using Microsoft.Extensions.Logging;

using FoundrySharePointKnowledge.Common;
using FoundrySharePointKnowledge.Domain.Contracts;
using FoundrySharePointKnowledge.Domain.Foundry.VectorStores;

namespace FoundrySharePointKnowledge.Infrastructure.Managers
{
    /// <summary>
    /// This converts source documents into the markdown a vector store indexes best, caching each result
    /// alongside its source so a re-run of the same tranche does not pay Document Intelligence twice.
    /// </summary>
    public class DocumentIntelligenceManager : IDocumentIntelligenceManager
    {
        #region Members
        private readonly BlobServiceClient _blobClient;
        private readonly ILogger<DocumentIntelligenceManager> _logger;
        private readonly DocumentIntelligenceClient _documentIntelligenceClient;
        private readonly ConcurrentDictionary<string, bool> _ensuredContainers = new ConcurrentDictionary<string, bool>();
        #endregion
        #region Initialization
        public DocumentIntelligenceManager(BlobServiceClient blobClient,
                                           ILogger<DocumentIntelligenceManager> logger,
                                           DocumentIntelligenceClient documentIntelligenceClient)
        {
            //initialization
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this._blobClient = blobClient ?? throw new ArgumentNullException(nameof(blobClient));
            this._documentIntelligenceClient = documentIntelligenceClient ?? throw new ArgumentNullException(nameof(documentIntelligenceClient));
        }
        #endregion
        #region Public Methods
        /// <summary>
        /// Converts one source blob into markdown, serving a previously converted copy whenever the source
        /// has not changed since it was written.
        /// </summary>
        public async Task<MarkdownConversion> ConvertToMarkdownAsync(MarkdownConversionRequest conversionRequest, CancellationToken cancellationToken)
        {
            //initialization
            ArgumentNullException.ThrowIfNull(conversionRequest);
            string extension = Path.GetExtension(conversionRequest.BlobName).ToLowerInvariant();

            //the caller names what the vector store sees, since only it knows which of a tranche's paths
            //flatten onto one another; the cache keeps each source's own path instead, which cannot collide
            string cachePath = $"{conversionRequest.BlobName}{FSPKConstants.Extensions.MD}";

            //converted markdown is kept in a container of its own, so nothing a user uploads can collide with
            //it however their folders happen to be named
            BlobContainerClient containerClient = this._blobClient.GetBlobContainerClient(conversionRequest.ContainerName);
            BlobContainerClient markdownContainerClient = await this.EnsureMarkdownContainerAsync(conversionRequest.ContainerName, cancellationToken);
            BlobClient cacheClient = markdownContainerClient.GetBlobClient(cachePath);

            //serve the cached markdown when the source has not changed since it was written, since analysis
            //is billed per page and a tranche is re-synchronized far more often than its files change
            BinaryData cachedMarkdown = await this.LoadCachedMarkdownAsync(cacheClient, conversionRequest.SourceETag, cancellationToken);
            if (cachedMarkdown != null)
            {
                //return
                this._logger.LogInformation($"Reusing the cached markdown of {conversionRequest.BlobName}.");
                return new MarkdownConversion(conversionRequest.BlobName, conversionRequest.MarkdownName, cachedMarkdown, true);
            }

            //reject what cannot be converted before paying to download it
            bool isAnalyzable = FSPKConstants.Foundry.DocumentIntelligence.AnalyzableExtensions.Contains(extension);
            bool isPassthrough = FSPKConstants.Foundry.DocumentIntelligence.PassthroughExtensions.Contains(extension);

            if (!isAnalyzable && !isPassthrough)
                return new MarkdownConversion(conversionRequest.BlobName, $"{(string.IsNullOrWhiteSpace(extension) ? "A file with no extension" : extension)} cannot be converted to markdown.");

            if (conversionRequest.SourceSize > FSPKConstants.Foundry.DocumentIntelligence.MaxFileSizeBytes)
                return new MarkdownConversion(conversionRequest.BlobName, $"The file is {conversionRequest.SourceSize} bytes, which is over the {FSPKConstants.Foundry.DocumentIntelligence.MaxFileSizeBytes} byte limit.");

            //download the source, holding its bytes only for as long as this one conversion runs
            Response<BlobDownloadResult> download = await containerClient.GetBlobClient(conversionRequest.BlobName).DownloadContentAsync(cancellationToken);
            string downloadError = await download.GetResponseErrorAsync($"Failed to download {conversionRequest.BlobName} from {containerClient.Uri}.");

            if (!string.IsNullOrWhiteSpace(downloadError))
                return new MarkdownConversion(conversionRequest.BlobName, downloadError);

            //convert the source, which for text is only a matter of presenting it as markdown
            BinaryData markdown = isAnalyzable
                                  ? await this.AnalyzeAsync(conversionRequest.BlobName, download.Value.Content, cancellationToken)
                                  : DocumentIntelligenceManager.WrapText(download.Value.Content, extension);

            if (markdown == null)
                return new MarkdownConversion(conversionRequest.BlobName, $"Analyzing {conversionRequest.BlobName} produced no content.");

            //cache the result against the source it came from, since a failure anywhere later in the upload
            //should not cost a second conversion when the tranche is retried
            await this.SaveCachedMarkdownAsync(cacheClient, markdown, conversionRequest.SourceETag, cancellationToken);

            //return
            return new MarkdownConversion(conversionRequest.BlobName, conversionRequest.MarkdownName, markdown, false);
        }

        /// <summary>
        /// Returns a textual representation of an instance of this object.
        /// </summary>
        public override string ToString()
        {
            //return
            return nameof(DocumentIntelligenceManager);
        }
        #endregion
        #region Private Methods
        /// <summary>
        /// Sends one document through the prebuilt layout model and returns it as markdown.
        /// </summary>
        private async Task<BinaryData> AnalyzeAsync(string blobName, BinaryData content, CancellationToken cancellationToken)
        {
            //initialization
            this._logger.LogInformation($"Analyzing {blobName} using {FSPKConstants.Foundry.PrebuiltLayout}.");
            AnalyzeDocumentOptions analyzeOptions = new AnalyzeDocumentOptions(FSPKConstants.Foundry.PrebuiltLayout, content)
            {
                //markdown keeps the tables and headings that plain text extraction throws away
                OutputContentFormat = DocumentContentFormat.Markdown
            };

            //analyze the document
            Operation<AnalyzeResult> operation = await this._documentIntelligenceClient.AnalyzeDocumentAsync(WaitUntil.Completed, analyzeOptions, cancellationToken);
            if (!operation.HasValue || string.IsNullOrWhiteSpace(operation.Value?.Content))
            {
                //error
                this._logger.LogWarning($"Analyzing {blobName} using {FSPKConstants.Foundry.PrebuiltLayout} returned no content.");
                return null;
            }

            //return
            this._logger.LogInformation($"Analyzed {blobName} into {operation.Value.Content.Length} markdown characters.");
            return BinaryData.FromString(operation.Value.Content);
        }

        /// <summary>
        /// Gets the container holding a tranche's converted markdown, creating it the first time this process
        /// is asked for it rather than on every file it converts.
        /// </summary>
        private async Task<BlobContainerClient> EnsureMarkdownContainerAsync(string containerName, CancellationToken cancellationToken)
        {
            //initialization
            string markdownContainerName = containerName.ToMarkdownContainerName();
            BlobContainerClient markdownContainerClient = this._blobClient.GetBlobContainerClient(markdownContainerName);

            //a container that has already been accounted for costs nothing to use again
            if (this._ensuredContainers.ContainsKey(markdownContainerName))
                return markdownContainerClient;

            //return
            await markdownContainerClient.CreateIfNotExistsAsync(PublicAccessType.None, null, null, cancellationToken);
            this._ensuredContainers.TryAdd(markdownContainerName, true);
            return markdownContainerClient;
        }

        /// <summary>
        /// Reads a previously converted copy of a source blob, which is only usable while the source it was
        /// converted from is the one still sitting in the container.
        /// </summary>
        private async Task<BinaryData> LoadCachedMarkdownAsync(BlobClient cacheClient, string sourceETag, CancellationToken cancellationToken)
        {
            //a source with no tag cannot be matched against anything, so it always converts afresh
            if (string.IsNullOrWhiteSpace(sourceETag))
                return null;

            try
            {
                //a missing cache entry is the ordinary case on a first run, not a failure
                Response<BlobProperties> properties = await cacheClient.GetPropertiesAsync(null, cancellationToken);
                if (properties?.Value?.Metadata == null)
                    return null;

                //the cached copy is stale once its source has been replaced
                properties.Value.Metadata.TryGetValue(FSPKConstants.Foundry.DocumentIntelligence.SourceETagMetadata, out string cachedETag);
                if (!string.Equals(cachedETag, DocumentIntelligenceManager.NormalizeETag(sourceETag), StringComparison.Ordinal))
                    return null;

                //return
                Response<BlobDownloadResult> download = await cacheClient.DownloadContentAsync(cancellationToken);
                return download?.Value?.Content;
            }
            catch (RequestFailedException ex)
            {
                //a cache that cannot be read is only a lost saving, so the conversion goes ahead without it
                this._logger.LogInformation($"No usable cached markdown for {cacheClient.Name}: {ex.Message}.");
                return null;
            }
        }

        /// <summary>
        /// Writes a converted document beside its source, stamped with the source it was converted from.
        /// </summary>
        private async Task SaveCachedMarkdownAsync(BlobClient cacheClient, BinaryData markdown, string sourceETag, CancellationToken cancellationToken)
        {
            try
            {
                //stamp the source's tag onto the cached copy so a later run can tell whether it still applies
                BlobUploadOptions uploadOptions = new BlobUploadOptions()
                {
                    //assemble object
                    HttpHeaders = new BlobHttpHeaders()
                    {
                        //assemble object
                        ContentType = FSPKConstants.ContentTypes.Markdown
                    },
                    Metadata = new Dictionary<string, string>()
                    {
                        //assemble dictionary
                        { FSPKConstants.Foundry.DocumentIntelligence.SourceETagMetadata, DocumentIntelligenceManager.NormalizeETag(sourceETag) }
                    }
                };

                //return
                await cacheClient.UploadAsync(markdown.ToStream(), uploadOptions, cancellationToken);
            }
            catch (RequestFailedException ex)
            {
                //failing to cache costs a future conversion but must not fail this one
                this._logger.LogWarning(ex, $"Failed to cache the markdown of {cacheClient.Name}.");
            }
        }

        /// <summary>
        /// Presents an already textual file as markdown, fencing the structured formats whose meaning depends
        /// on their layout.
        /// </summary>
        private static BinaryData WrapText(BinaryData content, string extension)
        {
            //plain text and markdown are already what a vector store wants
            if (!FSPKConstants.Foundry.DocumentIntelligence.FencedExtensions.Contains(extension))
                return content;

            //return
            return BinaryData.FromString(string.Format(FSPKConstants.Foundry.DocumentIntelligence.CodeFenceFormat, extension.TrimStart('.'), content.ToString()));
        }

        /// <summary>
        /// Strips the quoting an Azure entity tag carries, which blob metadata will not accept.
        /// </summary>
        private static string NormalizeETag(string eTag)
        {
            //return
            return eTag?.Trim('"') ?? string.Empty;
        }
        #endregion
    }
}
