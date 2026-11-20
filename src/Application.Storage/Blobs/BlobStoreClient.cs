// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Validates provider-neutral blob operations before invoking the configured behavior pipeline.
/// </summary>
/// <example>
/// <code>
/// var client = new BlobStoreClient("inmemory", provider, new BlobStoreOptions());
/// var result = await client.ExistsAsync(new BlobKey("reports", "2026/report.pdf"));
/// </code>
/// </example>
public sealed class BlobStoreClient : IBlobStoreClient, IBlobStoreContainerCatalog, IBlobStoreClientDecorator, IBlobDimensionDiagnosticsProvider
{
    private readonly IBlobStoreProvider provider;
    private readonly string providerName;
    private readonly IBlobStoreClient inner;
    private readonly BlobStoreOptions options;
    private readonly BlobStoreProviderCapabilities capabilities;
    private readonly IContinuationTokenProtector continuationTokenProtector;

    /// <summary>
    /// Gets the validated client's configured behavior pipeline.
    /// </summary>
    /// <example>
    /// <code>
    /// var pipeline = client.InnerClient;
    /// </code>
    /// </example>
    public IBlobStoreClient InnerClient => this.inner;

    /// <inheritdoc />
    public BlobDimensionCapabilities DimensionCapabilities => this.capabilities.Dimensions;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlobStoreClient" /> class.
    /// </summary>
    /// <param name="providerName">The provider discriminator used for continuation-token validation.</param>
    /// <param name="provider">The provider used to execute blob operations.</param>
    /// <param name="options">The validation options.</param>
    /// <param name="decorate">An optional behavior pipeline applied inside validation.</param>
    /// <param name="continuationTokenProtector">The optional continuation-token protector.</param>
    /// <example>
    /// <code>
    /// var client = new BlobStoreClient("custom", provider, options, inner => behavior(inner));
    /// </code>
    /// </example>
    public BlobStoreClient(
        string providerName,
        IBlobStoreProvider provider,
        BlobStoreOptions options = null,
        Func<IBlobStoreClient, IBlobStoreClient> decorate = null,
        IContinuationTokenProtector continuationTokenProtector = null)
    {
        this.providerName = string.IsNullOrWhiteSpace(providerName) ? "custom" : providerName;
        ArgumentNullException.ThrowIfNull(provider);
        this.provider = provider;
        this.options = options ?? new BlobStoreOptions();
        this.capabilities = provider.Capabilities;
        this.continuationTokenProtector = continuationTokenProtector;
        var providerClient = new ProviderClient(provider);
        this.inner = decorate?.Invoke(providerClient) ?? providerClient;
    }

    /// <inheritdoc />
    public Task<Result<BlobDimensionDiagnostics>> GetDimensionDiagnosticsAsync(bool includeDictionaryCount = false, CancellationToken cancellationToken = default) =>
        this.provider is IBlobDimensionDiagnosticsProvider diagnostics ? diagnostics.GetDimensionDiagnosticsAsync(includeDictionaryCount, cancellationToken)
            : Task.FromResult(Result<BlobDimensionDiagnostics>.Failure(new BlobDimensionsNotSupportedError()));

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<string>>> ListContainersAsync(CancellationToken cancellationToken = default) =>
        this.provider is IBlobStoreContainerCatalog catalog
            ? catalog.ListContainersAsync(cancellationToken)
            : Task.FromResult(Result<IReadOnlyList<string>>.Failure(
                new BlobStoreQueryNotSupportedError("Provider does not support container discovery.")));

    /// <inheritdoc />
    public async Task<Result<BlobInfo>> UploadAsync(BlobUpload upload, CancellationToken cancellationToken = default)
    {
        if (upload?.Dimensions is not null && !this.capabilities.Dimensions.SupportsDimensions)
        {
            return Result<BlobInfo>.Failure(new BlobDimensionsNotSupportedError());
        }

        upload = BlobStoredModelMapper.Snapshot(upload);
        var validation = BlobValidator.Validate(upload, this.options);
        return validation.IsFailure
            ? ToFailure<BlobInfo>(validation)
            : await this.inner.UploadAsync(upload, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<Result<BlobUploadBatchResult>> UploadManyAsync(IReadOnlyCollection<BlobUpload> uploads,
        BlobUploadBatchOptions options = null, CancellationToken cancellationToken = default) =>
        BlobUploadBatchExecutor.ExecuteAsync(this, uploads, options, cancellationToken);

    /// <inheritdoc />
    public async Task<Result<BlobDownload>> DownloadAsync(BlobKey key, CancellationToken cancellationToken = default)
    {
        var validation = BlobValidator.Validate(key);
        return validation.IsFailure
            ? ToFailure<BlobDownload>(validation)
            : await this.inner.DownloadAsync(key, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result<BlobInfo>> GetPropertiesAsync(BlobKey key, CancellationToken cancellationToken = default)
    {
        var validation = BlobValidator.Validate(key);
        return validation.IsFailure
            ? ToFailure<BlobInfo>(validation)
            : await this.inner.GetPropertiesAsync(key, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result<BlobInfo>> UpdatePropertiesAsync(
        BlobPropertiesUpdate update,
        CancellationToken cancellationToken = default)
    {
        update = BlobStoredModelMapper.Snapshot(update);
        var validation = BlobValidator.Validate(update);
        return validation.IsFailure
            ? ToFailure<BlobInfo>(validation)
            : await this.inner.UpdatePropertiesAsync(update, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> ExistsAsync(BlobKey key, CancellationToken cancellationToken = default)
    {
        var validation = BlobValidator.Validate(key);
        return validation.IsFailure
            ? ToFailure<bool>(validation)
            : await this.inner.ExistsAsync(key, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result<BlobPage>> ListPageAsync(BlobQuery query, CancellationToken cancellationToken = default)
    {
        var validation = BlobQueryValidator.NormalizeAndValidate(
            this.providerName,
            query,
            this.options,
            this.capabilities,
            this.continuationTokenProtector);
        if (validation.IsFailure)
        {
            return ToFailure<BlobPage>(validation);
        }

        return await this.inner.ListPageAsync(validation.Value.Query, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(
        BlobKey key,
        BlobDeleteOptions options = null,
        CancellationToken cancellationToken = default)
    {
        var validation = BlobValidator.Validate(key);
        if (validation.IsFailure)
        {
            return Result.Failure().WithErrors(validation.Errors).WithMessages(validation.Messages);
        }

        var condition = BlobValidator.Validate(options);
        return condition.IsFailure ? condition : await this.inner.DeleteAsync(key, options, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result<BlobInfo>> UpdateDimensionsAsync(BlobDimensionsUpdate update, CancellationToken cancellationToken = default)
    {
        if (!this.capabilities.Dimensions.SupportsDimensions)
        {
            return Result<BlobInfo>.Failure(new BlobDimensionsNotSupportedError());
        }

        var validation = BlobValidator.Validate(update?.Key);
        if (validation.IsFailure)
        {
            return Result<BlobInfo>.Failure(validation);
        }

        if (update.Dimensions is null)
        {
            return Result<BlobInfo>.Failure(new BlobStoreValidationError("Dimensions input is required."));
        }

        var condition = BlobValidator.Validate(new BlobDeleteOptions { IfMatchETag = update.IfMatchETag });
        return condition.IsFailure ? Result<BlobInfo>.Failure(condition) : await this.inner.UpdateDimensionsAsync(update, cancellationToken).ConfigureAwait(false);
    }

    private static Result<T> ToFailure<T>(Result validation) =>
        Result<T>.Failure().WithErrors(validation.Errors).WithMessages(validation.Messages);

    private sealed class ProviderClient(IBlobStoreProvider provider) : IBlobStoreClient
    {
        public BlobStoreProviderCapabilities Capabilities => provider.Capabilities;

        public BlobDimensionCapabilities DimensionCapabilities => provider.Capabilities.Dimensions;

        /// <inheritdoc />
        public async Task<Result<BlobInfo>> UpdateDimensionsAsync(BlobDimensionsUpdate update, CancellationToken cancellationToken = default) =>
            BlobStoredModelMapper.Map(await provider.UpdateDimensionsAsync(update, cancellationToken).ConfigureAwait(false), BlobStoredModelMapper.ToClient);

        public async Task<Result<BlobInfo>> UploadAsync(BlobUpload upload, CancellationToken cancellationToken = default) =>
            BlobStoredModelMapper.Map(await provider.UploadAsync(BlobStoredModelMapper.ToStored(upload), cancellationToken).ConfigureAwait(false), BlobStoredModelMapper.ToClient);

        public async Task<Result<BlobDownload>> DownloadAsync(BlobKey key, CancellationToken cancellationToken = default) =>
            BlobStoredModelMapper.Map(provider is IBlobStoreBatchDownloadProvider batchProvider && BlobDownloadBatchScope.Current is { } scope
                ? await scope.DownloadAsync(provider, batchProvider, key, cancellationToken).ConfigureAwait(false)
                : await provider.DownloadAsync(key, cancellationToken).ConfigureAwait(false), BlobStoredModelMapper.ToClient);

        public async Task<Result<BlobInfo>> GetPropertiesAsync(BlobKey key, CancellationToken cancellationToken = default) =>
            BlobStoredModelMapper.Map(await provider.GetPropertiesAsync(key, cancellationToken).ConfigureAwait(false), BlobStoredModelMapper.ToClient);

        public async Task<Result<BlobInfo>> UpdatePropertiesAsync(
            BlobPropertiesUpdate update,
            CancellationToken cancellationToken = default) =>
            BlobStoredModelMapper.Map(await provider.UpdatePropertiesAsync(BlobStoredModelMapper.ToStored(update), cancellationToken).ConfigureAwait(false), BlobStoredModelMapper.ToClient);

        public Task<Result<bool>> ExistsAsync(BlobKey key, CancellationToken cancellationToken = default) =>
            provider.ExistsAsync(key, cancellationToken);

        public async Task<Result<BlobPage>> ListPageAsync(BlobQuery query, CancellationToken cancellationToken = default) =>
            BlobStoredModelMapper.Map(await provider.ListPageAsync(query, cancellationToken).ConfigureAwait(false), BlobStoredModelMapper.ToClient);

        public Task<Result> DeleteAsync(
            BlobKey key,
            BlobDeleteOptions options = null,
            CancellationToken cancellationToken = default) =>
            provider.DeleteAsync(key, options, cancellationToken);
    }
}