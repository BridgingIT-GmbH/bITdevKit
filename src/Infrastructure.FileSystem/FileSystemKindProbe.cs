// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem;

using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

/// <summary>Observes local, SMB and Linux NFS filesystems without creating the proposed directory.</summary>
/// <remarks>The result describes the current mount or accessible ancestor. It does not qualify storage durability or coordination. Mount options and credentials are not returned.</remarks>
/// <example><code>var kind = FileSystemKindProbe.Probe(Path.Combine(Path.GetTempPath(), "report-store"));</code></example>
public static class FileSystemKindProbe
{
    /// <summary>Recognizes ordinary and extended Windows UNC paths without accessing a server.</summary>
    /// <param name="path">The Windows path to classify.</param>
    /// <returns>Whether the path names a UNC namespace. Local extended paths and device paths return false.</returns>
    /// <exception cref="ArgumentNullException">The path is null.</exception>
    /// <example><code>var isRemote = FileSystemKindProbe.IsUncPath(@"\\server\share\reports");</code></example>
    public static bool IsUncPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase) || path.StartsWith(@"\\", StringComparison.Ordinal) &&
            !path.StartsWith(@"\\?\", StringComparison.Ordinal) && !path.StartsWith(@"\\.\", StringComparison.Ordinal);
    }

    /// <summary>Classifies a Linux mount-table filesystem name without accessing the filesystem.</summary>
    /// <param name="fileSystem">The case-sensitive filesystem name, without mount options.</param>
    /// <returns>The supported local or remote kind, or Unsupported for an unrecognized name, including null.</returns>
    /// <example><code>var kind = FileSystemKindProbe.ClassifyLinux("nfs4"); // FileSystemKind.Nfs</code></example>
    public static FileSystemKind ClassifyLinux(string fileSystem) => fileSystem switch
    {
        "cifs" or "smb3" or "smbfs" => FileSystemKind.Smb,
        "nfs" or "nfs4" => FileSystemKind.Nfs,
        "ext2" or "ext3" or "ext4" or "xfs" or "btrfs" or "tmpfs" or "ramfs" or "overlay" or "zfs" or "f2fs" or
            "ntfs" or "ntfs3" or "exfat" or "vfat" or "msdos" or "jfs" or "reiserfs" or "bcachefs" or "ubifs" => FileSystemKind.Local,
        _ => FileSystemKind.Unsupported
    };

    /// <summary>Observes the filesystem containing a fully qualified path, which need not exist yet.</summary>
    /// <param name="root">The normalized absolute path to inspect.</param>
    /// <returns>The supported observed kind, or Unsupported when the native protocol cannot be identified or the host is neither Windows nor Linux.</returns>
    /// <remarks>Linux selects the longest matching mount in /proc/self/mountinfo. Windows recognizes local drives and inspects the nearest existing directory on network drives for SMB 3 or later. Native filesystem access can block or throw I/O and access exceptions.</remarks>
    /// <example><code>var kind = FileSystemKindProbe.Probe(Path.Combine(Path.GetTempPath(), "report-store"));</code></example>
    public static FileSystemKind Probe(string root) =>
        OperatingSystem.IsLinux() ? LinuxKind(root) : OperatingSystem.IsWindows() ? WindowsKind(root) : FileSystemKind.Unsupported;

    private static FileSystemKind LinuxKind(string root)
    {
        var longest = -1;
        var kind = FileSystemKind.Unsupported;
        foreach (var line in File.ReadLines("/proc/self/mountinfo"))
        {
            var separator = line.IndexOf(" - ", StringComparison.Ordinal);
            if (separator < 0)
            {
                continue;
            }

            var fields = line[..separator].Split(' ', 7);
            if (fields.Length < 6)
            {
                continue;
            }

            var mount = fields[4].Replace(@"\040", " ").Replace(@"\011", "\t").Replace(@"\012", "\n").Replace(@"\134", "\\");
            if (mount.Length <= longest || root != mount && !root.StartsWith(mount.TrimEnd('/') + "/", StringComparison.Ordinal))
            {
                continue;
            }

            longest = mount.Length;
            var type = line[(separator + 3)..];
            var end = type.IndexOf(' ');
            kind = ClassifyLinux(end < 0 ? type : type[..end]);
        }

        return kind;
    }

    private static FileSystemKind WindowsKind(string root)
    {
        var driveRoot = Path.GetPathRoot(root);
        if (driveRoot.StartsWith(@"\\?\", StringComparison.Ordinal) && !IsUncPath(driveRoot))
        {
            driveRoot = driveRoot[4..];
        }

        var drive = IsUncPath(root) ? DriveType.Network : new DriveInfo(driveRoot).DriveType;
        if (drive is DriveType.Unknown or DriveType.NoRootDirectory)
        {
            return FileSystemKind.Unsupported;
        }

        if (drive != DriveType.Network)
        {
            return FileSystemKind.Local;
        }

        var existing = root;
        while (!Directory.Exists(existing))
        {
            existing = Path.GetDirectoryName(existing);
            if (string.IsNullOrEmpty(existing))
            {
                return FileSystemKind.Unsupported;
            }
        }

        using var handle = CreateFile(FileSystemPaths.ToExtendedWindowsPath(existing), 0x80, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            return FileSystemKind.Unsupported;
        }

        var information = Marshal.AllocHGlobal(512);
        try
        {
            return GetFileInformationByHandleEx(handle, 13, information, 512) && Marshal.ReadInt32(information, 4) == 0x00020000 &&
                Marshal.ReadInt16(information, 8) >= 3 ? FileSystemKind.Smb : FileSystemKind.Unsupported;
        }
        finally
        {
            Marshal.FreeHGlobal(information);
        }
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int informationClass, IntPtr information, uint size);
}