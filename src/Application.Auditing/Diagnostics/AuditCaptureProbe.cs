// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Application.Auditing;

using BridgingIT.DevKit.Common;

/// <summary>Bounded hypothetical instrumentation facts. It contains no business payload or callback.</summary>
/// <example><code>var probe = new AuditCaptureProbe { Feature = "Requester", Boundary = "Dispatch" };</code></example>
public sealed record AuditCaptureProbe
{
    /// <summary>Source registration feature, or Manual for the core scope API.</summary>
    /// <example><code>var value = probe.Feature;</code></example>
    public string Feature { get; init; } = "Manual";

    /// <summary>Source-owned boundary to inspect.</summary>
    /// <example><code>var value = probe.Boundary;</code></example>
    public string Boundary { get; init; }

    /// <summary>Whether the hypothetical source operation is a read.</summary>
    /// <example><code>var value = probe.IsRead;</code></example>
    public bool IsRead { get; init; }

    /// <summary>Explicit source selection of the hypothetical business read.</summary>
    /// <example><code>var value = probe.ReadSelected;</code></example>
    public bool ReadSelected { get; init; }

    /// <summary>Hypothetical suppression, independent of the review request's own suppression.</summary>
    /// <example><code>var value = probe.Suppressed;</code></example>
    public bool Suppressed { get; init; }

    /// <summary>Whether the hypothetical operation has a recording parent.</summary>
    /// <example><code>var value = probe.HasRecordingParent;</code></example>
    public bool HasRecordingParent { get; init; }

    /// <summary>Requested join or independent-root semantics.</summary>
    /// <example><code>var value = probe.Mode;</code></example>
    public AuditScopeMode Mode { get; init; }
}
