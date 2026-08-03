// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Captures non-sensitive registration, capability, limit, and health diagnostics for Document Storage.</summary>
/// <remarks>
/// Diagnostics never expose document payloads, raw continuation tokens, encryption keys, transform secrets, or provider
/// connection details. The dashboard and MCP handler consume the same immutable snapshot contract.
/// </remarks>
/// <example><code>var snapshot = await diagnostics.CaptureAsync(cancellationToken);</code></example>
public interface IDocumentStorageDiagnosticsService
{
    /// <summary>Captures one point-in-time snapshot of every registered named document client.</summary>
    /// <param name="cancellationToken">The token used to cancel client health probes.</param>
    /// <returns>A result containing non-sensitive registration and health diagnostics.</returns>
    /// <example><code>var snapshot = await diagnostics.CaptureAsync(cancellationToken);</code></example>
    Task<Result<DocumentStorageDiagnosticsSnapshot>> CaptureAsync(CancellationToken cancellationToken = default);
}

/// <summary>Contains one immutable point-in-time Document Storage diagnostics snapshot.</summary>
/// <example><code>var unhealthy = snapshot.Clients.Count(x => !x.IsHealthy);</code></example>
public sealed record DocumentStorageDiagnosticsSnapshot
{
    /// <summary>Gets the UTC timestamp at which snapshot capture completed.</summary>
    /// <example><code>var capturedAt = snapshot.CapturedAt;</code></example>
    public DateTimeOffset CapturedAt { get; init; }
    /// <summary>Gets diagnostics for registered named clients in stable display order.</summary>
    /// <example><code>foreach (var client in snapshot.Clients) { Console.WriteLine(client.Name); }</code></example>
    public IReadOnlyList<DocumentStorageClientDiagnostics> Clients { get; init; } = [];
}

/// <summary>Contains non-sensitive registration, provider, capability, limit, and health diagnostics for one named client.</summary>
/// <example><code>Console.WriteLine(client.Name);</code></example>
public sealed record DocumentStorageClientDiagnostics
{
    /// <summary>Gets the normalized case-insensitive client name used by keyed dependency injection.</summary>
    /// <example><code>var name = client.Name;</code></example>
    public string Name { get; init; }
    /// <summary>Gets the non-sensitive provider display name.</summary>
    /// <example><code>var provider = client.ProviderName;</code></example>
    public string ProviderName { get; init; }
    /// <summary>Gets the stable persisted document type identity.</summary>
    /// <example><code>var type = client.DocumentType;</code></example>
    public string DocumentType { get; init; }
    /// <summary>Gets whether direct unkeyed injection resolves this named client.</summary>
    /// <example><code>if (client.IsDefault) { /* default registration */ }</code></example>
    public bool IsDefault { get; init; }
    /// <summary>Gets the container-owned provider and client lifetime.</summary>
    /// <example><code>var lifetime = client.Lifetime;</code></example>
    public ServiceLifetime Lifetime { get; init; }
    /// <summary>Gets immutable provider capabilities used by client validation and query planning.</summary>
    /// <example><code>var supportsEtags = client.Capabilities.SupportsConditionalWrite;</code></example>
    public DocumentStoreProviderCapabilities Capabilities { get; init; }
    /// <summary>Gets the provider transformed-payload size limit in bytes when known.</summary>
    /// <example><code>var limit = client.MaxStoredDocumentSize;</code></example>
    public long? MaxStoredDocumentSize { get; init; }
    /// <summary>Gets the configured non-sensitive payload transform identifiers in write order.</summary>
    /// <example><code>var encrypted = client.TransformIdentifiers.Contains("aes-cbc-pkcs7");</code></example>
    public IReadOnlyList<string> TransformIdentifiers { get; init; } = [];
    /// <summary>Gets the latest supported retention sweep outcome, or null before a sweep has completed.</summary>
    /// <example><code>var deleted = client.LastRetentionOutcome?.DeletedCount ?? 0;</code></example>
    public DocumentRetentionDiagnostics LastRetentionOutcome { get; init; }
    /// <summary>Gets whether the latest exact-key health probe completed successfully.</summary>
    /// <example><code>if (!client.IsHealthy) { Console.WriteLine(client.HealthDetail); }</code></example>
    public bool IsHealthy { get; init; }
    /// <summary>Gets a safe health summary that excludes payloads, credentials, and backend connection details.</summary>
    /// <example><code>var detail = client.HealthDetail;</code></example>
    public string HealthDetail { get; init; }
}

/// <summary>Contains the latest non-sensitive retention outcome for one named document client.</summary>
/// <example><code>Console.WriteLine(outcome.DeletedCount);</code></example>
public sealed record DocumentRetentionDiagnostics
{
    /// <summary>Gets when the sweep completed.</summary>
    public DateTimeOffset CompletedAt { get; init; }
    /// <summary>Gets whether the sweep completed successfully.</summary>
    public bool IsSuccess { get; init; }
    /// <summary>Gets the number of deleted records.</summary>
    public long DeletedCount { get; init; }
    /// <summary>Gets the number of processed batches.</summary>
    public int BatchCount { get; init; }
    /// <summary>Gets whether bounded work remained.</summary>
    public bool HasMore { get; init; }
    /// <summary>Gets a safe outcome summary.</summary>
    public string Detail { get; init; }
}
