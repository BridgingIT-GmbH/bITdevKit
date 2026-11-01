// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using BridgingIT.DevKit.Common;

/// <summary>Bounded read-only instance facts with no flush, write, key lookup or business invocation.</summary>
/// <example><code>var snapshot = await diagnostics.GetSnapshotAsync();</code></example>
public sealed record AuditDiagnosticsSnapshot
{
    /// <summary>Unrestricted, Restricted or Unavailable visibility of process-wide operational facts.</summary>
    /// <example><code>var visibility = snapshot.OperationalVisibility;</code></example>
    public string OperationalVisibility { get; init; } = "Unavailable";

    /// <summary>Already-observed local receipt counts, or null when the custom provider does not report them. Reading does not flush.</summary>
    /// <example><code>var pending = snapshot.Delivery?.Pending;</code></example>
    public AuditFlushResult Delivery { get; init; }

    /// <summary>Whole graphs evicted by memory capacity, or null for another provider.</summary>
    /// <example><code>var lost = snapshot.MemoryEvictedRecords;</code></example>
    public long? MemoryEvictedRecords { get; init; }

    /// <summary>Process-local observation identity.</summary>
    /// <example><code>var value = snapshot.InstanceId;</code></example>
    public Guid InstanceId { get; init; }

    /// <summary>Snapshot UTC time.</summary>
    /// <example><code>var value = snapshot.AsOfUtc;</code></example>
    public DateTimeOffset AsOfUtc { get; init; }

    /// <summary>These observations describe this process, not a deployment.</summary>
    /// <example><code>var value = snapshot.InstanceScoped;</code></example>
    public bool InstanceScoped { get; init; } = true;

    /// <summary>Whether configured capture is enabled.</summary>
    /// <example><code>var value = snapshot.Enabled;</code></example>
    public bool? Enabled { get; init; }

    /// <summary>Declared provider guarantees, not a readiness test.</summary>
    /// <example><code>var value = snapshot.Provider;</code></example>
    public AuditProviderCapabilities Provider { get; init; }

    /// <summary>Bounded static source declarations, distinct from observed executions.</summary>
    /// <example><code>var value = snapshot.Registrations;</code></example>
    public ImmutableArray<AuditBoundaryRegistration> Registrations { get; init; } = [];

    /// <summary>Whether the displayed inventory was truncated.</summary>
    /// <example><code>var value = snapshot.AdditionalDeclarationsOmitted;</code></example>
    public bool AdditionalDeclarationsOmitted { get; init; }

    /// <summary>Bounded aggregate admission and append-acknowledgment facts.</summary>
    /// <example><code>var value = snapshot.Observations;</code></example>
    public IReadOnlyDictionary<string, long> Observations { get; init; }

    /// <summary>Bounded safe capture failure counters.</summary>
    /// <example><code>var value = snapshot.Failures;</code></example>
    public IReadOnlyDictionary<string, long> Failures { get; init; }
}
