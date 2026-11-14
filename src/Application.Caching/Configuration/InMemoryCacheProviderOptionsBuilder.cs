// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Records named in-memory provider settings below the host configuration overlay.</summary>
/// <remarks>The provider defaults to 128 MiB resident and storage capacity and 100000 entries. Compression and collection hot-entry retention are configured separately.</remarks>
/// <example><code>builder.WithInMemoryProvider("lookups", provider =&gt; provider.Capacity(ByteSize.Megabytes(16)).MaxEntries(10000));</code></example>
public sealed class InMemoryCacheProviderOptionsBuilder : PatchOptionsBuilder<CacheProviderOptions, InMemoryCacheProviderOptionsBuilder>
{
    /// <summary>Sets equal resident and storage ceilings for this in-process provider.</summary>
    /// <param name="bytes">The positive capacity in bytes. An unspecified metadata cache scales down with resident capacity.</param>
    /// <returns>The same provider options builder.</returns>
    /// <example><code>provider.Capacity(ByteSize.Megabytes(16));</code></example>
    public InMemoryCacheProviderOptionsBuilder Capacity(long bytes) => this.MaxResidentBytes(bytes).MaxStorageBytes(bytes);

    /// <summary>Sets the provider's resident memory ceiling.</summary>
    /// <param name="bytes">The positive capacity in bytes. An unspecified metadata cache scales down with this ceiling.</param>
    /// <returns>The same provider options builder.</returns>
    /// <example><code>provider.MaxResidentBytes(ByteSize.Megabytes(8));</code></example>
    public InMemoryCacheProviderOptionsBuilder MaxResidentBytes(long bytes) => this.With(value => value.MaxResidentBytes, bytes);

    /// <summary>Sets the retained, staged, and retired payload storage ceiling.</summary>
    /// <param name="bytes">The positive capacity in bytes.</param>
    /// <returns>The same provider options builder.</returns>
    /// <example><code>provider.MaxStorageBytes(ByteSize.Megabytes(32));</code></example>
    public InMemoryCacheProviderOptionsBuilder MaxStorageBytes(long bytes) => this.With(value => value.MaxStorageBytes, bytes);

    /// <summary>Sets the provider's retained-entry ceiling.</summary>
    /// <param name="count">The positive maximum number of current entries.</param>
    /// <returns>The same provider options builder.</returns>
    /// <example><code>provider.MaxEntries(10000);</code></example>
    public InMemoryCacheProviderOptionsBuilder MaxEntries(long count) => this.With(value => value.MaxEntries, count);

    /// <summary>Sets the resident metadata and index cache explicitly.</summary>
    /// <param name="bytes">The positive metadata capacity within the resident ceiling.</param>
    /// <returns>The same provider options builder.</returns>
    /// <example><code>provider.MetadataCacheBytes(ByteSize.Megabytes(1));</code></example>
    public InMemoryCacheProviderOptionsBuilder MetadataCacheBytes(long bytes) => this.With(value => value.MetadataCacheBytes, bytes);

    /// <summary>Sets the storage fraction reserved for metadata and reclamation.</summary>
    /// <param name="ratio">A finite fraction greater than zero and less than one.</param>
    /// <returns>The same provider options builder.</returns>
    /// <example><code>provider.MetadataReserveRatio(0.05);</code></example>
    public InMemoryCacheProviderOptionsBuilder MetadataReserveRatio(double ratio) => this.With(value => value.MetadataReserveRatio, ratio);

    /// <summary>Sets the bounded native-operation attempt timeout.</summary>
    /// <param name="timeout">The finite positive timeout.</param>
    /// <returns>The same provider options builder.</returns>
    /// <example><code>provider.AttemptTimeout(TimeSpan.FromSeconds(5));</code></example>
    public InMemoryCacheProviderOptionsBuilder AttemptTimeout(TimeSpan timeout) => this.With(value => value.AttemptTimeout, timeout);
}