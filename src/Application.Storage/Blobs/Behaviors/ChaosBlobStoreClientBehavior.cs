// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System.Threading;

/// <summary>
/// Injects Result-native upload and download failures for blob-store resilience testing.
/// </summary>
/// <example>
/// <code>
/// services.AddBlobStorage()
///     .WithChaosBehavior(options => options.FailDownloadsEvery = 3)
///     .WithInMemoryClient("reports");
/// </code>
/// </example>
public sealed class ChaosBlobStoreClientBehavior : IBlobStoreClient
{
    private readonly IBlobStoreClient inner;
    private readonly ChaosBlobStoreClientBehaviorOptions options;
    private int downloadCount;
    private int uploadCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChaosBlobStoreClientBehavior" /> class.
    /// </summary>
    /// <param name="inner">The decorated blob-store client.</param>
    /// <param name="options">The chaos options.</param>
    /// <param name="storeName">The configured blob-store client name.</param>
    /// <example>
    /// <code>
    /// var behavior = new ChaosBlobStoreClientBehavior(inner, options, "reports");
    /// </code>
    /// </example>
    public ChaosBlobStoreClientBehavior(
        IBlobStoreClient inner,
        ChaosBlobStoreClientBehaviorOptions options = null,
        string storeName = null)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.options = options ?? new ChaosBlobStoreClientBehaviorOptions();
        this.StoreName = string.IsNullOrWhiteSpace(storeName) ? "default" : storeName;
    }

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
    public Task<Result<BlobInfo>> UploadAsync(
        BlobUpload upload,
        CancellationToken cancellationToken = default)
    {
        var validation = this.options.Validate();
        if (validation.IsFailure)
        {
            return Task.FromResult(Result<BlobInfo>.Failure(validation));
        }

        if (this.ShouldFail(BlobStoreChaosOperation.Upload))
        {
            return Task.FromResult(Result<BlobInfo>.Failure(this.CreateFailure(BlobStoreChaosOperation.Upload)));
        }

        return this.inner.UploadAsync(upload, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<BlobDownload>> DownloadAsync(
        BlobKey key,
        CancellationToken cancellationToken = default)
    {
        var validation = this.options.Validate();
        if (validation.IsFailure)
        {
            return Task.FromResult(Result<BlobDownload>.Failure(validation));
        }

        if (this.ShouldFail(BlobStoreChaosOperation.Download))
        {
            return Task.FromResult(Result<BlobDownload>.Failure(this.CreateFailure(BlobStoreChaosOperation.Download)));
        }

        return this.inner.DownloadAsync(key, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<BlobInfo>> GetPropertiesAsync(BlobKey key, CancellationToken cancellationToken = default) =>
        this.inner.GetPropertiesAsync(key, cancellationToken);

    /// <inheritdoc />
    public Task<Result<BlobInfo>> UpdatePropertiesAsync(BlobPropertiesUpdate update, CancellationToken cancellationToken = default) =>
        this.inner.UpdatePropertiesAsync(update, cancellationToken);

    /// <inheritdoc />
    public Task<Result<bool>> ExistsAsync(BlobKey key, CancellationToken cancellationToken = default) =>
        this.inner.ExistsAsync(key, cancellationToken);

    /// <inheritdoc />
    public Task<Result<BlobPage>> ListPageAsync(BlobQuery query, CancellationToken cancellationToken = default) =>
        this.inner.ListPageAsync(query, cancellationToken);

    /// <inheritdoc />
    public Task<Result> DeleteAsync(
        BlobKey key,
        BlobDeleteOptions options = null,
        CancellationToken cancellationToken = default) =>
        this.inner.DeleteAsync(key, options, cancellationToken);

    private bool ShouldFail(BlobStoreChaosOperation operation)
    {
        if (!this.options.Enabled)
        {
            return false;
        }

        var interval = operation is BlobStoreChaosOperation.Upload
            ? this.options.FailUploadsEvery
            : this.options.FailDownloadsEvery;
        if (interval is > 0)
        {
            var count = operation is BlobStoreChaosOperation.Upload
                ? Interlocked.Increment(ref this.uploadCount)
                : Interlocked.Increment(ref this.downloadCount);

            return count % interval.Value == 0;
        }

        var rate = operation is BlobStoreChaosOperation.Upload
            ? this.options.UploadFailureRate
            : this.options.DownloadFailureRate;

        return rate > 0D && (rate >= 1D || this.options.RandomDoubleFactory() < rate);
    }

    private BlobStoreProviderError CreateFailure(BlobStoreChaosOperation operation) =>
        new($"{this.options.Message} Operation={operation.ToString().ToLowerInvariant()}; Store={this.StoreName}.");
}
