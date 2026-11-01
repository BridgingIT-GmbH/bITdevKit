// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Presentation.Web.Auditing;

using System.Text.Json;
using BridgingIT.DevKit.Application.Auditing;

/// <summary>Bounded instance-local companion metadata. It retains no graphs, filter values, claims, streams or service scopes.</summary>
/// <example><code>var attempts = new AuditExportAttemptStore(options, TimeProvider.System);</code></example>
public sealed class AuditExportAttemptStore(AuditExportOptions options, TimeProvider time)
{
    private readonly object sync = new();
    private readonly Dictionary<Guid, Entry> entries = [];

    /// <summary>Admits initial safe metadata, rejecting capacity exhaustion without evicting active attempts.</summary>
    /// <example><code>var accepted = attempts.TryStart(session.Metadata, principalBinding);</code></example>
    public bool TryStart(AuditExportMetadata metadata, string principalBinding)
    {
        lock (this.sync)
        {
            foreach (var expired in this.entries.Where(pair => pair.Value.Expires <= time.GetUtcNow()).Select(pair => pair.Key).ToArray()) this.entries.Remove(expired);
            var initial = new AuditExportCompletion { Metadata = metadata };
            if (this.entries.Count >= options.DescriptorLimit || this.entries.ContainsKey(metadata.ExportId) || !this.Fits(initial)) return false;
            this.entries.Add(metadata.ExportId, new(principalBinding, initial, time.GetUtcNow() + options.MaxDuration + options.CleanupTimeout + options.ObserverTimeout + options.MetadataLifetime));
            return true;
        }
    }

    /// <summary>Publishes bounded terminal facts once; failure leaves progress unavailable.</summary>
    /// <example><code>attempts.Finish(completion.Metadata.ExportId, completion);</code></example>
    public bool Finish(Guid id, AuditExportCompletion completion)
    {
        lock (this.sync)
        {
            if (!this.entries.TryGetValue(id, out var entry) || entry.Completion.Status != "Streaming" || completion.Metadata?.ExportId != id) return false;
            if (!this.Fits(completion)) completion = new() { Metadata = entry.Completion.Metadata, Status = "Failed", ReasonCode = "AuditExportMetadataTooLarge", FinishedUtc = time.GetUtcNow() };
            var expires = time.GetUtcNow() + options.MetadataLifetime;
            completion = completion with { Metadata = completion.Metadata with { ExpiresUtc = expires } };
            this.entries[id] = entry with { Completion = completion, Expires = expires };
            return true;
        }
    }

    /// <summary>Retrieves only metadata belonging to the current principal and current host scope.</summary>
    /// <example><code>var snapshot = attempts.Find(id, principalBinding, scopeFingerprint);</code></example>
    public AuditExportCompletion Find(Guid id, string principalBinding, string scopeFingerprint)
    {
        lock (this.sync)
        {
            if (!this.entries.TryGetValue(id, out var entry) || entry.Expires <= time.GetUtcNow() || entry.Principal != principalBinding || entry.Completion.Metadata.ScopeFingerprint != scopeFingerprint) return null;
            if (entry.Completion.Status == "Streaming" && time.GetUtcNow() > entry.Completion.Metadata.StartedUtc + options.MaxDuration + options.CleanupTimeout + options.ObserverTimeout)
            {
                this.Finish(id, new() { Metadata = entry.Completion.Metadata, Status = "Failed", ReasonCode = "AuditExportMetadataUnavailable", FinishedUtc = time.GetUtcNow() });
                return this.entries[id].Completion;
            }

            return entry.Completion;
        }
    }

    private bool Fits(AuditExportCompletion completion) => JsonSerializer.SerializeToUtf8Bytes(completion, AuditExportJson.CreateOptions()).Length + 1024 <= options.DescriptorByteLimit;
    private sealed record Entry(string Principal, AuditExportCompletion Completion, DateTimeOffset Expires);
}
