// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Defines finite parent ceilings shared by every advanced-cache collection.</summary>
/// <example><code>var options = new CacheProcessLimits();</code></example>
public sealed record CacheProcessLimits
{
    /// <summary>Gets resident bytes charged to cache-owned resources.</summary>
    public long MaxResidentBytes { get; init; } = 256 * CacheDefaults.MiB;

    /// <summary>Gets concurrent transient capacity reserved before allocation.</summary>
    public long MaxTransientBytes { get; init; } = 64 * CacheDefaults.MiB;

    /// <summary>Gets active source executions, including timed-out work until it actually stops.</summary>
    public int MaxActiveSources { get; init; } = 32;

    /// <summary>Gets queued source admissions.</summary>
    public int MaxQueuedSources { get; init; } = 256;

    /// <summary>Gets occupied persistent-provider I/O units.</summary>
    public int MaxActiveProviderOperations { get; init; } = 64;

    /// <summary>Gets queued provider operations.</summary>
    public int MaxQueuedProviderOperations { get; init; } = 256;

    /// <summary>Gets concurrent bounded metadata queries.</summary>
    public int MaxMetadataQueries { get; init; } = 4;

    /// <summary>Gets concurrent occupied payload transfers.</summary>
    public int MaxTransfers { get; init; } = 8;

    /// <summary>Gets concurrent typed decode or hot promotion work.</summary>
    public int MaxDecodePromotions { get; init; } = 4;

    /// <summary>Gets the process-wide indexed candidate allowance shared by all collections in one refresh sweep.</summary>
    public int MaxRefreshCandidates { get; init; } = 128;

    /// <summary>Gets background refresh source sublimit.</summary>
    public int MaxRefreshSources { get; init; } = 4;
}