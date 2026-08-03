// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>Retries provider failures sequentially without overlapping attempts.</summary>
/// <typeparam name="T">The document type.</typeparam>
/// <example><code>var behavior = new RetryDocumentStoreClientBehavior&lt;Person&gt;(loggerFactory, inner);</code></example>
public class RetryDocumentStoreClientBehavior<T>(ILoggerFactory loggerFactory, IDocumentStoreClient<T> inner, RetryDocumentStoreClientBehaviorOptions options = null)
    : DocumentStoreClientBehaviorBase<T>(inner) where T : class, new()
{
    private readonly ILogger<RetryDocumentStoreClientBehavior<T>> logger = loggerFactory?.CreateLogger<RetryDocumentStoreClientBehavior<T>>() ?? NullLogger<RetryDocumentStoreClientBehavior<T>>.Instance;
    private readonly RetryDocumentStoreClientBehaviorOptions options = options ?? new();

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
        var attempts = Math.Max(1, this.options.Attempts);
        Result<TResult> result = null;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result = await operation(cancellationToken);
            if (result.IsSuccess || !IsRetryable(result) || attempt == attempts) return result;
            this.logger.LogWarning("{LogKey} document operation retry (type={DocumentType}, attempt={Attempt})", Constants.LogKey, typeof(T).Name, attempt);
            var factor = this.options.BackoffExponential ? Math.Pow(2, attempt - 1) : 1;
            await Task.Delay(TimeSpan.FromMilliseconds(this.options.Backoff.TotalMilliseconds * factor), cancellationToken);
        }
        return result;
    }

    private static bool IsRetryable<TResult>(Result<TResult> result) => result.Errors.All(error => error is DocumentStoreProviderError);

    private async Task<Result> ExecuteAsync(Func<CancellationToken, Task<Result>> operation, CancellationToken cancellationToken)
    {
        var attempts = Math.Max(1, this.options.Attempts);
        Result result = default;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result = await operation(cancellationToken);
            if (result.IsSuccess || result.Errors.Any(error => error is not DocumentStoreProviderError) || attempt == attempts) return result;
            this.logger.LogWarning("{LogKey} document operation retry (type={DocumentType}, attempt={Attempt})", Constants.LogKey, typeof(T).Name, attempt);
            var factor = this.options.BackoffExponential ? Math.Pow(2, attempt - 1) : 1;
            await Task.Delay(TimeSpan.FromMilliseconds(this.options.Backoff.TotalMilliseconds * factor), cancellationToken);
        }
        return result;
    }
}
