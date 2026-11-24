// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Records finite process resource ceilings shared by all cache collections.</summary>
/// <example><code>limits.MaxResidentBytes(ByteSize.Megabytes(256)).MaxActiveSources(16);</code></example>
public sealed class CacheProcessLimitsBuilder : PatchOptionsBuilder<CacheProcessLimits, CacheProcessLimitsBuilder>
{
    /// <summary>Sets the total cache-owned resident byte ceiling.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>limits.MaxResidentBytes(ByteSize.Megabytes(256));</code></example>
    public CacheProcessLimitsBuilder MaxResidentBytes(long value) => this.With(options => options.MaxResidentBytes, value);

    /// <summary>Sets the simultaneous transient byte ceiling.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>limits.MaxTransientBytes(ByteSize.Megabytes(64));</code></example>
    public CacheProcessLimitsBuilder MaxTransientBytes(long value) => this.With(options => options.MaxTransientBytes, value);

    /// <summary>Sets simultaneous source executions, including unfinished tails.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>limits.MaxActiveSources(16);</code></example>
    public CacheProcessLimitsBuilder MaxActiveSources(int value) => this.With(options => options.MaxActiveSources, value);

    /// <summary>Sets queued source admissions.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>limits.MaxQueuedSources(64);</code></example>
    public CacheProcessLimitsBuilder MaxQueuedSources(int value) => this.With(options => options.MaxQueuedSources, value);

    /// <summary>Sets simultaneous persistent-provider I/O operations.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>limits.MaxActiveProviderOperations(32);</code></example>
    public CacheProcessLimitsBuilder MaxActiveProviderOperations(int value) => this.With(options => options.MaxActiveProviderOperations, value);

    /// <summary>Sets queued provider I/O admissions.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>limits.MaxQueuedProviderOperations(64);</code></example>
    public CacheProcessLimitsBuilder MaxQueuedProviderOperations(int value) => this.With(options => options.MaxQueuedProviderOperations, value);

    /// <summary>Sets simultaneous metadata queries.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>limits.MaxMetadataQueries(4);</code></example>
    public CacheProcessLimitsBuilder MaxMetadataQueries(int value) => this.With(options => options.MaxMetadataQueries, value);

    /// <summary>Sets simultaneous occupied payload transfers.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>limits.MaxTransfers(8);</code></example>
    public CacheProcessLimitsBuilder MaxTransfers(int value) => this.With(options => options.MaxTransfers, value);

    /// <summary>Sets simultaneous typed decode or hot promotion work.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>limits.MaxDecodePromotions(4);</code></example>
    public CacheProcessLimitsBuilder MaxDecodePromotions(int value) => this.With(options => options.MaxDecodePromotions, value);

    /// <summary>Sets indexed candidates shared by one refresh sweep.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>limits.MaxRefreshCandidates(128);</code></example>
    public CacheProcessLimitsBuilder MaxRefreshCandidates(int value) => this.With(options => options.MaxRefreshCandidates, value);

    /// <summary>Sets the process background-refresh source sublimit.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>limits.MaxRefreshSources(4);</code></example>
    public CacheProcessLimitsBuilder MaxRefreshSources(int value) => this.With(options => options.MaxRefreshSources, value);
}