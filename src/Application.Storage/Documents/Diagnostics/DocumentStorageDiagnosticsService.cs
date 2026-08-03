// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>Captures non-sensitive Document Storage diagnostics through registered dashboard-safe accessors.</summary>
/// <param name="factory">The scoped factory exposing named client descriptors and accessors.</param>
/// <param name="timeProvider">The clock used to timestamp completed snapshots.</param>
/// <remarks>
/// Health probes execute through the configured client graph and therefore honor keyed lifetimes and behaviors. Snapshot
/// output excludes payloads, continuation tokens, encryption material, and provider connection details.
/// </remarks>
/// <example><code>var result = await service.CaptureAsync(cancellationToken);</code></example>
public sealed class DocumentStorageDiagnosticsService(
    IDocumentStoreClientFactory factory,
    DocumentRetentionBackgroundService retentionService,
    TimeProvider timeProvider)
    : IDocumentStorageDiagnosticsService
{
    /// <inheritdoc />
    public async Task<Result<DocumentStorageDiagnosticsSnapshot>> CaptureAsync(CancellationToken cancellationToken = default)
    {
        var clients = new List<DocumentStorageClientDiagnostics>();
        foreach (var descriptor in factory.GetDescriptors())
        {
            var accessor = factory.Create(descriptor.ClientId);
            var health = accessor is null
                ? Result<bool>.Failure(new DocumentStoreProviderError("The keyed client accessor is unavailable."))
                : await accessor.ExistsAsync(new("__bdk/healthcheck", "probe"), cancellationToken);
            clients.Add(new()
            {
                Name = descriptor.Name,
                ProviderName = descriptor.ProviderName,
                DocumentType = descriptor.TypeIdentity.Value,
                IsDefault = descriptor.IsDefault,
                Lifetime = descriptor.Lifetime,
                Capabilities = descriptor.Capabilities,
                MaxStoredDocumentSize = descriptor.Capabilities.MaxStoredDocumentSize,
                TransformIdentifiers = descriptor.TransformIdentifiers,
                LastRetentionOutcome = retentionService.GetLastOutcome(descriptor.ClientId),
                IsHealthy = health.IsSuccess,
                HealthDetail = health.IsSuccess ? "Healthy" : health.Messages?.LastOrDefault() ?? health.Errors?.FirstOrDefault()?.Message ?? "Probe failed"
            });
        }

        return Result<DocumentStorageDiagnosticsSnapshot>.Success(new()
        {
            CapturedAt = timeProvider.GetUtcNow(),
            Clients = clients
        });
    }
}
