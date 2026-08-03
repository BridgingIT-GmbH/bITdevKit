// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Adds exact-key read-through caching and write invalidation to an <see cref="IBlobStoreClient" />.
/// </summary>
/// <example>
/// <code>
/// services.AddBlobStorage()
///     .WithCacheBehavior(options => options.SlidingExpiration = TimeSpan.FromMinutes(10))
///     .WithInMemoryClient("reports");
/// </code>
/// </example>
public sealed partial class CacheBlobStoreClientBehavior : IBlobStoreClient
{
    private readonly ICacheProvider cacheProvider;
    private readonly IBlobStoreClient inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="CacheBlobStoreClientBehavior" /> class.
    /// </summary>
    /// <param name="loggerFactory">The logger factory used by the behavior.</param>
    /// <param name="inner">The decorated blob-store client.</param>
    /// <param name="cacheProvider">The cache provider used to store exact-key downloads.</param>
    /// <param name="options">The optional cache behavior options.</param>
    /// <param name="storeName">The configured blob-store client name.</param>
    /// <example>
    /// <code>
    /// var behavior = new CacheBlobStoreClientBehavior(loggerFactory, inner, cacheProvider, options, "reports");
    /// </code>
    /// </example>
    public CacheBlobStoreClientBehavior(
        ILoggerFactory loggerFactory,
        IBlobStoreClient inner,
        ICacheProvider cacheProvider,
        CacheBlobStoreClientBehaviorOptions options = null,
        string storeName = null)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.cacheProvider = cacheProvider ?? throw new ArgumentNullException(nameof(cacheProvider));
        this.Logger = loggerFactory?.CreateLogger<CacheBlobStoreClientBehavior>() ??
            NullLoggerFactory.Instance.CreateLogger<CacheBlobStoreClientBehavior>();
        this.Options = options ?? new CacheBlobStoreClientBehaviorOptions();
        this.StoreName = string.IsNullOrWhiteSpace(storeName) ? "default" : storeName;
    }

    /// <summary>
    /// Gets the logger used by the behavior.
    /// </summary>
    /// <example>
    /// <code>
    /// var logger = behavior.Logger;
    /// </code>
    /// </example>
    public ILogger<CacheBlobStoreClientBehavior> Logger { get; }

    /// <summary>
    /// Gets the cache behavior options.
    /// </summary>
    /// <example>
    /// <code>
    /// var maxBytes = behavior.Options.MaxCachedBlobSize;
    /// </code>
    /// </example>
    public CacheBlobStoreClientBehaviorOptions Options { get; }

    /// <summary>
    /// Gets the configured blob-store client name.
    /// </summary>
    /// <example>
    /// <code>
    /// var store = behavior.StoreName;
    /// </code>
    /// </example>
    public string StoreName { get; }

    /// <inheritdoc />
    public async Task<Result<BlobInfo>> UploadAsync(
        BlobUpload upload,
        CancellationToken cancellationToken = default)
    {
        var result = await this.inner.UploadAsync(upload, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            await this.InvalidateAsync(upload?.Key, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<Result<BlobDownload>> DownloadAsync(
        BlobKey key,
        CancellationToken cancellationToken = default)
    {
        if (key is null)
        {
            return await this.inner.DownloadAsync(key, cancellationToken).ConfigureAwait(false);
        }

        var validation = this.Options.Validate();
        if (validation.IsFailure)
        {
            return Result<BlobDownload>.Failure()
                .WithErrors(validation.Errors)
                .WithMessages(validation.Messages);
        }

        var cacheKey = this.CreateExactCacheKey(key);
        if (await this.cacheProvider.TryGetAsync(cacheKey, out CacheBlobDownloadEntry cached, cancellationToken).ConfigureAwait(false))
        {
            TypedLogger.LogCacheHit(this.Logger, this.StoreName);

            return Result<BlobDownload>.Success(new BlobDownload
            {
                Content = new MemoryStream(cached.Content ?? [], writable: false),
                Info = cached.Info
            });
        }

        TypedLogger.LogCacheMiss(this.Logger, this.StoreName);

        var result = await this.inner.DownloadAsync(key, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return result;
        }

        if (!this.CanCache(result.Value))
        {
            return result;
        }

        return await this.CacheAndReturnAsync(cacheKey, result.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<Result<BlobInfo>> GetPropertiesAsync(BlobKey key, CancellationToken cancellationToken = default) =>
        this.inner.GetPropertiesAsync(key, cancellationToken);

    /// <inheritdoc />
    public async Task<Result<BlobInfo>> UpdatePropertiesAsync(
        BlobPropertiesUpdate update,
        CancellationToken cancellationToken = default)
    {
        var result = await this.inner.UpdatePropertiesAsync(update, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            await this.InvalidateAsync(update?.Key, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    /// <inheritdoc />
    public Task<Result<bool>> ExistsAsync(BlobKey key, CancellationToken cancellationToken = default) =>
        this.inner.ExistsAsync(key, cancellationToken);

    /// <inheritdoc />
    public Task<Result<BlobPage>> ListPageAsync(BlobQuery query, CancellationToken cancellationToken = default) =>
        this.inner.ListPageAsync(query, cancellationToken);

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(
        BlobKey key,
        BlobDeleteOptions options = null,
        CancellationToken cancellationToken = default)
    {
        var result = await this.inner.DeleteAsync(key, options, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            await this.InvalidateAsync(key, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    private bool CanCache(BlobDownload download)
    {
        if (download?.Content is null || download.Info is null)
        {
            return false;
        }

        if (this.Options.MaxCachedBlobSize == 0)
        {
            return false;
        }

        return download.Info.Length <= this.Options.MaxCachedBlobSize;
    }

    private async Task<Result<BlobDownload>> CacheAndReturnAsync(
        string cacheKey,
        BlobDownload download,
        CancellationToken cancellationToken)
    {
        await using (download.ConfigureAwait(false))
        {
            using var buffer = new MemoryStream();
            await download.Content.CopyToAsync(buffer, this.Options.BufferSize, cancellationToken).ConfigureAwait(false);
            var bytes = buffer.ToArray();

            if (bytes.Length > this.Options.MaxCachedBlobSize)
            {
                return Result<BlobDownload>.Success(new BlobDownload
                {
                    Content = new MemoryStream(bytes, writable: false),
                    Info = download.Info
                });
            }

            await this.cacheProvider.SetAsync(
                cacheKey,
                new CacheBlobDownloadEntry
                {
                    Info = download.Info,
                    Content = bytes
                },
                this.Options.SlidingExpiration,
                this.Options.AbsoluteExpiration,
                cancellationToken).ConfigureAwait(false);

            TypedLogger.LogCacheSet(this.Logger, this.StoreName, bytes.Length);

            return Result<BlobDownload>.Success(new BlobDownload
            {
                Content = new MemoryStream(bytes, writable: false),
                Info = download.Info
            });
        }
    }

    private string CreateExactCacheKey(BlobKey key) =>
        $"{this.CreateCachePrefix()}download-{HashHelper.ComputeSha256($"{key?.Container}\n{key?.Name}")}";

    private string CreateCachePrefix() =>
        $"storage-blobs-{HashHelper.ComputeSha256(this.StoreName)}-";

    private async Task InvalidateAsync(BlobKey key, CancellationToken cancellationToken)
    {
        if (key is null)
        {
            return;
        }

        await this.cacheProvider.RemoveAsync(this.CreateExactCacheKey(key), cancellationToken).ConfigureAwait(false);
        await this.cacheProvider.RemoveStartsWithAsync(this.CreateCachePrefix(), cancellationToken).ConfigureAwait(false);
        TypedLogger.LogCacheInvalidated(this.Logger, this.StoreName);
    }

    private static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Debug, "blob download cache hit (store={StoreName})")]
        public static partial void LogCacheHit(ILogger logger, string storeName);

        [LoggerMessage(1, LogLevel.Debug, "blob download cache miss (store={StoreName})")]
        public static partial void LogCacheMiss(ILogger logger, string storeName);

        [LoggerMessage(2, LogLevel.Debug, "blob download cached (store={StoreName}, bytes={Bytes})")]
        public static partial void LogCacheSet(ILogger logger, string storeName, long bytes);

        [LoggerMessage(3, LogLevel.Debug, "blob download cache invalidated (store={StoreName})")]
        public static partial void LogCacheInvalidated(ILogger logger, string storeName);
    }
}
