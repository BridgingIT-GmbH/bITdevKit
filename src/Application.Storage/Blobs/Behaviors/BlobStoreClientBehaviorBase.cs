// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Provides a reusable base for blob-store client decorators.
/// </summary>
/// <param name="inner">The decorated blob-store client.</param>
/// <param name="storeName">The configured blob-store client name.</param>
/// <example>
/// <code>
/// public sealed class CustomBehavior(IBlobStoreClient inner) : BlobStoreClientBehaviorBase(inner);
/// </code>
/// </example>
public abstract class BlobStoreClientBehaviorBase(
    IBlobStoreClient inner,
    string storeName = null) : IBlobStoreClient, IBlobStoreClientDecorator
{
    /// <summary>
    /// Gets the decorated inner blob-store client.
    /// </summary>
    /// <example>
    /// <code>
    /// var inner = this.Inner;
    /// </code>
    /// </example>
    protected IBlobStoreClient Inner { get; } = inner ?? throw new ArgumentNullException(nameof(inner));

    /// <inheritdoc />
    public IBlobStoreClient InnerClient => this.Inner;

    /// <summary>
    /// Gets the configured blob-store client name.
    /// </summary>
    /// <example>
    /// <code>
    /// var store = this.StoreName;
    /// </code>
    /// </example>
    protected string StoreName { get; } = string.IsNullOrWhiteSpace(storeName) ? "default" : storeName;

    /// <inheritdoc />
    public Task<Result<BlobInfo>> UploadAsync(BlobUpload upload, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(
            "upload",
            BlobStoreOperationContext.ForUpload(upload),
            token => this.Inner.UploadAsync(upload, token),
            cancellationToken);

    /// <inheritdoc />
    public Task<Result<BlobDownload>> DownloadAsync(BlobKey key, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(
            "download",
            BlobStoreOperationContext.ForKey(key),
            token => this.Inner.DownloadAsync(key, token),
            cancellationToken);

    /// <inheritdoc />
    public Task<Result<BlobInfo>> GetPropertiesAsync(BlobKey key, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(
            "get_properties",
            BlobStoreOperationContext.ForKey(key),
            token => this.Inner.GetPropertiesAsync(key, token),
            cancellationToken);

    /// <inheritdoc />
    public Task<Result<BlobInfo>> UpdatePropertiesAsync(BlobPropertiesUpdate update, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(
            "update_properties",
            BlobStoreOperationContext.ForUpdate(update),
            token => this.Inner.UpdatePropertiesAsync(update, token),
            cancellationToken);

    /// <inheritdoc />
    public Task<Result<bool>> ExistsAsync(BlobKey key, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(
            "exists",
            BlobStoreOperationContext.ForKey(key),
            token => this.Inner.ExistsAsync(key, token),
            cancellationToken);

    /// <inheritdoc />
    public Task<Result<BlobPage>> ListPageAsync(BlobQuery query, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(
            "list_page",
            BlobStoreOperationContext.ForQuery(query),
            token => this.Inner.ListPageAsync(query, token),
            cancellationToken);

    /// <inheritdoc />
    public Task<Result> DeleteAsync(
        BlobKey key,
        BlobDeleteOptions options = null,
        CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(
            "delete",
            BlobStoreOperationContext.ForKey(key),
            token => this.Inner.DeleteAsync(key, options, token),
            cancellationToken);

    /// <summary>
    /// Executes one value-returning blob operation.
    /// </summary>
    /// <typeparam name="T">The result value type.</typeparam>
    /// <param name="operation">The low-cardinality operation name.</param>
    /// <param name="context">The sanitized operation context.</param>
    /// <param name="next">The next client delegate.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns>The operation result.</returns>
    /// <example>
    /// <code>
    /// return await this.ExecuteAsync("exists", context, next, cancellationToken);
    /// </code>
    /// </example>
    protected abstract Task<Result<T>> ExecuteAsync<T>(
        string operation,
        BlobStoreOperationContext context,
        Func<CancellationToken, Task<Result<T>>> next,
        CancellationToken cancellationToken);

    /// <summary>
    /// Executes one non-value blob operation.
    /// </summary>
    /// <param name="operation">The low-cardinality operation name.</param>
    /// <param name="context">The sanitized operation context.</param>
    /// <param name="next">The next client delegate.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns>The operation result.</returns>
    /// <example>
    /// <code>
    /// return await this.ExecuteAsync("delete", context, next, cancellationToken);
    /// </code>
    /// </example>
    protected abstract Task<Result> ExecuteAsync(
        string operation,
        BlobStoreOperationContext context,
        Func<CancellationToken, Task<Result>> next,
        CancellationToken cancellationToken);
}

/// <summary>
/// Describes sanitized context for one blob-store client operation.
/// </summary>
/// <example>
/// <code>
/// var context = BlobStoreOperationContext.ForKey(new BlobKey("reports", "2026/report.pdf"));
/// </code>
/// </example>
public sealed class BlobStoreOperationContext
{
    private BlobStoreOperationContext() { }

    /// <summary>
    /// Gets the exact blob key when available.
    /// </summary>
    /// <example>
    /// <code>
    /// var container = context.Key?.Container;
    /// </code>
    /// </example>
    public BlobKey Key { get; private init; }

    /// <summary>
    /// Gets the upload model when the operation is an upload.
    /// </summary>
    /// <example>
    /// <code>
    /// var canRead = context.Upload?.Content?.CanRead;
    /// </code>
    /// </example>
    public BlobUpload Upload { get; private init; }

    /// <summary>
    /// Gets the property update model when the operation updates properties.
    /// </summary>
    /// <example>
    /// <code>
    /// var propertyCount = context.Update?.Properties?.Count;
    /// </code>
    /// </example>
    public BlobPropertiesUpdate Update { get; private init; }

    /// <summary>
    /// Gets the query model when the operation lists blobs.
    /// </summary>
    /// <example>
    /// <code>
    /// var take = context.Query?.Take;
    /// </code>
    /// </example>
    public BlobQuery Query { get; private init; }

    /// <summary>
    /// Creates operation context from an exact blob key.
    /// </summary>
    /// <param name="key">The blob key.</param>
    /// <returns>The operation context.</returns>
    /// <example>
    /// <code>
    /// var context = BlobStoreOperationContext.ForKey(key);
    /// </code>
    /// </example>
    public static BlobStoreOperationContext ForKey(BlobKey key) => new() { Key = key };

    /// <summary>
    /// Creates operation context from an upload model.
    /// </summary>
    /// <param name="upload">The upload model.</param>
    /// <returns>The operation context.</returns>
    /// <example>
    /// <code>
    /// var context = BlobStoreOperationContext.ForUpload(upload);
    /// </code>
    /// </example>
    public static BlobStoreOperationContext ForUpload(BlobUpload upload) => new()
    {
        Key = upload?.Key,
        Upload = upload
    };

    /// <summary>
    /// Creates operation context from a property update model.
    /// </summary>
    /// <param name="update">The property update model.</param>
    /// <returns>The operation context.</returns>
    /// <example>
    /// <code>
    /// var context = BlobStoreOperationContext.ForUpdate(update);
    /// </code>
    /// </example>
    public static BlobStoreOperationContext ForUpdate(BlobPropertiesUpdate update) => new()
    {
        Key = update?.Key,
        Update = update
    };

    /// <summary>
    /// Creates operation context from a listing query.
    /// </summary>
    /// <param name="query">The listing query.</param>
    /// <returns>The operation context.</returns>
    /// <example>
    /// <code>
    /// var context = BlobStoreOperationContext.ForQuery(query);
    /// </code>
    /// </example>
    public static BlobStoreOperationContext ForQuery(BlobQuery query) => new() { Query = query };
}
