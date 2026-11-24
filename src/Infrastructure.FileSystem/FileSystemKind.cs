// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem;

/// <summary>Identifies a filesystem's observable local or supported network kind.</summary>
/// <remarks>This observation does not establish locking, durability, permissions or deployment qualification.</remarks>
/// <example><code>var isLocal = FileSystemKindProbe.Probe(root) == FileSystemKind.Local;</code></example>
public enum FileSystemKind
{
    /// <summary>The host, filesystem or remote protocol could not be recognized as a supported kind.</summary>
    /// <example><code>if (kind == FileSystemKind.Unsupported) { return; }</code></example>
    Unsupported,

    /// <summary>The observed filesystem is local to the host.</summary>
    /// <example><code>var isLocal = kind == FileSystemKind.Local;</code></example>
    Local,

    /// <summary>The filesystem uses SMB. Windows observations require SMB version 3 or later.</summary>
    /// <example><code>var isSmb = kind == FileSystemKind.Smb;</code></example>
    Smb,

    /// <summary>The Linux mount table identifies an NFS filesystem.</summary>
    /// <example><code>var isNfs = kind == FileSystemKind.Nfs;</code></example>
    Nfs
}