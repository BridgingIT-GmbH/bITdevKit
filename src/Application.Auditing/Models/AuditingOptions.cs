// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Common;

/// <summary>Copied auditing configuration with ReportOnly delivery failures.</summary>
/// <example><code>var value = new AuditingOptions();</code></example>
public sealed record AuditingOptions
{
    /// <summary>Explicit value-name projection policies shared by every capture path.</summary>
    /// <example><code>var policy = snapshot.CapturePolicy;</code></example>
    public AuditCapturePolicyOptions CapturePolicy { get; init; } = new();

    /// <summary>Replaces derived naming while preserving explicit overrides.</summary>
    /// <example><code>var naming = snapshot.NamingConvention;</code></example>
    public AuditNamingConvention NamingConvention { get; init; } = AuditNamingConvention.Default;

    /// <summary>Whether configured capture is enabled.</summary>
    /// <example><code>var value = snapshot.Enabled;</code></example>
    public bool Enabled { get; init; } = true;

    /// <summary>Quiet period after the latest capture or observed delivery failure before health recovers.</summary>
    /// <example><code>builder.Configure(o => o with { HealthFailureWindow = TimeSpan.FromMinutes(2) });</code></example>
    public TimeSpan HealthFailureWindow { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Source application identity.</summary>
    /// <example><code>var value = snapshot.Application;</code></example>
    public string Application { get; init; } = "Application";

    /// <summary>Source node identity.</summary>
    /// <example><code>var value = snapshot.Node;</code></example>
    public string Node { get; init; } = Environment.MachineName;

    /// <summary>Finite per-root capture limits.</summary>
    /// <example><code>var value = snapshot.Limits;</code></example>
    public AuditCaptureLimits Limits { get; init; } = new();

    /// <summary>Combined active/archive memory record limit.</summary>
    /// <example><code>var value = snapshot.MemoryRecordLimit;</code></example>
    public int MemoryRecordLimit { get; init; } = 10_000;

    /// <summary>Combined memory encoded-byte limit.</summary>
    /// <example><code>var value = snapshot.MemoryByteLimit;</code></example>
    public long MemoryByteLimit { get; init; } = 134_217_728;

    /// <summary>Fresh asynchronous sealing/admission budget, independent of client cancellation.</summary>
    /// <example><code>var value = snapshot.FinalizationTimeout;</code></example>
    public TimeSpan FinalizationTimeout { get; init; } = TimeSpan.FromSeconds(10);

}
