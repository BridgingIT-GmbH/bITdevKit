// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using BridgingIT.DevKit.Common;

/// <summary>Explains static eligibility separately from observed capture and append acknowledgments, without executing source handlers.</summary>
/// <example><code>var explanation = await diagnostics.ExplainAsync(new() { Feature = "Manual" });</code></example>
public sealed class AuditDiagnosticsService(AuditRuntime runtime, IAuditingStorageProvider provider, IEnumerable<IAuditBoundaryRegistrationSource> sources, IAuditQueryService queries)
{
    /// <summary>Returns bounded process-local facts. Provider capabilities do not assert schema readiness or durable delivery.</summary>
    /// <example><code>var snapshot = await diagnostics.GetSnapshotAsync(cancellationToken);</code></example>
    public async Task<AuditDiagnosticsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var suppressed = runtime.Suppress();
        var access = await queries.PrepareAsync(null, cancellationToken).ConfigureAwait(false);
        if (access.IsFailure || !access.Value.IsUnrestrictedScope) return new() { InstanceId = runtime.InstanceId, AsOfUtc = runtime.Time.GetUtcNow(), OperationalVisibility = access.IsFailure ? "Unavailable" : "Restricted" };
        var registrations = this.Declarations();
        return new AuditDiagnosticsSnapshot
        {
            OperationalVisibility = "Unrestricted", InstanceId = runtime.InstanceId, AsOfUtc = runtime.Time.GetUtcNow(), Enabled = runtime.Options.Enabled, Provider = provider.Capabilities,
            Delivery = provider.DeliveryStatus, MemoryEvictedRecords = provider is InMemoryAuditStorageProvider memory ? memory.EvictedRecords : null,
            Registrations = registrations.Take(32).Select(item => new AuditBoundaryRegistration(Text(item.Feature, 64), Text(item.Boundary, 64), item.Selection, Text(item.TypeAlias, 128))).ToImmutableArray(),
            AdditionalDeclarationsOmitted = registrations.Length > 32, Observations = runtime.SnapshotObservations(), Failures = runtime.SnapshotFailures().Take(32).ToImmutableDictionary()
        };
    }

    /// <summary>Predicts admission from explicit hypothetical facts and static declarations; dynamic callbacks remain Unknown.</summary>
    /// <example><code>var prediction = await diagnostics.ExplainAsync(new() { Feature = "Requester", Boundary = "Dispatch" });</code></example>
    public async Task<Result<AuditCapturePrediction>> ExplainAsync(AuditCaptureProbe probe, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (probe is null || probe.Feature?.Length is not (> 0 and <= 128) || probe.Boundary?.Length > 128 || !Enum.IsDefined(probe.Mode)) return Result<AuditCapturePrediction>.Failure(new AuditError("AuditProbeInvalid"));
        using var suppressed = runtime.Suppress();
        var access = await queries.PrepareAsync(null, cancellationToken).ConfigureAwait(false);
        if (access.IsFailure || !access.Value.IsUnrestrictedScope) return Result<AuditCapturePrediction>.Success(new("Unknown", "RestrictedOperationalFacts", runtime.InstanceId, runtime.Time.GetUtcNow()));
        var reason = AuditCaptureEligibility.Reason(runtime.Options.Enabled, runtime.IsStopping, false, probe.Suppressed, probe.HasRecordingParent && probe.Mode == AuditScopeMode.JoinOrStart, true, runtime.CanStart);
        var declarations = this.Declarations().Where(item => item.Feature == probe.Feature && (probe.Boundary is null || item.Boundary == probe.Boundary)).ToArray();
        var selected = probe.Feature == "Manual" ? "selected" : declarations.Length == 0 ? "unknown" : declarations.Any(item => item.Selection == "unknown") ? "unknown" : declarations.Any(item => item.Selection == "selected") ? "selected" : "omitted";
        if (reason == "Eligible") reason = selected != "selected" ? selected == "unknown" ? "DynamicSourceSelection" : "SourceNotSelected" : probe.IsRead && !probe.ReadSelected ? "ReadNotSelected" : "Eligible";
        return Result<AuditCapturePrediction>.Success(new(reason == "Eligible" ? "Eligible" : reason == "DynamicSourceSelection" ? "Unknown" : "Ineligible", reason, runtime.InstanceId, runtime.Time.GetUtcNow()));
    }

    private AuditBoundaryRegistration[] Declarations()
    {
        var result = new List<AuditBoundaryRegistration>();
        foreach (var source in sources.Take(128))
        {
            try { result.AddRange(source.Registrations.Take(Math.Max(0, 1025 - result.Count))); }
            catch (Exception) { result.Add(new("Custom", "Declaration", "unknown")); }

            if (result.Count >= 1025) break;
        }

        return result.Where(item => item is not null).DistinctBy(item => item.Identity).OrderBy(item => item.Identity, StringComparer.Ordinal).ToArray();
    }

    private static string Text(string text, int limit) => text is null || text.Length <= limit ? text : text[..(char.IsHighSurrogate(text[limit - 1]) ? limit - 1 : limit)];
}

/// <summary>Predicted eligibility only. It never asserts a business invocation, persisted record or successful output.</summary>
/// <param name="Decision">Eligible, Ineligible or Unknown.</param>
/// <param name="ReasonCode">Safe pure-policy result.</param>
/// <param name="InstanceId">Process-local context.</param>
/// <param name="AsOfUtc">Time of the prediction.</param>
/// <example><code>var predicted = explanation.Value.Decision;</code></example>
public sealed record AuditCapturePrediction(string Decision, string ReasonCode, Guid InstanceId, DateTimeOffset AsOfUtc)
{
    /// <summary>Diagnostics does not execute the hypothetical operation.</summary>
    /// <example><code>var predicted = prediction.PredictedOnly;</code></example>
    public bool PredictedOnly => true;

    /// <summary>No persistence is confirmed by an eligibility probe.</summary>
    /// <example><code>var persisted = prediction.PersistenceConfirmed;</code></example>
    public bool PersistenceConfirmed => false;
}
