// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>Applies a deadline, cancels timed-out work, and waits for it to quiesce.</summary>
/// <typeparam name="T">The document type.</typeparam>
/// <example><code>var behavior = new TimeoutDocumentStoreClientBehavior&lt;Person&gt;(loggerFactory, inner);</code></example>
public class TimeoutDocumentStoreClientBehavior<T>(ILoggerFactory loggerFactory, IDocumentStoreClient<T> inner, TimeoutDocumentStoreClientBehaviorOptions options = null, TimeProvider timeProvider = null)
    : DocumentStoreClientBehaviorBase<T>(inner) where T : class, new()
{
    private readonly ILogger<TimeoutDocumentStoreClientBehavior<T>> logger = loggerFactory?.CreateLogger<TimeoutDocumentStoreClientBehavior<T>>() ?? NullLogger<TimeoutDocumentStoreClientBehavior<T>>.Instance;
    private readonly TimeoutDocumentStoreClientBehaviorOptions options = options ?? new();
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    /// <inheritdoc />
    public override Task<Result<DocumentEntry<T>>> GetAsync(DocumentKey key, CancellationToken cancellationToken = default) => this.ExecuteAsync(ct => this.Inner.GetAsync(key, ct), cancellationToken);
    /// <inheritdoc />
    public override Task<Result<DocumentPage<T>>> FindPageAsync(DocumentQuery query, CancellationToken cancellationToken = default) => this.ExecuteAsync(ct => this.Inner.FindPageAsync(query, ct), cancellationToken);
    /// <inheritdoc />
    public override Task<Result<DocumentKeyPage>> ListPageAsync(DocumentQuery query, CancellationToken cancellationToken = default) => this.ExecuteAsync(ct => this.Inner.ListPageAsync(query, ct), cancellationToken);
    /// <inheritdoc />
    public override Task<Result<long>> CountAsync(DocumentCountQuery query, CancellationToken cancellationToken = default) => this.ExecuteAsync(ct => this.Inner.CountAsync(query, ct), cancellationToken);
    /// <inheritdoc />
    public override Task<Result<bool>> ExistsAsync(DocumentKey key, CancellationToken cancellationToken = default) => this.ExecuteAsync(ct => this.Inner.ExistsAsync(key, ct), cancellationToken);
    /// <inheritdoc />
    public override Task<Result<DocumentInfo>> UpsertAsync(DocumentKey key, T value, DocumentWriteOptions options = null, CancellationToken cancellationToken = default) => this.ExecuteAsync(ct => this.Inner.UpsertAsync(key, value, options, ct), cancellationToken);
    /// <inheritdoc />
    public override Task<Result<DocumentBatchResult<DocumentInfo>>> UpsertManyAsync(IReadOnlyCollection<DocumentWrite<T>> writes, CancellationToken cancellationToken = default) => this.ExecuteAsync(ct => this.Inner.UpsertManyAsync(writes, ct), cancellationToken);
    /// <inheritdoc />
    public override Task<Result<DocumentInfo>> UpdatePropertiesAsync(DocumentPropertiesUpdate update, CancellationToken cancellationToken = default) => this.ExecuteAsync(ct => this.Inner.UpdatePropertiesAsync(update, ct), cancellationToken);
    /// <inheritdoc />
    public override Task<Result> DeleteAsync(DocumentKey key, DocumentDeleteOptions options = null, CancellationToken cancellationToken = default) => this.ExecuteAsync(ct => this.Inner.DeleteAsync(key, options, ct), cancellationToken);
    /// <inheritdoc />
    public override Task<Result<DocumentBatchResult<DocumentKey>>> DeleteManyAsync(IReadOnlyCollection<DocumentDelete> deletes, CancellationToken cancellationToken = default) => this.ExecuteAsync(ct => this.Inner.DeleteManyAsync(deletes, ct), cancellationToken);

    private async Task<Result<TResult>> ExecuteAsync<TResult>(Func<CancellationToken, Task<Result<TResult>>> operation, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var task = operation(linked.Token);
        var deadline = Task.Delay(this.options.Timeout, this.timeProvider, cancellationToken);
        if (await Task.WhenAny(task, deadline) == task) return await task;

        linked.Cancel();
        this.logger.LogWarning("{LogKey} document operation timed out (type={DocumentType}, timeout={Timeout})", Constants.LogKey, typeof(T).Name, this.options.Timeout);
        try { await task; } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
        cancellationToken.ThrowIfCancellationRequested();
        return Result<TResult>.Failure(new DocumentStoreTimeoutError($"Operation exceeded {this.options.Timeout}."));
    }

    private async Task<Result> ExecuteAsync(Func<CancellationToken, Task<Result>> operation, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var task = operation(linked.Token);
        var deadline = Task.Delay(this.options.Timeout, this.timeProvider, cancellationToken);
        if (await Task.WhenAny(task, deadline) == task) return await task;

        linked.Cancel();
        this.logger.LogWarning("{LogKey} document operation timed out (type={DocumentType}, timeout={Timeout})", Constants.LogKey, typeof(T).Name, this.options.Timeout);
        try { await task; } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
        cancellationToken.ThrowIfCancellationRequested();
        return Result.Failure(new DocumentStoreTimeoutError($"Operation exceeded {this.options.Timeout}."));
    }
}
