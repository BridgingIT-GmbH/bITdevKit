// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using BridgingIT.DevKit.Application.Caching;

/// <summary>Checks the observable filesystem kind before selecting the cache root's native ownership protocol.</summary>
/// <remarks>A shared marker protocol may be exercised conservatively on a local filesystem. This guard establishes protocol selection, not network durability qualification. Mount options and credentials are not emitted.</remarks>
/// <example><code>CacheFileSystemAccess.ValidateBeforeCreate(profile.RootPath, profile.Native.AccessKind);</code></example>
public static class CacheFileSystemAccess
{
    /// <summary>Recognizes ordinary and extended UNC roots without accessing a server.</summary>
    /// <param name="path">The fully qualified Windows path.</param>
    /// <returns>Whether the path names a network namespace.</returns>
    /// <example><code>var isRemote = CacheFileSystemAccess.IsUncPath(@"\\server\share\cache");</code></example>
    public static bool IsUncPath(string path) => FileSystemKindProbe.IsUncPath(path);

    /// <summary>Maps a Linux mount filesystem name to its observable local or supported remote protocol.</summary>
    /// <param name="fileSystem">The filesystem name from the mount table, without options.</param>
    /// <returns>Local, Smb, Nfs, or Unsupported.</returns>
    /// <example><code>var kind = CacheFileSystemAccess.ClassifyLinux("ext4");</code></example>
    public static string ClassifyLinux(string fileSystem) => FileSystemKindProbe.ClassifyLinux(fileSystem).ToString();

    /// <summary>Rejects an unsafe local declaration for a remote filesystem, mismatched remote protocols and unknown native kinds.</summary>
    /// <param name="declared">The configured Local, Smb or Nfs protocol.</param>
    /// <param name="observed">The observed Local, Smb, Nfs or Unsupported kind.</param>
    /// <exception cref="CacheTransferException">The ownership protocol cannot safely serve the observed filesystem.</exception>
    /// <example><code>CacheFileSystemAccess.ValidateKind("Smb", "Local");</code></example>
    public static void ValidateKind(string declared, string observed)
    {
        if (declared is not ("Local" or "Smb" or "Nfs") || observed is not ("Local" or "Smb" or "Nfs") ||
            observed != "Local" && observed != declared)
        {
            throw new CacheTransferException(CacheErrorCode.Unsupported);
        }
    }

    /// <summary>Inspects an existing mount or ancestor and rejects an incompatible ownership declaration before any cache files are created.</summary>
    /// <param name="root">The normalized absolute cache root.</param>
    /// <param name="declared">The configured ownership protocol.</param>
    /// <exception cref="CacheTransferException">The actual filesystem is unsupported or does not match the safe declared protocol.</exception>
    /// <example><code>CacheFileSystemAccess.ValidateBeforeCreate(root, "Local");</code></example>
    public static void ValidateBeforeCreate(string root, string declared) =>
        ValidateKind(declared, FileSystemKindProbe.Probe(root).ToString());
}