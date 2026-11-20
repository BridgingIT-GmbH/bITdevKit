// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>Contains non-sensitive provider-instance dictionary/cache counters and optional retained-name cardinality.</summary>
/// <example><code>var cached = snapshot.PositiveCacheEntries;</code></example>
public sealed record DocumentDimensionDiagnostics
{
    /// <summary>Gets the bounded number of positive cache entries.</summary>
    public int PositiveCacheEntries { get; init; }

    /// <summary>Gets mutations currently holding new-name registration admission.</summary>
    public int RegistrationOperationsInFlight { get; init; }

    /// <summary>Gets attempted registration-race resolution rounds since provider creation.</summary>
    public long RegistrationResolutionAttempts { get; init; }

    /// <summary>Gets unique-key registration races encountered by this provider instance.</summary>
    public long RegistrationConflicts { get; init; }

    /// <summary>Gets names registered by acknowledged owner commits since provider creation.</summary>
    public long AcknowledgedNameRegistrations { get; init; }

    /// <summary>Gets acknowledged registrations per elapsed second since provider creation.</summary>
    public double RegistrationsPerSecond { get; init; }

    /// <summary>Gets retained dictionary cardinality only when explicitly requested; ordinary probes leave this null.</summary>
    public long? DictionaryEntries { get; init; }
}

/// <summary>Exposes cheap counters and opt-in dictionary cardinality for document providers.</summary>
/// <example><code>var stats = await inspector.GetDimensionDiagnosticsAsync(DocumentTypeIdentity.For&lt;Invoice&gt;(), includeDictionaryCount: true);</code></example>
public interface IDocumentDimensionDiagnosticsProvider
{
    /// <summary>Captures counters; dictionary counting requires explicit consent and performs backend I/O.</summary>
    Task<Result<DocumentDimensionDiagnostics>> GetDimensionDiagnosticsAsync(DocumentTypeIdentity type, bool includeDictionaryCount = false, CancellationToken cancellationToken = default);
}

/// <summary>Exposes scoped Dimension diagnostics through a typed document client accessor.</summary>
/// <example><code>var counters = await inspector.GetDimensionDiagnosticsAsync();</code></example>
public interface IDocumentDimensionDiagnosticsAccessor
{
    /// <summary>Captures provider counters and optional cardinality for this accessor's document type.</summary>
    Task<Result<DocumentDimensionDiagnostics>> GetDimensionDiagnosticsAsync(bool includeDictionaryCount = false, CancellationToken cancellationToken = default);
}
