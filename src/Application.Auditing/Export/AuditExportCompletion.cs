// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Common;

/// <summary>Contains candidate extraction facts until the final writer and cleanup outcome confirms them.</summary>
/// <example><code>var value = new AuditExportCompletion();</code></example>
public sealed record AuditExportCompletion
{
    /// <summary>Fixed attempt identity and selection metadata.</summary>
    /// <example><code>var value = snapshot.Metadata;</code></example>
    public AuditExportMetadata Metadata { get; init; }

    /// <summary>Streaming, Completed, Partial, Failed, or Canceled disposition.</summary>
    /// <example><code>var value = snapshot.Status;</code></example>
    public string Status { get; init; } = "Streaming";

    /// <summary>Safe limit or failure reason.</summary>
    /// <example><code>var value = snapshot.ReasonCode;</code></example>
    public string ReasonCode { get; init; }

    /// <summary>Termination time after the relevant stage completed.</summary>
    /// <example><code>var value = snapshot.FinishedUtc;</code></example>
    public DateTimeOffset? FinishedUtc { get; init; }

    /// <summary>Last admitted source position for unrestricted access; omitted for restricted scopes and never client confirmation.</summary>
    /// <example><code>var value = snapshot.LastPosition;</code></example>
    public long? LastPosition { get; init; }

    /// <summary>Whether the fixed upper window was completely scanned.</summary>
    /// <example><code>var value = snapshot.WindowExhausted;</code></example>
    public bool WindowExhausted { get; init; }

    /// <summary>Whole source graphs represented.</summary>
    /// <example><code>var value = snapshot.SourceRecordCount;</code></example>
    public long SourceRecordCount { get; init; }

    /// <summary>Profile output items, independent of control frames.</summary>
    /// <example><code>var value = snapshot.OutputItemCount;</code></example>
    public long OutputItemCount { get; init; }

    /// <summary>Exact profile-defined framed data bytes before HTTP content encoding.</summary>
    /// <example><code>var value = snapshot.DataByteCount;</code></example>
    public long DataByteCount { get; init; }

    /// <summary>Exact framed bytes including all control output.</summary>
    /// <example><code>var value = snapshot.ResponseByteCount;</code></example>
    public long? ResponseByteCount { get; init; }

    /// <summary>Expected contiguous transport-frame prefix, including control frames.</summary>
    /// <example><code>var value = snapshot.FrameCount;</code></example>
    public long FrameCount { get; init; }

    /// <summary>SHA-256 of exact profile data bytes, not a source HMAC.</summary>
    /// <example><code>var value = snapshot.Sha256;</code></example>
    public string Sha256 { get; init; }

    /// <summary>Bounded original-verification totals.</summary>
    /// <example><code>var value = snapshot.VerificationCounts;</code></example>
    public ImmutableDictionary<string, long> VerificationCounts { get; init; } = ImmutableDictionary<string, long>.Empty;

    /// <summary>Bounded capture-completeness totals.</summary>
    /// <example><code>var value = snapshot.CompletenessCounts;</code></example>
    public ImmutableDictionary<string, long> CompletenessCounts { get; init; } = ImmutableDictionary<string, long>.Empty;

    /// <summary>Bounded exposure and formatter transformation totals.</summary>
    /// <example><code>var value = snapshot.Transformations;</code></example>
    public ImmutableDictionary<string, long> Transformations { get; init; } = ImmutableDictionary<string, long>.Empty;

    /// <summary>Candidate trusted progress; presentation protects it only after confirmed output.</summary>
    /// <example><code>var value = snapshot.Progress;</code></example>
    [JsonIgnore]
    public AuditExportResume Progress { get; init; }

    /// <summary>Protected remaining-window progress when partial and confirmed.</summary>
    /// <example><code>var value = snapshot.ContinuationToken;</code></example>
    public string ContinuationToken { get; init; }

    /// <summary>Protected next-window progress only when exhausted and confirmed.</summary>
    /// <example><code>var value = snapshot.CheckpointToken;</code></example>
    public string CheckpointToken { get; init; }

    /// <summary>True only after expected flushed output, successful cleanup and final selection checks.</summary>
    /// <example><code>var value = snapshot.OutputConfirmed;</code></example>
    public bool OutputConfirmed { get; init; }

    /// <summary>Server emission never acknowledges recipient processing.</summary>
    /// <example><code>var value = snapshot.ClientConfirmed;</code></example>
    public bool ClientConfirmed { get; init; } = false;
}
