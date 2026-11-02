namespace BridgingIT.DevKit.Infrastructure.FileSystem.Probes;

using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

internal static class RemoteFileSystemProbe
{
    internal static void Validate(QualificationProfile profile)
    {
        var os = OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsLinux() ? "Linux" : "Unsupported";

        if (profile.HostOs != os || profile.AccessKind is not ("Smb" or "Nfs") || (os == "Windows" && profile.AccessKind == "Nfs"))
        {
            throw new InvalidDataException("Qualification profile is not applicable to this OS/protocol.");
        }

        ProbeWorkspace.RejectLinks(profile.ScratchRoot);

        if (!Directory.Exists(profile.ScratchRoot))
        {
            throw new InvalidDataException("Share scratch root must already exist on the configured mounted share. No local replacement root is created.");
        }

        if (OperatingSystem.IsWindows())
        {
            ValidateWindows(profile.ScratchRoot);
        }
        else
        {
            ValidateLinux(profile);
        }
    }

    internal static string KernelIdentity()
    {
        return OperatingSystem.IsLinux() ? File.ReadAllText("/proc/sys/kernel/random/boot_id").Trim() : null;
    }

    private static void ValidateWindows(string root)
    {
        var full = Path.GetFullPath(root);
        var native = full.StartsWith(@"\\?\", StringComparison.Ordinal) ? full : full.StartsWith(@"\\", StringComparison.Ordinal) ? @"\\?\UNC\" + full[2..] : @"\\?\" + full;
        using var handle = CreateFile(native, 0x80, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero);

        if (handle.IsInvalid)
        {
            throw new InvalidDataException("Cannot inspect the configured remote directory. Check OS-managed access and share availability.");
        }

        var buffer = Marshal.AllocHGlobal(512);

        try
        {
            if (!GetFileInformationByHandleEx(handle, 13, buffer, 512) || Marshal.ReadInt32(buffer, 4) != 0x00020000 || Marshal.ReadInt16(buffer, 8) < 3)
            {
                throw new InvalidDataException("Qualification requires independently observable SMB 3.x remote protocol information; local/WebDAV/unknown protocols are refused.");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static void ValidateLinux(QualificationProfile profile)
    {
        var buffer = Marshal.AllocHGlobal(512);

        try
        {
            if (StatFs(profile.ScratchRoot, buffer) != 0)
            {
                throw new InvalidDataException("Cannot inspect the configured mounted filesystem.");
            }

            var magic = unchecked((uint)(IntPtr.Size == 8 ? Marshal.ReadInt64(buffer) : Marshal.ReadInt32(buffer)));

            if (profile.AccessKind == "Nfs" ? magic != 0x6969 : magic is not (0xFF534D42 or 0xFE534D42))
            {
                throw new InvalidDataException("Qualification root is not the declared mounted SMB/NFS filesystem. Local roots are never network evidence.");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        var path = Path.GetFullPath(profile.ScratchRoot);
        string[] selected = null;
        var longest = -1;

        foreach (var line in File.ReadLines("/proc/self/mountinfo"))
        {
            var parts = line.Split(' ');
            var separator = Array.IndexOf(parts, "-");

            if (separator < 0 || separator + 3 >= parts.Length || parts.Length < 6)
            {
                continue;
            }

            var mount = parts[4].Replace(@"\040", " ").Replace(@"\011", "\t").Replace(@"\134", "\\");

            if (mount.Length <= longest || (path != mount && !path.StartsWith(mount.TrimEnd('/') + "/", StringComparison.Ordinal)))
            {
                continue;
            }

            longest = mount.Length;
            selected = parts[(separator + 1)..];
        }

        // Read only protocol/version fields. Never persist mount options, which can contain OS-managed identities.
        var options = selected?[2].Split(',') ?? [];
        var versionText = options.FirstOrDefault(p => p.StartsWith("vers=", StringComparison.Ordinal))?[5..];

        if (profile.AccessKind == "Smb" ? versionText?.StartsWith("3.", StringComparison.Ordinal) != true
            : !System.Version.TryParse(versionText, out var version) || version.Major != 4 || version.Minor < 1)
        {
            throw new InvalidDataException("Mount metadata does not establish the required SMB 3.x or Linux NFSv4.1+ version. No fallback qualification is allowed.");
        }
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int informationClass, IntPtr information, uint size);

    [DllImport("libc", EntryPoint = "statfs", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern int StatFs([MarshalAs(UnmanagedType.LPUTF8Str)] string path, IntPtr buffer);
}