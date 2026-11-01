// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Common;

/// <summary>Finite capture admission and lifetime limits.</summary>
/// <example><code>var value = new AuditCaptureLimits();</code></example>
public sealed record AuditCaptureLimits
{
    /// <summary>Maximum projected JSON depth, leaving room for document envelopes.</summary>
    /// <example><code>var depth = snapshot.MaxProjectedJsonDepth;</code></example>
    public int MaxProjectedJsonDepth { get; init; } = 32;

    /// <summary>Maximum complete UTF-8 document bytes, including protection.</summary>
    /// <example><code>var value = snapshot.MaxRecordBytes;</code></example>
    public int MaxRecordBytes { get; init; } = 1_048_576;

    /// <summary>Maximum retained action occurrences per root.</summary>
    /// <example><code>var value = snapshot.MaxActions;</code></example>
    public int MaxActions { get; init; } = 500;

    /// <summary>Maximum nested action depth.</summary>
    /// <example><code>var value = snapshot.MaxDepth;</code></example>
    public int MaxDepth { get; init; } = 32;

    /// <summary>Maximum simultaneously open children per root.</summary>
    /// <example><code>var value = snapshot.MaxActiveChildren;</code></example>
    public int MaxActiveChildren { get; init; } = 128;

    /// <summary>Maximum details per root or action.</summary>
    /// <example><code>var value = snapshot.MaxDetails;</code></example>
    public int MaxDetails { get; init; } = 32;

    /// <summary>Maximum encoded bytes per projected value.</summary>
    /// <example><code>var value = snapshot.MaxDetailValueBytes;</code></example>
    public int MaxDetailValueBytes { get; init; } = 4_096;

    /// <summary>Maximum retained source change groups per execution.</summary>
    /// <example><code>var groups = limits.MaxChangeGroups;</code></example>
    public int MaxChangeGroups { get; init; } = 100;

    /// <summary>Maximum retained changed properties across all groups.</summary>
    /// <example><code>var properties = limits.MaxChangeProperties;</code></example>
    public int MaxChangeProperties { get; init; } = 1_000;

    /// <summary>Maximum retained distinct safe error occurrences per root.</summary>
    /// <example><code>var value = snapshot.MaxErrors;</code></example>
    public int MaxErrors { get; init; } = 20;

    /// <summary>Maximum admitted active roots per instance.</summary>
    /// <example><code>var value = snapshot.MaxActiveRoots;</code></example>
    public int MaxActiveRoots { get; init; } = 1_024;

    /// <summary>Maximum capture lifetime without canceling business work.</summary>
    /// <example><code>var value = snapshot.MaxLifetime;</code></example>
    public TimeSpan MaxLifetime { get; init; } = TimeSpan.FromMinutes(30);

}
