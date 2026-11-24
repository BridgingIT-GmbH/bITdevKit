// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Applies configured operation behaviors around a binary collection while preserving the mandatory engine's result.</summary>
/// <param name="inner">The collection that performs mandatory cache work.</param>
/// <param name="binding">The validated effective collection binding used to describe each operation.</param>
/// <param name="tasks">The owner that observes behavior faults and drains outstanding runtime work.</param>
/// <param name="resolveIdentity">The collection's identity resolver, including its configured key limits.</param>
/// <param name="behaviors">The compatible behaviors in outermost-to-innermost execution order.</param>
/// <remarks>Use the catalog's collection resolver in application code. This wrapper supports custom collection composition.</remarks>
/// <example><code>
/// var observed = new CacheBinaryOperationBehavior(collection, binding, tasks,
///     resolveIdentity, behaviors);
/// </code></example>
public sealed class CacheBinaryOperationBehavior(ICacheBinaryCollection inner, CacheCollectionBinding binding, CacheRuntimeTasks tasks,
    Func<string, CacheEntryIdentity> resolveIdentity, IReadOnlyList<ICacheCollectionBehavior> behaviors) : ICacheBinaryCollection
{
    private readonly ICacheBinaryCollection inner = inner;
    private readonly CacheCollectionBinding binding = binding;
    private readonly Func<string, CacheEntryIdentity> resolveIdentity = resolveIdentity;
    private readonly CacheOperationPipeline<CacheLookup<ICacheReadHandle>> readPipeline = new(behaviors, tasks);
    private readonly CacheOperationPipeline<CacheWriteOutcome> writePipeline = new(behaviors, tasks);
    private readonly CacheOperationPipeline<IReadOnlyList<Result<CacheLookup<ICacheReadHandle>>>> batchPipeline = new(behaviors, tasks);
    private readonly CacheOperationPipeline<bool> actionPipeline = new(behaviors, tasks);
    private readonly CacheOperationPipeline<CacheInvalidationPreview> previewPipeline = new(behaviors, tasks);
    private readonly CacheOperationPipeline<CacheInvalidationOutcome> invalidationPipeline = new(behaviors, tasks);

    /// <inheritdoc />
    public string Name => this.inner.Name;

    /// <inheritdoc />
    public ValueTask<Result<CacheLookup<ICacheReadHandle>>> OpenReadAsync(string key, CacheReadOptions options = null, CancellationToken cancellationToken = default) =>
        this.readPipeline.ExecuteAsync(new CacheOperationContext(this.binding, CacheOperationKind.OpenRead, this.resolveIdentity(key)), token => this.inner.OpenReadAsync(key, options, token), cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheWriteOutcome>> SetAsync(CacheItem item, Stream input, CacheEntryDescriptor descriptor = null, CacheWriteCondition condition = default, CancellationToken cancellationToken = default) =>
        this.writePipeline.ExecuteAsync(new CacheOperationContext(this.binding, CacheOperationKind.Set, this.resolveIdentity(item.Key)), token => this.inner.SetAsync(item, input, descriptor, condition, token), cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheLookup<ICacheReadHandle>>> GetOrLoadAsync(CacheItem item, CacheBinaryLoadDelegate loader, CancellationToken cancellationToken = default) =>
        this.readPipeline.ExecuteAsync(new CacheOperationContext(this.binding, CacheOperationKind.GetOrLoad, this.resolveIdentity(item.Key)), token => this.inner.GetOrLoadAsync(item, loader, token), cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheLookup<ICacheReadHandle>>> GetOrLoadAsync(CacheItem item, CacheBinaryLoadDelegate loader, CacheAccessOptions options, CancellationToken cancellationToken = default) =>
        this.readPipeline.ExecuteAsync(new CacheOperationContext(this.binding, CacheOperationKind.GetOrLoad, this.resolveIdentity(item.Key)), token => this.inner.GetOrLoadAsync(item, loader, options, token), cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheLookup<ICacheReadHandle>>> RefreshAsync(CacheItem item, CacheBinaryLoadDelegate loader, CancellationToken cancellationToken = default) =>
        this.readPipeline.ExecuteAsync(new CacheOperationContext(this.binding, CacheOperationKind.Refresh, this.resolveIdentity(item.Key)), token => this.inner.RefreshAsync(item, loader, token), cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheLookup<ICacheReadHandle>>> RefreshAsync(CacheItem item, CacheBinaryLoadDelegate loader, CacheAccessOptions options, CancellationToken cancellationToken = default) =>
        this.readPipeline.ExecuteAsync(new CacheOperationContext(this.binding, CacheOperationKind.Refresh, this.resolveIdentity(item.Key)), token => this.inner.RefreshAsync(item, loader, options, token), cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<Result<CacheLookup<ICacheReadHandle>>>>> GetOrLoadManyAsync(IReadOnlyList<CacheItem> items, CacheBinaryLoadDelegate loader, CancellationToken cancellationToken = default) =>
        this.batchPipeline.ExecuteAsync(new CacheOperationContext(this.binding, CacheOperationKind.GetOrLoadMany), token => this.inner.GetOrLoadManyAsync(items, loader, token), cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<Result<CacheLookup<ICacheReadHandle>>>>> GetOrLoadManyAsync(IReadOnlyList<CacheItem> items, CacheBinaryBatchLoadDelegate loader, CancellationToken cancellationToken = default) =>
        this.batchPipeline.ExecuteAsync(new CacheOperationContext(this.binding, CacheOperationKind.GetOrLoadMany), token => this.inner.GetOrLoadManyAsync(items, loader, token), cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result> RemoveAsync(string key, CacheWriteCondition condition = default, CancellationToken cancellationToken = default) =>
        this.ActionAsync(this.actionPipeline, new CacheOperationContext(this.binding, CacheOperationKind.Remove, this.resolveIdentity(key)), token => this.inner.RemoveAsync(key, condition, token), cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result> TouchAsync(string key, CancellationToken cancellationToken = default) =>
        this.ActionAsync(this.actionPipeline, new CacheOperationContext(this.binding, CacheOperationKind.Touch, this.resolveIdentity(key)), token => this.inner.TouchAsync(key, token), cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheInvalidationPreview>> PreviewInvalidationAsync(CacheSelection selection, CancellationToken cancellationToken = default) =>
        this.previewPipeline.ExecuteAsync(new CacheOperationContext(this.binding, CacheOperationKind.PreviewInvalidation), token => this.inner.PreviewInvalidationAsync(selection, token), cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheInvalidationOutcome>> InvalidateAsync(CacheSelection selection, CancellationToken cancellationToken = default) =>
        this.invalidationPipeline.ExecuteAsync(new CacheOperationContext(this.binding, CacheOperationKind.Invalidate), token => this.inner.InvalidateAsync(selection, token), cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<CacheInvalidationOutcome>> ClearAsync(CancellationToken cancellationToken = default) =>
        this.invalidationPipeline.ExecuteAsync(new CacheOperationContext(this.binding, CacheOperationKind.Clear), token => this.inner.ClearAsync(token), cancellationToken);

    private async ValueTask<Result> ActionAsync(CacheOperationPipeline<bool> pipeline, CacheOperationContext context,
        Func<CancellationToken, ValueTask<Result>> action, CancellationToken cancellationToken)
    {
        var result = await pipeline.ExecuteAsync(context, async token =>
        {
            var actual = await action(token).ConfigureAwait(false);
            return actual.IsSuccess ? Result<bool>.Success(true).WithMessages(actual.Messages) : Result<bool>.Failure((IResult)actual);
        }, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Result.Success(result.Messages) : Result.Failure().WithErrors(result.Errors).WithMessages(result.Messages);
    }
}