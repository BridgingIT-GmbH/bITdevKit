// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using System.Security.Cryptography;
using System.Text;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using global::Azure;
using global::Azure.Storage.Blobs.Models;
using global::Azure.Storage.Blobs.Specialized;

public partial class AzureBlobStoreProvider
{
    /// <summary>
    /// Defines the protected Azure transport seam used by provider contract tests and specialized derived providers.
    /// </summary>
    /// <example>
    /// <code>
    /// sealed class TestProvider : AzureBlobStoreProvider
    /// {
    ///     sealed class Backend : IAzureBlobStoreBackend { }
    /// }
    /// </code>
    /// </example>
    protected interface IAzureBlobStoreBackend
    {
        /// <summary>Lists all containers in the configured storage account.</summary>
        /// <example><code>var containers = await backend.ListContainersAsync(cancellationToken);</code></example>
        Task<IReadOnlyList<string>> ListContainersAsync(CancellationToken cancellationToken);

        /// <summary>Stages, validates, and commits one blob upload.</summary>
        /// <example><code>var result = await backend.UploadAsync(key, content, request, cancellationToken);</code></example>
        Task<AzureBlobUploadResult> UploadAsync(BlobKey key, Stream content, AzureBlobUploadRequest request, CancellationToken cancellationToken);

        /// <summary>Opens a readable stream for an existing blob.</summary>
        /// <example><code>await using var stream = await backend.OpenReadAsync(key, cancellationToken);</code></example>
        Task<Stream> OpenReadAsync(BlobKey key, CancellationToken cancellationToken);

        /// <summary>Reads complete provider properties for an existing blob.</summary>
        /// <example><code>var properties = await backend.GetPropertiesAsync(key, cancellationToken);</code></example>
        Task<AzureBlobProperties> GetPropertiesAsync(BlobKey key, CancellationToken cancellationToken);

        /// <summary>Updates headers, metadata, and tags with compensation on failure.</summary>
        /// <example><code>var updated = await backend.UpdatePropertiesAsync(key, type, metadata, tags, etag, cancellationToken);</code></example>
        Task<AzureBlobProperties> UpdatePropertiesAsync(
            BlobKey key,
            string contentType,
            IDictionary<string, string> metadata,
            IDictionary<string, string> tags,
            string ifMatchETag,
            CancellationToken cancellationToken);

        /// <summary>Determines whether a blob exists.</summary>
        /// <example><code>var exists = await backend.ExistsAsync(key, cancellationToken);</code></example>
        Task<bool> ExistsAsync(BlobKey key, CancellationToken cancellationToken);

        /// <summary>Deletes a blob when its optional ETag matches.</summary>
        /// <example><code>await backend.DeleteIfExistsAsync(key, etag, cancellationToken);</code></example>
        Task DeleteIfExistsAsync(BlobKey key, string ifMatchETag, CancellationToken cancellationToken);

        /// <summary>Lists one native blob page.</summary>
        /// <example><code>var page = await backend.ListPageAsync(container, prefix, token, take, cancellationToken);</code></example>
        Task<AzureBlobListPage> ListPageAsync(
            string container,
            string prefix,
            string continuationToken,
            int take,
            CancellationToken cancellationToken);

        /// <summary>Lists one native page of expired blob keys.</summary>
        /// <example><code>var page = await backend.ListExpiredAsync(cutoff, token, take, cancellationToken);</code></example>
        Task<AzureBlobRetentionPage> ListExpiredAsync(
            string expiresOnOrBeforeTag,
            string continuationToken,
            int take,
            CancellationToken cancellationToken);
    }

    private sealed class AzureBlobStoreBackend(BlobServiceClient serviceClient) : IAzureBlobStoreBackend
    {
        public async Task<IReadOnlyList<string>> ListContainersAsync(CancellationToken cancellationToken)
        {
            var containers = new List<string>();
            await foreach (var item in serviceClient.GetBlobContainersAsync(cancellationToken: cancellationToken))
            {
                containers.Add(item.Name);
            }

            return containers.Order(StringComparer.Ordinal).ToArray();
        }

        private const string ExpiresAtTagKey = "bdk_expiresat";
        private const int UploadBlockSize = 4 * 1024 * 1024;

        public async Task<AzureBlobUploadResult> UploadAsync(
            BlobKey key,
            Stream content,
            AzureBlobUploadRequest request,
            CancellationToken cancellationToken)
        {
            var containerClient = this.GetContainerClient(key.Container);
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            var blobClient = containerClient.GetBlockBlobClient(key.Name);
            var staging = new BlockStagingWriteStream(blobClient);
            StreamCopyResult copy;
            try
            {
                copy = await StreamHelper.CopyAsync(
                        content,
                        staging,
                        new StreamCopyOptions
                        {
                            BufferSize = UploadBlockSize,
                            MaximumBytes = request.MaxBlobSize,
                            HashAlgorithm = HashAlgorithmName.SHA256
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (StreamSizeLimitExceededException exception)
            {
                throw new AzureBlobStoreBackendException(new BlobStoreSizeLimitExceededError(
                    exception.ObservedBytes,
                    exception.MaximumBytes));
            }

            var contentHash = $"sha256:{copy.Hash}";
            if (!string.IsNullOrWhiteSpace(request.ExpectedContentHash) &&
                !string.Equals(request.ExpectedContentHash, contentHash, StringComparison.Ordinal))
            {
                throw new AzureBlobStoreBackendException(new BlobStoreIntegrityError(
                    "ExpectedContentHash does not match uploaded content."));
            }

            var metadata = new Dictionary<string, string>(request.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                [request.ContentHashMetadataKey] = contentHash
            };
            var response = await blobClient.CommitBlockListAsync(
                    staging.BlockIds,
                    new CommitBlockListOptions
                    {
                        HttpHeaders = new BlobHttpHeaders { ContentType = request.ContentType },
                        Metadata = metadata,
                        Tags = request.Tags,
                        Conditions = request.FailIfExists
                            ? new BlobRequestConditions { IfNoneMatch = ETag.All }
                            : null
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            return new AzureBlobUploadResult(response.Value.ETag.ToString(), contentHash);
        }

        public async Task<Stream> OpenReadAsync(BlobKey key, CancellationToken cancellationToken) =>
            await this.GetContainerClient(key.Container)
                .GetBlobClient(key.Name)
                .OpenReadAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

        public async Task<AzureBlobProperties> GetPropertiesAsync(BlobKey key, CancellationToken cancellationToken)
        {
            var blobClient = this.GetContainerClient(key.Container).GetBlobClient(key.Name);
            var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            var tags = await blobClient.GetTagsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            return ToProperties(key, properties.Value, tags.Value.Tags);
        }

        public async Task<AzureBlobProperties> UpdatePropertiesAsync(
            BlobKey key,
            string contentType,
            IDictionary<string, string> metadata,
            IDictionary<string, string> tags,
            string ifMatchETag,
            CancellationToken cancellationToken)
        {
            var blobClient = this.GetContainerClient(key.Container).GetBlobClient(key.Name);
            var before = await this.GetPropertiesAsync(key, cancellationToken).ConfigureAwait(false);
            var initialETag = string.IsNullOrWhiteSpace(ifMatchETag) ? before.ETag : ifMatchETag;
            var mutationApplied = false;

            try
            {
                var headers = await blobClient.SetHttpHeadersAsync(
                        CopyHeaders(before.HttpHeaders, contentType),
                        new BlobRequestConditions { IfMatch = new ETag(initialETag) },
                        cancellationToken)
                    .ConfigureAwait(false);
                mutationApplied = true;
                var metadataResponse = await blobClient.SetMetadataAsync(
                        metadata,
                        new BlobRequestConditions { IfMatch = headers.Value.ETag },
                        cancellationToken)
                    .ConfigureAwait(false);
                await blobClient.SetTagsAsync(
                        tags ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                        new BlobRequestConditions { IfMatch = metadataResponse.Value.ETag },
                        cancellationToken)
                    .ConfigureAwait(false);

                return await this.GetPropertiesAsync(key, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception operationException)
            {
                if (!mutationApplied && operationException is RequestFailedException { Status: 412 })
                {
                    throw;
                }

                try
                {
                    await RestoreAsync(blobClient, before).ConfigureAwait(false);
                }
                catch (Exception restoreException)
                {
                    throw new AzureBlobStoreBackendException(new BlobStorePartialUpdateError(
                        $"Azure blob properties for '{key.Container}/{key.Name}' could not be restored.",
                        operationException.Message,
                        restoreException.Message));
                }

                if (operationException is OperationCanceledException)
                {
                    throw;
                }

                throw;
            }
        }

        public async Task<bool> ExistsAsync(BlobKey key, CancellationToken cancellationToken) =>
            (await this.GetContainerClient(key.Container)
                .GetBlobClient(key.Name)
                .ExistsAsync(cancellationToken)
                .ConfigureAwait(false)).Value;

        public async Task DeleteIfExistsAsync(BlobKey key, string ifMatchETag, CancellationToken cancellationToken)
        {
            var conditions = string.IsNullOrWhiteSpace(ifMatchETag)
                ? null
                : new BlobRequestConditions { IfMatch = new ETag(ifMatchETag) };
            await this.GetContainerClient(key.Container)
                .GetBlobClient(key.Name)
                .DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, conditions, cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<AzureBlobListPage> ListPageAsync(
            string container,
            string prefix,
            string continuationToken,
            int take,
            CancellationToken cancellationToken)
        {
            var pages = this.GetContainerClient(container)
                .GetBlobsAsync(
                    BlobTraits.Metadata | BlobTraits.Tags,
                    BlobStates.None,
                    prefix,
                    cancellationToken)
                .AsPages(continuationToken, take);
            await foreach (var page in pages.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                return new AzureBlobListPage(
                    page.Values.Select(item => ToProperties(container, item)).ToArray(),
                    page.ContinuationToken);
            }

            return new AzureBlobListPage([], null);
        }

        public async Task<AzureBlobRetentionPage> ListExpiredAsync(
            string expiresOnOrBeforeTag,
            string continuationToken,
            int take,
            CancellationToken cancellationToken)
        {
            var query = $"\"{ExpiresAtTagKey}\" <= '{expiresOnOrBeforeTag}'";
            var pages = serviceClient.FindBlobsByTagsAsync(query, cancellationToken)
                .AsPages(continuationToken, take);
            await foreach (var page in pages.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                return new AzureBlobRetentionPage(
                    page.Values.Select(item => new BlobKey(item.BlobContainerName, item.BlobName)).ToArray(),
                    page.ContinuationToken);
            }

            return new AzureBlobRetentionPage([], null);
        }

        private static async Task RestoreAsync(BlobClient blobClient, AzureBlobProperties before)
        {
            var current = await blobClient.GetPropertiesAsync(cancellationToken: CancellationToken.None).ConfigureAwait(false);
            var headers = await blobClient.SetHttpHeadersAsync(
                    CopyHeaders(before.HttpHeaders, before.HttpHeaders?.ContentType),
                    new BlobRequestConditions { IfMatch = current.Value.ETag },
                    CancellationToken.None)
                .ConfigureAwait(false);
            var metadata = await blobClient.SetMetadataAsync(
                    before.Metadata,
                    new BlobRequestConditions { IfMatch = headers.Value.ETag },
                    CancellationToken.None)
                .ConfigureAwait(false);
            await blobClient.SetTagsAsync(
                    before.Tags ?? new Dictionary<string, string>(),
                    new BlobRequestConditions { IfMatch = metadata.Value.ETag },
                    CancellationToken.None)
                .ConfigureAwait(false);
        }

        private BlobContainerClient GetContainerClient(string container) => serviceClient.GetBlobContainerClient(container);

        private static AzureBlobProperties ToProperties(
            BlobKey key,
            BlobProperties properties,
            IDictionary<string, string> tags) =>
            new(
                key,
                properties.ContentLength,
                new BlobHttpHeaders
                {
                    ContentType = properties.ContentType,
                    ContentEncoding = properties.ContentEncoding,
                    ContentLanguage = properties.ContentLanguage,
                    ContentDisposition = properties.ContentDisposition,
                    CacheControl = properties.CacheControl,
                    ContentHash = properties.ContentHash?.ToArray()
                },
                properties.ETag.ToString(),
                properties.CreatedOn,
                properties.LastModified,
                properties.Metadata,
                tags);

        private static AzureBlobProperties ToProperties(string container, BlobItem item) =>
            new(
                new BlobKey(container, item.Name),
                item.Properties.ContentLength ?? 0,
                new BlobHttpHeaders
                {
                    ContentType = item.Properties.ContentType,
                    ContentEncoding = item.Properties.ContentEncoding,
                    ContentLanguage = item.Properties.ContentLanguage,
                    ContentDisposition = item.Properties.ContentDisposition,
                    CacheControl = item.Properties.CacheControl,
                    ContentHash = item.Properties.ContentHash?.ToArray()
                },
                item.Properties.ETag?.ToString(),
                item.Properties.CreatedOn,
                item.Properties.LastModified,
                item.Metadata,
                item.Tags);

        private static BlobHttpHeaders CopyHeaders(BlobHttpHeaders headers, string contentType) => new()
        {
            ContentType = contentType,
            ContentEncoding = headers?.ContentEncoding,
            ContentLanguage = headers?.ContentLanguage,
            ContentDisposition = headers?.ContentDisposition,
            CacheControl = headers?.CacheControl,
            ContentHash = headers?.ContentHash?.ToArray()
        };

        private sealed class BlockStagingWriteStream(BlockBlobClient client) : Stream
        {
            private readonly List<string> blockIds = [];
            private readonly string operationId = Guid.NewGuid().ToString("N");

            public IReadOnlyList<string> BlockIds => this.blockIds;

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) =>
                throw new NotSupportedException("Use asynchronous writes for Azure block staging.");

            public override async ValueTask WriteAsync(
                ReadOnlyMemory<byte> buffer,
                CancellationToken cancellationToken = default)
            {
                var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                    $"{this.operationId}-{this.blockIds.Count:D8}"));
                await using var content = new MemoryStream(buffer.ToArray(), writable: false);
                await client.StageBlockAsync(blockId, content, cancellationToken: cancellationToken).ConfigureAwait(false);
                this.blockIds.Add(blockId);
            }
        }
    }

    /// <summary>
    /// Describes a staged Azure blob commit request.
    /// </summary>
    /// <example>
    /// <code>
    /// var request = new AzureBlobUploadRequest(contentType, metadata, tags, false, expectedHash, maxSize, "bdk_contenthash");
    /// </code>
    /// </example>
    protected sealed record AzureBlobUploadRequest(
        string ContentType,
        IDictionary<string, string> Metadata,
        IDictionary<string, string> Tags,
        bool FailIfExists,
        string ExpectedContentHash,
        long? MaxBlobSize,
        string ContentHashMetadataKey);

    /// <summary>
    /// Describes a committed Azure blob upload.
    /// </summary>
    /// <example>
    /// <code>
    /// var result = new AzureBlobUploadResult(etag, contentHash);
    /// </code>
    /// </example>
    protected sealed record AzureBlobUploadResult(string ETag, string ContentHash);

    /// <summary>
    /// Describes Azure blob properties used by the provider boundary.
    /// </summary>
    /// <example>
    /// <code>
    /// var etag = properties.ETag;
    /// </code>
    /// </example>
    protected sealed record AzureBlobProperties(
        BlobKey Key,
        long Length,
        BlobHttpHeaders HttpHeaders,
        string ETag,
        DateTimeOffset? CreatedAt,
        DateTimeOffset? LastModifiedAt,
        IDictionary<string, string> Metadata,
        IDictionary<string, string> Tags);

    /// <summary>
    /// Describes one Azure listing page.
    /// </summary>
    /// <example>
    /// <code>
    /// var next = page.ContinuationToken;
    /// </code>
    /// </example>
    protected sealed record AzureBlobListPage(IReadOnlyCollection<AzureBlobProperties> Items, string ContinuationToken);

    /// <summary>
    /// Describes one Azure retention candidate page.
    /// </summary>
    /// <example>
    /// <code>
    /// var candidates = page.Items;
    /// </code>
    /// </example>
    protected sealed record AzureBlobRetentionPage(IReadOnlyCollection<BlobKey> Items, string ContinuationToken);

    /// <summary>
    /// Carries a typed blob-store error across the protected Azure backend boundary.
    /// </summary>
    /// <example>
    /// <code>
    /// throw new AzureBlobStoreBackendException(new BlobStoreIntegrityError("Hash mismatch."));
    /// </code>
    /// </example>
    protected sealed class AzureBlobStoreBackendException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AzureBlobStoreBackendException" /> class.
        /// </summary>
        /// <param name="error">The typed provider error.</param>
        /// <example>
        /// <code>
        /// var exception = new AzureBlobStoreBackendException(error);
        /// </code>
        /// </example>
        public AzureBlobStoreBackendException(IResultError error)
            : base(error?.Message)
        {
            this.Error = error ?? throw new ArgumentNullException(nameof(error));
        }

        /// <summary>
        /// Gets the typed provider error.
        /// </summary>
        /// <example>
        /// <code>
        /// var error = exception.Error;
        /// </code>
        /// </example>
        public IResultError Error { get; }
    }
}
