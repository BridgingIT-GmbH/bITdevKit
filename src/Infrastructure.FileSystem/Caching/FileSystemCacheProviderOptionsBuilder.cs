// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using BridgingIT.DevKit.Application.Caching;

/// <summary>Records native filesystem cache options while preserving configuration precedence and final catalog validation.</summary>
/// <remarks>Paths and profiles are validated after configuration overlays. This builder does not access the filesystem.</remarks>
/// <example><code>builder.WithFileSystemProvider("reports", provider =&gt; provider.Root(absoluteRoot).AccessKind(FileSystemStorageAccessKind.Local));</code></example>
public sealed class FileSystemCacheProviderOptionsBuilder : PatchOptionsBuilder<CacheProviderOptions, FileSystemCacheProviderOptionsBuilder>
{
    /// <summary>Sets the dedicated absolute cache root.</summary>
    /// <param name="path">The absolute directory owned by this cache provider.</param>
    /// <returns>The same native provider options builder.</returns>
    /// <example><code>provider.Root(absoluteRoot);</code></example>
    public FileSystemCacheProviderOptionsBuilder Root(string path) => this.With(value => value.RootPath, path);

    /// <summary>Selects the Local, Smb, or Nfs access profile.</summary>
    /// <param name="kind">The declared filesystem access profile.</param>
    /// <returns>The same native provider options builder.</returns>
    /// <example><code>provider.AccessKind("Local");</code></example>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Advanced)]
    public FileSystemCacheProviderOptionsBuilder AccessKind(string kind) => this.With(value => value.AccessKind, kind);

    /// <summary>Selects the filesystem access profile using its declared native kind.</summary>
    /// <param name="kind">The native filesystem access profile.</param>
    /// <returns>The same native provider options builder.</returns>
    /// <example><code>provider.AccessKind(FileSystemStorageAccessKind.Local);</code></example>
    public FileSystemCacheProviderOptionsBuilder AccessKind(FileSystemStorageAccessKind kind) => this.With(value => value.AccessKind, kind.ToString());

    /// <summary>Selects Durable or Buffered publication acknowledgements.</summary>
    /// <param name="durability">The declared acknowledgement profile.</param>
    /// <returns>The same native provider options builder.</returns>
    /// <example><code>provider.Durability("Durable");</code></example>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Advanced)]
    public FileSystemCacheProviderOptionsBuilder Durability(string durability) => this.With(value => value.Durability, durability);

    /// <summary>Selects publication acknowledgements using the native durability profile.</summary>
    /// <param name="durability">The native acknowledgement profile.</param>
    /// <returns>The same native provider options builder.</returns>
    /// <example><code>provider.Durability(FileSystemStorageDurability.Durable);</code></example>
    public FileSystemCacheProviderOptionsBuilder Durability(FileSystemStorageDurability durability) => this.With(value => value.Durability, durability.ToString());

    /// <summary>Sets the provider's resident capacity ceiling.</summary>
    /// <param name="bytes">The positive resident capacity in bytes.</param>
    /// <returns>The same native provider options builder.</returns>
    /// <example><code>provider.MaxResidentBytes(ByteSize.Megabytes(128));</code></example>
    public FileSystemCacheProviderOptionsBuilder MaxResidentBytes(long bytes) => this.With(value => value.MaxResidentBytes, bytes);

    /// <summary>Sets the retained, staged, and retired storage ceiling.</summary>
    /// <param name="bytes">The positive persistent capacity in bytes.</param>
    /// <returns>The same native provider options builder.</returns>
    /// <example><code>provider.MaxStorageBytes(ByteSize.Gigabytes(10));</code></example>
    public FileSystemCacheProviderOptionsBuilder MaxStorageBytes(long bytes) => this.With(value => value.MaxStorageBytes, bytes);

    /// <summary>Sets the provider's retained-entry ceiling.</summary>
    /// <param name="count">The positive maximum current entry count.</param>
    /// <returns>The same native provider options builder.</returns>
    /// <example><code>provider.MaxEntries(100000);</code></example>
    public FileSystemCacheProviderOptionsBuilder MaxEntries(long count) => this.With(value => value.MaxEntries, count);

    /// <summary>Sets the bounded resident metadata and index cache.</summary>
    /// <param name="bytes">The metadata capacity in bytes within the resident ceiling.</param>
    /// <returns>The same native provider options builder.</returns>
    /// <example><code>provider.MetadataCacheBytes(ByteSize.Megabytes(16));</code></example>
    public FileSystemCacheProviderOptionsBuilder MetadataCacheBytes(long bytes) => this.With(value => value.MetadataCacheBytes, bytes);

    /// <summary>Sets the storage fraction reserved for metadata and reclamation.</summary>
    /// <param name="ratio">A finite fraction greater than zero and less than one.</param>
    /// <returns>The same native provider options builder.</returns>
    /// <example><code>provider.MetadataReserveRatio(0.05);</code></example>
    public FileSystemCacheProviderOptionsBuilder MetadataReserveRatio(double ratio) => this.With(value => value.MetadataReserveRatio, ratio);

    /// <summary>Sets the bounded native-operation attempt timeout.</summary>
    /// <param name="timeout">The finite positive operation timeout.</param>
    /// <returns>The same native provider options builder.</returns>
    /// <example><code>provider.AttemptTimeout(TimeSpan.FromSeconds(5));</code></example>
    public FileSystemCacheProviderOptionsBuilder AttemptTimeout(TimeSpan timeout) => this.With(value => value.AttemptTimeout, timeout);

    /// <summary>Sets the bounded native payload chunk size.</summary>
    /// <param name="bytes">The positive chunk size, at most one MiB.</param>
    /// <returns>The same native provider options builder.</returns>
    /// <example><code>provider.ChunkSize(checked((int)ByteSize.Kilobytes(64)));</code></example>
    public FileSystemCacheProviderOptionsBuilder ChunkSize(int bytes) => this.With(value => value.ChunkSize, bytes);

    /// <summary>Sets the bounded shared source-ownership lease.</summary>
    /// <param name="lifetime">The finite positive lease lifetime.</param>
    /// <returns>The same native provider options builder.</returns>
    /// <example><code>provider.LeaseLifetime(TimeSpan.FromSeconds(30));</code></example>
    public FileSystemCacheProviderOptionsBuilder LeaseLifetime(TimeSpan lifetime) => this.With(value => value.LeaseLifetime, lifetime);

    /// <summary>Sets how often shared source ownership is renewed.</summary>
    /// <param name="interval">The positive interval shorter than the lease lifetime.</param>
    /// <returns>The same native provider options builder.</returns>
    /// <example><code>provider.LeaseRenewalInterval(TimeSpan.FromSeconds(10));</code></example>
    public FileSystemCacheProviderOptionsBuilder LeaseRenewalInterval(TimeSpan interval) => this.With(value => value.LeaseRenewalInterval, interval);

    /// <summary>Sets the interruptible native-authority acquisition timeout.</summary>
    /// <param name="timeout">The finite positive authority timeout.</param>
    /// <returns>The same native provider options builder.</returns>
    /// <example><code>provider.LockAcquireTimeout(TimeSpan.FromSeconds(5));</code></example>
    public FileSystemCacheProviderOptionsBuilder LockAcquireTimeout(TimeSpan timeout) => this.With(value => value.LockAcquireTimeout, timeout);

    /// <summary>Sets the native metadata and index block size.</summary>
    /// <param name="bytes">The supported format-one block size, 65536 bytes.</param>
    /// <returns>The same native provider options builder.</returns>
    /// <example><code>provider.MetadataBlockBytes(checked((int)ByteSize.Kilobytes(64)));</code></example>
    public FileSystemCacheProviderOptionsBuilder MetadataBlockBytes(int bytes) => this.With(value => value.MetadataBlockBytes, bytes);

    /// <summary>Sets the journal checkpoint target.</summary>
    /// <param name="bytes">The positive target below the maximum journal tail.</param>
    /// <returns>The same native provider options builder.</returns>
    /// <example><code>provider.CheckpointBytes(ByteSize.Megabytes(16));</code></example>
    public FileSystemCacheProviderOptionsBuilder CheckpointBytes(long bytes) => this.With(value => value.CheckpointBytes, bytes);

    /// <summary>Sets the hard uncheckpointed journal-tail limit.</summary>
    /// <param name="bytes">The positive limit, at most 32 MiB.</param>
    /// <returns>The same native provider options builder.</returns>
    /// <example><code>provider.MaximumJournalTailBytes(ByteSize.Megabytes(32));</code></example>
    public FileSystemCacheProviderOptionsBuilder MaximumJournalTailBytes(long bytes) => this.With(value => value.MaximumJournalTailBytes, bytes);

    /// <summary>Sets the maximum native quota-credit block.</summary>
    /// <param name="bytes">The positive block size, at most eight MiB.</param>
    /// <returns>The same native provider options builder.</returns>
    /// <example><code>provider.QuotaCreditBytes(ByteSize.Megabytes(8));</code></example>
    public FileSystemCacheProviderOptionsBuilder QuotaCreditBytes(long bytes) => this.With(value => value.QuotaCreditBytes, bytes);
}