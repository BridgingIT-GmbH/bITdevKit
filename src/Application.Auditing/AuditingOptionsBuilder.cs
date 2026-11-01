// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Common;

/// <summary>Builds a validated, immutable configuration before runtime services are resolved.</summary>
/// <example><code>services.AddAuditing(o => o.Application("CustomerPortal"));</code></example>
public sealed class AuditingOptionsBuilder
{
    private AuditingOptions options = new();

    /// <summary>Enables or disables capture without opting in any source feature.</summary>
    /// <example><code>builder.Enabled(false);</code></example>
    public AuditingOptionsBuilder Enabled(bool value = true) { this.options = this.options with { Enabled = value }; return this; }

    /// <summary>Sets the source application name.</summary>
    /// <example><code>builder.Application("CustomerPortal");</code></example>
    public AuditingOptionsBuilder Application(string value) { this.options = this.options with { Application = value }; return this; }

    /// <summary>Sets the source node name.</summary>
    /// <example><code>builder.Node("worker-1");</code></example>
    public AuditingOptionsBuilder Node(string value) { this.options = this.options with { Node = value }; return this; }

    /// <summary>Copies selected capture-limit overrides.</summary>
    /// <example><code>builder.Limits(l => l with { MaxActions = 100 });</code></example>
    public AuditingOptionsBuilder Limits(Func<AuditCaptureLimits, AuditCaptureLimits> configure) { this.options = this.options with { Limits = configure(this.options.Limits) }; return this; }

    /// <summary>Copies a complete options transformation.</summary>
    /// <example><code>builder.Configure(o => o with { MemoryRecordLimit = 100 });</code></example>
    public AuditingOptionsBuilder Configure(Func<AuditingOptions, AuditingOptions> configure) { this.options = configure(this.options); return this; }

    /// <summary>Validates finite limits without starting capture or resolving services.</summary>
    /// <example><code>var options = builder.Build();</code></example>
    public AuditingOptions Build()
    {
        var o = this.options ?? throw new ArgumentException("Auditing options are required.");
        var l = o.Limits ?? throw new ArgumentException("Audit limits are required.");
        if (o.CapturePolicy is null || o.NamingConvention is null || string.IsNullOrWhiteSpace(o.Application) || o.Application.Length > 128 || o.Node?.Length > 128 || o.MemoryRecordLimit < 1 || o.MemoryByteLimit < 1 ||
            o.FinalizationTimeout <= TimeSpan.Zero || o.FinalizationTimeout > TimeSpan.FromMinutes(1) ||
            o.HealthFailureWindow <= TimeSpan.Zero || o.HealthFailureWindow > TimeSpan.FromDays(1) ||
            l.MaxProjectedJsonDepth < 1 || l.MaxProjectedJsonDepth > 32 || l.MaxRecordBytes < 128 || l.MaxRecordBytes > 16_777_216 || l.MaxActions < 1 || l.MaxActions > 10_000 || l.MaxDepth < 1 || l.MaxDepth > 64 ||
            l.MaxActiveChildren < 1 || l.MaxActiveRoots < 1 || l.MaxDetails < 1 || l.MaxDetails > 128 || l.MaxDetailValueBytes < 1 || l.MaxDetailValueBytes > 65_536 ||
            l.MaxErrors < 1 || l.MaxErrors > 100 || l.MaxChangeGroups is < 1 or > 1_000 || l.MaxChangeProperties is < 1 or > 10_000 || l.MaxLifetime <= TimeSpan.Zero || l.MaxLifetime > TimeSpan.FromDays(1))
            throw new ArgumentException("Auditing configuration exceeds supported finite bounds.");
        var policy = o.CapturePolicy;
        if (policy.ExcludeNames is null || policy.RedactNames is null || policy.HashNames is null ||
            policy.ExcludeNames.Count + policy.RedactNames.Count + policy.HashNames.Count > 256 ||
            policy.ExcludeNames.Concat(policy.RedactNames).Concat(policy.HashNames).Any(n => string.IsNullOrWhiteSpace(n) || n.Length > 128 || n.Any(char.IsControl)))
            throw new ArgumentException("Audit capture policy names exceed supported bounds.");
        return o with
        {
            Limits = l with { },
            CapturePolicy = policy with
            {
                ExcludeNames = policy.ExcludeNames.WithComparer(StringComparer.OrdinalIgnoreCase),
                RedactNames = policy.RedactNames.WithComparer(StringComparer.OrdinalIgnoreCase),
                HashNames = policy.HashNames.WithComparer(StringComparer.OrdinalIgnoreCase)
            }
        };
    }
}
