// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Records explicit native named-provider settings.</summary>
/// <example><code>provider.MaxResidentBytes(ByteSize.Megabytes(64)).MaxEntries(1000);</code></example>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Advanced)]
public sealed class CacheProviderBuilder : PatchOptionsBuilder<CacheProviderOptions, CacheProviderBuilder>
{
    /// <summary>Selects one trusted registered complete provider kind.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>provider.Kind("InMemory");</code></example>
    public CacheProviderBuilder Kind(string value) => this.With(options => options.Kind, value);

    /// <summary>Selects the registered relational engine profile.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>provider.Engine("SqlServer");</code></example>
    public CacheProviderBuilder Engine(string value) => this.With(options => options.Engine, value);

    /// <summary>Sets this provider's resident byte ceiling.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>provider.MaxResidentBytes(ByteSize.Megabytes(128));</code></example>
    public CacheProviderBuilder MaxResidentBytes(long value) => this.With(options => options.MaxResidentBytes, value);

    /// <summary>Sets this provider's current, staged, and retired storage ceiling.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>provider.MaxStorageBytes(ByteSize.Gigabytes(2));</code></example>
    public CacheProviderBuilder MaxStorageBytes(long value) => this.With(options => options.MaxStorageBytes, value);

    /// <summary>Sets this provider's retained current entry ceiling.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>provider.MaxEntries(10000);</code></example>
    public CacheProviderBuilder MaxEntries(long value) => this.With(options => options.MaxEntries, value);

    /// <summary>Sets this provider's resident metadata and index cache ceiling.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>provider.MetadataCacheBytes(ByteSize.Megabytes(16));</code></example>
    public CacheProviderBuilder MetadataCacheBytes(long value) => this.With(options => options.MetadataCacheBytes, value);

    /// <summary>Sets the bounded native provider attempt timeout.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>provider.AttemptTimeout(TimeSpan.FromSeconds(5));</code></example>
    public CacheProviderBuilder AttemptTimeout(TimeSpan value) => this.With(options => options.AttemptTimeout, value);

    /// <summary>Sets bounded shared-owner lease and renewal intervals.</summary>
    /// <param name="lifetime">The finite ownership lease lifetime.</param>
    /// <param name="renewalInterval">The interval before the lease expires at which renewal is attempted.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>provider.Lease(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10));</code></example>
    public CacheProviderBuilder Lease(TimeSpan lifetime, TimeSpan renewalInterval) => this.With(options => options.LeaseLifetime, lifetime).With(options => options.LeaseRenewalInterval, renewalInterval);
}