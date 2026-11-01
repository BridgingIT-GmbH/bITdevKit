// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Application.Auditing;

/// <summary>Pure capture admission policy shared by real scopes and hypothetical diagnostics.</summary>
/// <example><code>var reason = AuditCaptureEligibility.Reason(true, false, false, false, false, true, true);</code></example>
public static class AuditCaptureEligibility
{
    /// <summary>Returns Eligible or a safe rejection reason without allocating a scope or invoking metadata callbacks.</summary>
    /// <example><code>var eligible = AuditCaptureEligibility.Reason(enabled, stopping, enriching, suppressed, joinsRoot, rootRecording, capacity) == "Eligible";</code></example>
    public static string Reason(bool enabled, bool stopping, bool enriching, bool suppressed, bool joinsRoot, bool rootRecording, bool capacity)
        => !enabled ? "Disabled" : stopping ? "Stopping" : enriching ? "EnrichmentSuppressed" : suppressed ? "Suppressed" : joinsRoot ? rootRecording ? "Eligible" : "ParentClosed" : capacity ? "Eligible" : "AuditRootLimit";
}
