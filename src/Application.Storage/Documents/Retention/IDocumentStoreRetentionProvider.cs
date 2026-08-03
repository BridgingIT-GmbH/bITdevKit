// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Defines bounded provider-native cleanup of physical document records whose logical expiration is due.
/// </summary>
/// <remarks>
/// Retention is intentionally separate from <see cref="IDocumentStoreClient{T}" />. Implementations use indexed or native
/// expiration queries and conditional deletion, leases, or transactions so concurrent updates and multiple application
/// nodes cannot delete a document whose expiration was extended or cleared. Public document scans must not emulate cleanup.
/// </remarks>
/// <example>
/// <code>
/// var result = await provider.SweepExpiredAsync(request, cancellationToken);
/// </code>
/// </example>
public interface IDocumentStoreRetentionProvider
{
    /// <summary>
    /// Deletes due physical records in bounded, provider-native batches.
    /// </summary>
    /// <param name="request">
    /// The validated type namespace, cutoff, batch bounds, and inter-batch delay.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel the sweep and any configured batch delay.
    /// </param>
    /// <returns>
    /// A result containing deletion progress and whether more due records may remain. Already-deleted records are treated
    /// as idempotent cleanup rather than failures.
    /// </returns>
    /// <example>
    /// <code>
    /// var result = await provider.SweepExpiredAsync(request, cancellationToken);
    /// </code>
    /// </example>
    Task<Result<DocumentRetentionSweepResult>> SweepExpiredAsync(DocumentRetentionSweepRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Configures one bounded physical-retention sweep for a persisted document type namespace.
/// </summary>
/// <example>
/// <code>
/// var request = new DocumentRetentionSweepRequest
/// {
///     DocumentType = DocumentTypeIdentity.For&lt;Customer&gt;(),
///     VisibilityCutoff = timeProvider.GetUtcNow(),
///     BatchSize = 500,
///     MaxBatches = 4
/// };
/// </code>
/// </example>
public sealed record DocumentRetentionSweepRequest
{
    /// <summary>
    /// Gets the stable persisted document type namespace to sweep.
    /// </summary>
    /// <example>
    /// <code>
    /// var type = request.DocumentType;
    /// </code>
    /// </example>
    public required DocumentTypeIdentity DocumentType { get; init; }

    /// <summary>
    /// Gets the inclusive UTC expiration cutoff; records expiring at or before this instant are due.
    /// </summary>
    /// <example>
    /// <code>
    /// var cutoff = request.VisibilityCutoff;
    /// </code>
    /// </example>
    public DateTimeOffset VisibilityCutoff { get; init; }

    /// <summary>
    /// Gets the positive maximum number of physical records considered in one provider batch.
    /// </summary>
    /// <example>
    /// <code>
    /// var take = request.BatchSize;
    /// </code>
    /// </example>
    public int BatchSize { get; init; } = 1000;

    /// <summary>
    /// Gets the positive maximum number of batches processed by this sweep.
    /// </summary>
    /// <example>
    /// <code>
    /// var maxBatches = request.MaxBatches;
    /// </code>
    /// </example>
    public int MaxBatches { get; init; } = 10;

    /// <summary>
    /// Gets the non-negative delay applied between completed batches.
    /// </summary>
    /// <example>
    /// <code>
    /// var delay = request.BatchDelay;
    /// </code>
    /// </example>
    public TimeSpan BatchDelay { get; init; }
}

/// <summary>
/// Reports bounded physical cleanup performed by one provider retention sweep.
/// </summary>
/// <example>
/// <code>
/// Console.WriteLine($"Deleted {result.DeletedCount} document records.");
/// </code>
/// </example>
public sealed record DocumentRetentionSweepResult
{
    /// <summary>
    /// Gets the stable persisted document type namespace that was swept.
    /// </summary>
    /// <example>
    /// <code>
    /// var type = result.DocumentType;
    /// </code>
    /// </example>
    public required DocumentTypeIdentity DocumentType { get; init; }

    /// <summary>
    /// Gets the total number of physical records successfully deleted.
    /// </summary>
    /// <example>
    /// <code>
    /// var deleted = result.DeletedCount;
    /// </code>
    /// </example>
    public long DeletedCount { get; init; }

    /// <summary>
    /// Gets the exact document keys successfully deleted by this sweep.
    /// </summary>
    /// <example>
    /// <code>
    /// foreach (var key in result.DeletedKeys) { Console.WriteLine(key.RowKey); }
    /// </code>
    /// </example>
    public IReadOnlyList<DocumentKey> DeletedKeys { get; init; } = [];

    /// <summary>
    /// Gets the number of provider batches processed, including a final partially filled batch.
    /// </summary>
    /// <example>
    /// <code>
    /// var batches = result.BatchCount;
    /// </code>
    /// </example>
    public int BatchCount { get; init; }

    /// <summary>
    /// Gets whether the configured bounds were reached and more due physical records may remain.
    /// </summary>
    /// <example>
    /// <code>
    /// if (result.HasMore) { /* schedule another bounded sweep */ }
    /// </code>
    /// </example>
    public bool HasMore { get; init; }
}
