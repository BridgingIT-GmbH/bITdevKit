// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using BridgingIT.DevKit.Common;

/// <summary>Describes a fixed retained persisted window without echoing filter values.</summary>
/// <example><code>var value = new AuditExportMetadata();</code></example>
public sealed record AuditExportMetadata
{
    /// <summary>Whether trusted unrestricted access permits global technical publication positions to be exposed.</summary>
    /// <example><code>var visible = metadata.PositionsExposed;</code></example>
    public bool PositionsExposed { get; init; }

    /// <summary>Whether any selected membership depends on archive state, including mixed predicates.</summary>
    /// <example><code>var stable = metadata.RequiresStableLifecycle;</code></example>
    public bool RequiresStableLifecycle { get; init; } = true;

    /// <summary>The independently versioned export representation.</summary>
    /// <example><code>var value = snapshot.ExportSchemaVersion;</code></example>
    public int ExportSchemaVersion { get; init; } = 1;

    /// <summary>This bounded attempt identity, not a source-item deduplication key.</summary>
    /// <example><code>var value = snapshot.ExportId;</code></example>
    public Guid ExportId { get; init; }

    /// <summary>Stable source store identity.</summary>
    /// <example><code>var value = snapshot.SourceStoreId;</code></example>
    public Guid SourceStoreId { get; init; }

    /// <summary>Committed-position epoch.</summary>
    /// <example><code>var value = snapshot.SourceStoreEpoch;</code></example>
    public Guid SourceStoreEpoch { get; init; }

    /// <summary>Selected format identifier.</summary>
    /// <example><code>var value = snapshot.Format;</code></example>
    public string Format { get; init; }

    /// <summary>Pinned formatter projection version.</summary>
    /// <example><code>var value = snapshot.Profile;</code></example>
    public string Profile { get; init; }

    /// <summary>Safe effective selection fingerprint.</summary>
    /// <example><code>var value = snapshot.SelectionFingerprint;</code></example>
    public string SelectionFingerprint { get; init; }

    /// <summary>Trusted current host scope fingerprint.</summary>
    /// <example><code>var value = snapshot.ScopeFingerprint;</code></example>
    public string ScopeFingerprint { get; init; }

    /// <summary>Bound exposure and formatter revision.</summary>
    /// <example><code>var value = snapshot.ExposureFingerprint;</code></example>
    public string ExposureFingerprint { get; init; }

    /// <summary>Absolute event-range lower bound, absent for an exact-ID selection.</summary>
    /// <example><code>var value = snapshot.FromUtc;</code></example>
    public DateTimeOffset? FromUtc { get; init; }

    /// <summary>Absolute event-range upper bound, absent for an exact-ID selection.</summary>
    /// <example><code>var value = snapshot.ToUtc;</code></example>
    public DateTimeOffset? ToUtc { get; init; }

    /// <summary>Clock at which the event selection was fixed.</summary>
    /// <example><code>var value = snapshot.AsOfUtc;</code></example>
    public DateTimeOffset AsOfUtc { get; init; }

    /// <summary>Attempt start time.</summary>
    /// <example><code>var value = snapshot.StartedUtc;</code></example>
    public DateTimeOffset StartedUtc { get; init; }

    /// <summary>Attempt metadata expiry after termination is supplied by the presentation owner.</summary>
    /// <example><code>var value = snapshot.ExpiresUtc;</code></example>
    public DateTimeOffset ExpiresUtc { get; init; }

    /// <summary>Selected active, archived, or all dataset.</summary>
    /// <example><code>var value = snapshot.Lifecycle;</code></example>
    public string Lifecycle { get; init; }

    /// <summary>Client-confirmed lower committed boundary.</summary>
    /// <example><code>var value = snapshot.WindowLowerPosition;</code></example>
    public long? WindowLowerPosition { get; init; }

    /// <summary>Fixed committed upper boundary for unrestricted access; restricted progress retains it privately.</summary>
    /// <example><code>var value = snapshot.WindowUpperPosition;</code></example>
    public long? WindowUpperPosition { get; init; }

    /// <summary>Required original verification or explicitly unprotected history policy.</summary>
    /// <example><code>var value = snapshot.SourceVerificationPolicy;</code></example>
    public string SourceVerificationPolicy { get; init; }

    /// <summary>Bounded formatter omissions and original-envelope boundary disclosures.</summary>
    /// <example><code>var value = snapshot.Omissions;</code></example>
    public ImmutableArray<string> Omissions { get; init; } = [];
}
