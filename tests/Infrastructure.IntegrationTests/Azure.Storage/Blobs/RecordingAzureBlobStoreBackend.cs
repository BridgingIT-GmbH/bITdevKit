// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.Azure.Storage;

using Application.Storage;
using Infrastructure.Azure;
using global::Azure;
using global::Azure.Storage.Blobs.Models;

internal sealed class RecordingAzureBlobStoreBackend : AzureBlobStoreProvider
{
    private readonly Backend backend;

    public RecordingAzureBlobStoreBackend(BlobStoreOptions options = null)
        : this(new Backend(), options)
    {
    }

    private RecordingAzureBlobStoreBackend(Backend backend, BlobStoreOptions options)
        : base(backend, options)
    {
        this.backend = backend;
    }

    public int UploadCalls => this.backend.UploadCalls;
    public int SetPropertiesCalls => this.backend.SetPropertiesCalls;
    public int OpenReadCalls { get => this.backend.OpenReadCalls; set => this.backend.OpenReadCalls = value; }
    public bool LastFailIfExists => this.backend.LastFailIfExists;
    public string LastIfMatchETag => this.backend.LastIfMatchETag;
    public string LastDeleteIfMatchETag => this.backend.LastDeleteIfMatchETag;
    public string LastListPrefix => this.backend.LastListPrefix;
    public IDictionary<string, string> LastMetadata => this.backend.LastMetadata;
    public IDictionary<string, string> LastTags => this.backend.LastTags;
    public Exception NextException { get => this.backend.NextException; set => this.backend.NextException = value; }

    private sealed class Backend : IAzureBlobStoreBackend
    {
        private readonly Dictionary<BlobKey, Entry> entries = [];

        public int UploadCalls { get; private set; }
        public int SetPropertiesCalls { get; private set; }
        public int OpenReadCalls { get; set; }
        public bool LastFailIfExists { get; private set; }
        public string LastIfMatchETag { get; private set; }
        public string LastDeleteIfMatchETag { get; private set; }
        public string LastListPrefix { get; private set; }
        public IDictionary<string, string> LastMetadata { get; private set; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public IDictionary<string, string> LastTags { get; private set; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public Exception NextException { get; set; }

        public Task<IReadOnlyList<string>> ListContainersAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<string> containers = this.entries.Keys.Select(key => key.Container)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            return Task.FromResult(containers);
        }

        public async Task<AzureBlobUploadResult> UploadAsync(
            BlobKey key,
            Stream content,
            AzureBlobUploadRequest request,
            CancellationToken cancellationToken)
        {
            this.ThrowIfRequested();
            this.UploadCalls++;
            this.LastFailIfExists = request.FailIfExists;

            if (request.FailIfExists && this.entries.ContainsKey(key))
            {
                throw new RequestFailedException(412, "Blob already exists.");
            }

            using var target = new MemoryStream();
            StreamCopyResult copy;
            try
            {
                copy = await StreamHelper.CopyAsync(
                    content,
                    target,
                    new StreamCopyOptions
                    {
                        MaximumBytes = request.MaxBlobSize,
                        HashAlgorithm = System.Security.Cryptography.HashAlgorithmName.SHA256
                    },
                    cancellationToken);
            }
            catch (StreamSizeLimitExceededException exception)
            {
                throw new AzureBlobStoreBackendException(new BlobStoreSizeLimitExceededError(
                    exception.ObservedBytes,
                    exception.MaximumBytes));
            }

            var contentHash = $"{BlobContentHash.Prefix}{copy.Hash}";
            if (!string.IsNullOrWhiteSpace(request.ExpectedContentHash) &&
                !string.Equals(request.ExpectedContentHash, contentHash, StringComparison.Ordinal))
            {
                throw new AzureBlobStoreBackendException(new BlobStoreIntegrityError("ExpectedContentHash does not match uploaded content."));
            }

            var now = DateTimeOffset.UtcNow;
            var metadata = Clone(request.Metadata);
            metadata[request.ContentHashMetadataKey] = contentHash;
            var entry = new Entry
            {
                Content = target.ToArray(),
                ContentType = request.ContentType,
                Metadata = metadata,
                Tags = Clone(request.Tags),
                ETag = CreateETag(),
                CreatedAt = this.entries.TryGetValue(key, out var existing) ? existing.CreatedAt : now,
                LastModifiedAt = now
            };
            this.entries[key] = entry;
            this.LastMetadata = Clone(entry.Metadata);
            this.LastTags = Clone(entry.Tags);

            return new AzureBlobUploadResult(entry.ETag, contentHash);
        }

        public Task<Stream> OpenReadAsync(BlobKey key, CancellationToken cancellationToken)
        {
            this.ThrowIfRequested();
            this.OpenReadCalls++;
            var entry = this.GetEntry(key);
            return Task.FromResult<Stream>(new MemoryStream(entry.Content.ToArray(), writable: false));
        }

        public Task<AzureBlobProperties> GetPropertiesAsync(BlobKey key, CancellationToken cancellationToken)
        {
            this.ThrowIfRequested();
            return Task.FromResult(this.ToProperties(key, this.GetEntry(key)));
        }

        public Task<AzureBlobProperties> UpdatePropertiesAsync(
            BlobKey key,
            string contentType,
            IDictionary<string, string> metadata,
            IDictionary<string, string> tags,
            string ifMatchETag,
            CancellationToken cancellationToken)
        {
            this.ThrowIfRequested();
            this.SetPropertiesCalls++;
            this.LastIfMatchETag = ifMatchETag;
            var entry = this.GetEntry(key);
            if (!string.IsNullOrWhiteSpace(ifMatchETag) &&
                !string.Equals(ifMatchETag, entry.ETag, StringComparison.Ordinal))
            {
                throw new RequestFailedException(412, "ETag mismatch.");
            }

            entry.ContentType = contentType;
            entry.Metadata = Clone(metadata);
            entry.Tags = Clone(tags);
            entry.ETag = CreateETag();
            entry.LastModifiedAt = DateTimeOffset.UtcNow;
            this.LastMetadata = Clone(entry.Metadata);
            this.LastTags = Clone(entry.Tags);
            return Task.FromResult(this.ToProperties(key, entry));
        }

        public Task<bool> ExistsAsync(BlobKey key, CancellationToken cancellationToken)
        {
            this.ThrowIfRequested();
            return Task.FromResult(this.entries.ContainsKey(key));
        }

        public Task DeleteIfExistsAsync(BlobKey key, string ifMatchETag, CancellationToken cancellationToken)
        {
            this.ThrowIfRequested();
            this.LastDeleteIfMatchETag = ifMatchETag;
            if (this.entries.TryGetValue(key, out var entry) &&
                !string.IsNullOrWhiteSpace(ifMatchETag) &&
                !string.Equals(ifMatchETag, entry.ETag, StringComparison.Ordinal))
            {
                throw new RequestFailedException(412, "ETag mismatch.");
            }

            this.entries.Remove(key);
            return Task.CompletedTask;
        }

        public Task<AzureBlobListPage> ListPageAsync(
            string container,
            string prefix,
            string continuationToken,
            int take,
            CancellationToken cancellationToken)
        {
            this.ThrowIfRequested();
            this.LastListPrefix = prefix;
            var skip = string.IsNullOrWhiteSpace(continuationToken)
                ? 0
                : int.Parse(continuationToken.Replace("native:", string.Empty, StringComparison.Ordinal));
            var query = this.entries
                .Where(item => string.Equals(item.Key.Container, container, StringComparison.Ordinal))
                .Where(item => string.IsNullOrEmpty(prefix) || item.Key.Name.StartsWith(prefix, StringComparison.Ordinal))
                .OrderBy(item => item.Key.Name, StringComparer.Ordinal);
            var rows = query.Skip(skip).Take(take).ToList();
            var next = rows.Count == take && query.Skip(skip + take).Any()
                ? $"native:{skip + take}"
                : null;
            return Task.FromResult(new AzureBlobListPage(
                rows.Select(item => this.ToProperties(item.Key, item.Value)).ToList(),
                next));
        }

        public Task<AzureBlobRetentionPage> ListExpiredAsync(
            string expiresOnOrBeforeTag,
            string continuationToken,
            int take,
            CancellationToken cancellationToken)
        {
            this.ThrowIfRequested();
            var skip = string.IsNullOrWhiteSpace(continuationToken)
                ? 0
                : int.Parse(continuationToken.Replace("retention:", string.Empty, StringComparison.Ordinal));
            var query = this.entries
                .Where(item => item.Value.Tags.TryGetValue("bdk_expiresat", out var expiresAt) &&
                    string.Compare(expiresAt, expiresOnOrBeforeTag, StringComparison.Ordinal) <= 0)
                .OrderBy(item => item.Value.Tags["bdk_expiresat"], StringComparer.Ordinal)
                .ThenBy(item => item.Key.Container, StringComparer.Ordinal)
                .ThenBy(item => item.Key.Name, StringComparer.Ordinal);
            var rows = query.Skip(skip).Take(take).Select(item => item.Key).ToList();
            var next = rows.Count == take && query.Skip(skip + take).Any()
                ? $"retention:{skip + take}"
                : null;
            return Task.FromResult(new AzureBlobRetentionPage(rows, next));
        }

        private static string CreateETag() => $"\"{Guid.NewGuid():N}\"";

        private static Dictionary<string, string> Clone(IDictionary<string, string> values) =>
            values is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);

        private Entry GetEntry(BlobKey key)
        {
            if (!this.entries.TryGetValue(key, out var entry))
            {
                throw new RequestFailedException(404, "Blob was not found.");
            }

            return entry;
        }

        private AzureBlobProperties ToProperties(BlobKey key, Entry entry) => new(
            key,
            entry.Content.Length,
            new BlobHttpHeaders { ContentType = entry.ContentType },
            entry.ETag,
            entry.CreatedAt,
            entry.LastModifiedAt,
            Clone(entry.Metadata),
            Clone(entry.Tags));

        private void ThrowIfRequested()
        {
            if (this.NextException is null)
            {
                return;
            }

            var exception = this.NextException;
            this.NextException = null;
            throw exception;
        }

        private sealed class Entry
        {
            public byte[] Content { get; set; }
            public string ContentType { get; set; }
            public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string> Tags { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public string ETag { get; set; }
            public DateTimeOffset CreatedAt { get; set; }
            public DateTimeOffset LastModifiedAt { get; set; }
        }
    }
}
