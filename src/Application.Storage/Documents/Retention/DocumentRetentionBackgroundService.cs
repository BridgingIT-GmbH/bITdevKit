// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using BridgingIT.DevKit.Common;

/// <summary>Runs monitored, bounded provider-native physical cleanup for expired documents.</summary>
/// <param name="scopeFactory">The root scope factory used to resolve each keyed client in a fresh owned scope.</param>
/// <param name="options">The top-level feature and shared retention scheduling options.</param>
/// <param name="descriptors">The immutable named client registrations considered by each sweep.</param>
/// <param name="applicationLifetime">The host lifetime used to delay scheduling until application startup completes.</param>
/// <param name="timeProvider">The clock used for scheduling and expiration cutoffs.</param>
/// <param name="logger">The typed logger used for non-sensitive sweep outcomes.</param>
/// <remarks>
/// Unsupported providers are skipped. Retention-capable providers are resolved from the same container-owned client graph
/// used by application operations, preventing duplicate providers and respecting configured lifetimes. Scheduling is
/// serialized and monitored by <see cref="PeriodicBackgroundService" />.
/// </remarks>
/// <example><code>var result = await service.SweepOnceAsync(cancellationToken);</code></example>
public sealed class DocumentRetentionBackgroundService(
    IServiceScopeFactory scopeFactory,
    DocumentStorageOptions options,
    IEnumerable<DocumentStoreClientDescriptor> descriptors,
    IHostApplicationLifetime applicationLifetime,
    TimeProvider timeProvider,
    ILogger<DocumentRetentionBackgroundService> logger,
    IStoragePermalinkChangeQueue permalinkQueue = null) : PeriodicBackgroundService(
        new()
        {
            StartupDelay = options.Retention.StartupDelay,
            Interval = options.Retention.SweepInterval,
            StopTimeout = options.Retention.StopTimeout
        },
        applicationLifetime,
        timeProvider)
{
    private readonly IReadOnlyList<DocumentStoreClientDescriptor> descriptors = descriptors?.ToArray() ?? [];
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DocumentRetentionDiagnostics> outcomes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the latest completed retention outcome for a registered client.</summary>
    /// <param name="clientId">The stable descriptor client identifier.</param>
    /// <returns>The latest outcome, or null before the first supported sweep completes.</returns>
    /// <example><code>var outcome = service.GetLastOutcome(descriptor.ClientId);</code></example>
    public DocumentRetentionDiagnostics GetLastOutcome(string clientId) =>
        string.IsNullOrWhiteSpace(clientId) ? null : this.outcomes.GetValueOrDefault(clientId);

    /// <summary>Executes one bounded retention sweep across all retention-capable registered named clients.</summary>
    /// <param name="cancellationToken">The token used to cancel provider cleanup and inter-batch delays.</param>
    /// <returns>A result containing one sweep result per supported provider, in descriptor order.</returns>
    /// <example><code>var result = await service.SweepOnceAsync(cancellationToken);</code></example>
    public async Task<Result<IReadOnlyList<DocumentRetentionSweepResult>>> SweepOnceAsync(CancellationToken cancellationToken = default)
    {
        var validation = options.Retention.Validate();
        if (validation.IsFailure)
        {
            return Result<IReadOnlyList<DocumentRetentionSweepResult>>.Failure(validation);
        }

        var results = new List<DocumentRetentionSweepResult>();
        foreach (var descriptor in this.descriptors)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var key = new DocumentStoreServiceKey(descriptor.DocumentType, descriptor.Name);
            var provider = scope.ServiceProvider.GetRequiredKeyedService<IDocumentStoreProvider>(key);
            if (provider is not IDocumentStoreRetentionProvider retention)
            {
                continue;
            }

            var sweepStartedAt = this.TimeProvider.GetUtcNow();
            var result = await retention.SweepExpiredAsync(new()
            {
                DocumentType = descriptor.TypeIdentity,
                VisibilityCutoff = sweepStartedAt,
                BatchSize = options.Retention.BatchSize,
                MaxBatches = options.Retention.MaxBatchesPerStore,
                BatchDelay = options.Retention.BatchDelay
            }, cancellationToken);
            if (result.IsFailure)
            {
                this.outcomes[descriptor.ClientId] = new()
                {
                    CompletedAt = this.TimeProvider.GetUtcNow(),
                    IsSuccess = false,
                    Detail = result.Errors.FirstOrDefault()?.Message ?? "Retention sweep failed."
                };
                logger.LogWarning("[DocumentStorage] retention sweep failed (clientName={ClientName}, provider={Provider})", descriptor.Name, descriptor.ProviderName);
                return Result<IReadOnlyList<DocumentRetentionSweepResult>>.Failure(result);
            }

            results.Add(result.Value);
            var accessor = scope.ServiceProvider.GetRequiredKeyedService<IDocumentStoreClientAccessor>(key);
            if (permalinkQueue is not null && accessor?.PermalinksEnabled == true)
            {
                foreach (var documentKey in result.Value.DeletedKeys)
                {
                    await permalinkQueue.EnqueueAsync(new(StorageResourceChangeKind.Deleted, StorageResourceLocation.ForDocument(descriptor.ClientId, documentKey), occurredAt: sweepStartedAt), cancellationToken);
                }
            }

            this.outcomes[descriptor.ClientId] = new()
            {
                CompletedAt = this.TimeProvider.GetUtcNow(),
                IsSuccess = true,
                DeletedCount = result.Value.DeletedCount,
                BatchCount = result.Value.BatchCount,
                HasMore = result.Value.HasMore,
                Detail = "Retention sweep completed."
            };
            logger.LogInformation("[DocumentStorage] retention sweep completed (clientName={ClientName}, deletedCount={DeletedCount}, batchCount={BatchCount})", descriptor.Name, result.Value.DeletedCount, result.Value.BatchCount);
        }

        return Result<IReadOnlyList<DocumentRetentionSweepResult>>.Success(results);
    }

    /// <inheritdoc />
    protected override bool IsEnabled => options.IsEnabled && options.Retention.Enabled;

    /// <inheritdoc />
    protected override async Task ExecuteIterationAsync(CancellationToken cancellationToken) =>
        await this.SweepOnceAsync(cancellationToken).ConfigureAwait(false);
}
