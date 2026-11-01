// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using BridgingIT.DevKit.Common;

/// <summary>Contains bounded progress identities without filter values, key material, or a live execution context.</summary>
/// <example><code>var value = new AuditExportResume();</code></example>
public sealed record AuditExportResume
{
    /// <summary>Original chunk lower boundary, retained only inside protected or trusted progress for final availability checks.</summary>
    /// <example><code>var lower = progress.ChunkLowerPosition;</code></example>
    public long ChunkLowerPosition { get; init; }

    /// <summary>The stable source store identity.</summary>
    /// <example><code>var value = snapshot.StoreId;</code></example>
    public Guid StoreId { get; init; }

    /// <summary>The publication epoch that invalidates restored or reset progress.</summary>
    /// <example><code>var value = snapshot.StoreEpoch;</code></example>
    public Guid StoreEpoch { get; init; }

    /// <summary>The last whole source position confirmed by the receiver.</summary>
    /// <example><code>var value = snapshot.AfterPosition;</code></example>
    public long AfterPosition { get; init; }

    /// <summary>The fixed upper boundary for a partial window.</summary>
    /// <example><code>var value = snapshot.UpperPosition;</code></example>
    public long UpperPosition { get; init; }

    /// <summary>The original window lifecycle generation.</summary>
    /// <example><code>var value = snapshot.LifecycleGeneration;</code></example>
    public long LifecycleGeneration { get; init; }

    /// <summary>The original retained-evidence removal boundary.</summary>
    /// <example><code>var value = snapshot.AvailabilityBaseline;</code></example>
    public long AvailabilityBaseline { get; init; }

    /// <summary>The effective absolute selection fingerprint.</summary>
    /// <example><code>var value = snapshot.SelectionFingerprint;</code></example>
    public string SelectionFingerprint { get; init; }

    /// <summary>The host data-scope fingerprint.</summary>
    /// <example><code>var value = snapshot.ScopeFingerprint;</code></example>
    public string ScopeFingerprint { get; init; }

    /// <summary>The current source exposure and formatter revision fingerprint.</summary>
    /// <example><code>var value = snapshot.ExposureFingerprint;</code></example>
    public string ExposureFingerprint { get; init; }

    /// <summary>The bound format identifier.</summary>
    /// <example><code>var value = snapshot.Format;</code></example>
    public string Format { get; init; }

    /// <summary>The pinned formatter profile.</summary>
    /// <example><code>var value = snapshot.Profile;</code></example>
    public string Profile { get; init; }

    /// <summary>The original selection clock used for omitted event-time boundaries.</summary>
    /// <example><code>var value = snapshot.AsOfUtc;</code></example>
    public DateTimeOffset AsOfUtc { get; init; }

    /// <summary>Absolute progress expiry.</summary>
    /// <example><code>var value = snapshot.ExpiresUtc;</code></example>
    public DateTimeOffset ExpiresUtc { get; init; }

    /// <summary>True only for an exhausted, successfully output window; opens a new committed window.</summary>
    /// <example><code>var value = snapshot.IsCheckpoint;</code></example>
    public bool IsCheckpoint { get; init; }
}
