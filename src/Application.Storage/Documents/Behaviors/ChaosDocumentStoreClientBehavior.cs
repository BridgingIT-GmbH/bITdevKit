// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>Injects configured faults before document operations for resilience testing.</summary>
/// <typeparam name="T">The document type.</typeparam>
/// <example><code>var behavior = new ChaosDocumentStoreClientBehavior&lt;Person&gt;(loggerFactory, inner);</code></example>
public class ChaosDocumentStoreClientBehavior<T>(ILoggerFactory loggerFactory, IDocumentStoreClient<T> inner, ChaosDocumentStoreClientBehaviorOptions options = null)
    : DocumentStoreClientBehaviorBase<T>(inner) where T : class, new()
{
    private readonly ILogger<ChaosDocumentStoreClientBehavior<T>> logger = loggerFactory?.CreateLogger<ChaosDocumentStoreClientBehavior<T>>() ?? NullLogger<ChaosDocumentStoreClientBehavior<T>>.Instance;
    private readonly ChaosDocumentStoreClientBehaviorOptions options = options ?? new();

    /// <inheritdoc />
    public override Task<Result<DocumentEntry<T>>> GetAsync(DocumentKey key, CancellationToken cancellationToken = default) { this.MaybeThrow(); return base.GetAsync(key, cancellationToken); }
    /// <inheritdoc />
    public override Task<Result<DocumentInfo>> UpsertAsync(DocumentKey key, T value, DocumentWriteOptions options = null, CancellationToken cancellationToken = default) { this.MaybeThrow(); return base.UpsertAsync(key, value, options, cancellationToken); }
    /// <inheritdoc />
    public override Task<Result> DeleteAsync(DocumentKey key, DocumentDeleteOptions options = null, CancellationToken cancellationToken = default) { this.MaybeThrow(); return base.DeleteAsync(key, options, cancellationToken); }

    private void MaybeThrow()
    {
        if (Random.Shared.NextDouble() < this.options.InjectionRate)
        {
            this.logger.LogWarning("{LogKey} document chaos fault injected (type={DocumentType})", Constants.LogKey, typeof(T).Name);
            throw this.options.Fault;
        }
    }
}
