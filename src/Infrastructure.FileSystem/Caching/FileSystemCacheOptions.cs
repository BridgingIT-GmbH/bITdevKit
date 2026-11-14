// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using BridgingIT.DevKit.Application.Caching;

/// <summary>Captures validated advanced-cache filesystem settings without accessing the root.</summary>
/// <example><code>var profile = new FileSystemCacheOptions(new() { Kind = "FileSystem", RootPath = absoluteRoot });</code></example>
public sealed class FileSystemCacheOptions
{
    /// <summary>Gets the format-one metadata frame and manifest byte ceiling.</summary>
    public const int MaximumMetadataBytes = 1024 * 1024;

    /// <summary>Creates an immutable native profile. Unsupported hosts and path/access combinations are rejected before I/O.</summary>
    public FileSystemCacheOptions(CacheProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.RootPath) || !Path.IsPathFullyQualified(options.RootPath) ||
            (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux()) || options.AccessKind is not ("Local" or "Smb" or "Nfs") ||
            (OperatingSystem.IsWindows() && options.AccessKind == "Nfs") || options.Durability is not ("Durable" or "Buffered") ||
            options.ShardCount != 256 || options.MetadataFormatVersion != 1 || options.MetadataBlockBytes != 65536 ||
            options.CheckpointBytes <= 0 || options.CheckpointBytes >= options.MaximumJournalTailBytes || options.MaximumJournalTailBytes > 32 * CacheDefaults.MiB ||
            options.QuotaCreditBytes is <= 0 or > 8 * CacheDefaults.MiB || options.LockAcquireTimeout <= TimeSpan.Zero ||
            options.MetadataReserveRatio is <= 0 or >= 1 || double.IsNaN(options.MetadataReserveRatio) ||
            options.ChunkSize is <= 0 or > CacheDefaults.MaximumChunkBytes || options.MaxStorageBytes <= 0 || options.MaxEntries <= 0)
        {
            throw new ArgumentException("The cache filesystem root or native profile is invalid or unsupported.", nameof(options));
        }

        this.Native = options with { RootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.RootPath)) };
        if (OperatingSystem.IsWindows() && options.AccessKind == "Local" && CacheFileSystemAccess.IsUncPath(this.Native.RootPath))
        {
            throw new ArgumentException("A network cache root requires its explicit shared ownership protocol.", nameof(options));
        }

        this.AuthorityOptions = new() { RootPath = this.Native.RootPath, AccessKind = Enum.Parse<FileSystemStorageAccessKind>(options.AccessKind),
            Durability = Enum.Parse<FileSystemStorageDurability>(options.Durability), LockAcquireTimeout = options.LockAcquireTimeout };
    }

    /// <summary>Gets the normalized absolute provider-owned root.</summary>
    public string RootPath => this.Native.RootPath;

    /// <summary>Gets whether acknowledgements require native data and namespace persistence barriers.</summary>
    public bool Durable => this.Native.Durability == "Durable";

    /// <summary>Gets whether the persistent shared ownership-marker protocol is required.</summary>
    public bool Shared => this.Native.AccessKind != "Local";

    /// <summary>Gets the cloned validated native filesystem settings used by the cache provider.</summary>
    public CacheProviderOptions Native { get; }

    /// <summary>Gets native authority settings derived from the same cache root and access profile.</summary>
    public FileSystemStorageOptions AuthorityOptions { get; }
}