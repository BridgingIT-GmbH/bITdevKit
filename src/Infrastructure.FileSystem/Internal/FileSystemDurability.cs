namespace BridgingIT.DevKit.Infrastructure.FileSystem;

using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

/// <summary>Performs native same-directory publication and persistence operations on Windows and Linux.</summary>
/// <remarks>These operations do not qualify hardware, controller caches, remote stable storage or power-loss behavior. Use only with trusted provider-owned paths.</remarks>
/// <example><code>FileSystemDurability.FlushFile(staging); FileSystemDurability.Move(staging, target, true, true);</code></example>
public static class FileSystemDurability
{
    /// <summary>Tests replacement with an active reader and atomic no-replace installation using disposable files in a directory.</summary>
    /// <param name="directory">An existing provider-owned directory with create, read, flush and delete permissions.</param>
    /// <exception cref="FileSystemConfigurationException">The filesystem does not preserve the tested publication semantics.</exception>
    /// <example><code>FileSystemDurability.ValidatePublicationDirectory(root);</code></example>
    public static void ValidatePublicationDirectory(string directory)
    {
        var identity = Guid.NewGuid().ToString("N");
        var source = Path.Combine(directory, $".publication-{identity}.tmp");
        var target = Path.Combine(directory, $".publication-{identity}.check");

        try
        {
            File.WriteAllBytes(source, [1]);
            File.WriteAllBytes(target, [0]);
            FlushFile(source);
            using var pinned = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            Move(source, target, true, true);

            if (pinned.ReadByte() != 0)
            {
                throw new FileSystemConfigurationException("Namespace replacement does not preserve the selected generation.");
            }

            File.WriteAllBytes(source, [2]);
            FlushFile(source);

            try
            {
                Move(source, target, false, true);
                throw new FileSystemConfigurationException("No-replace publication overwrote an existing namespace entry.");
            }
            catch (IOException exception)when (IsAlreadyExists(exception))
            {
            }

            File.Delete(target);
            Move(source, target, false, true);
        }
        finally
        {
            File.Delete(source);
            File.Delete(target);

            if (OperatingSystem.IsLinux())
            {
                SyncDirectory(directory);
            }
        }
    }

    /// <summary>Creates missing directory components with Windows write-through publication or Linux parent-directory synchronization.</summary>
    /// <remarks>Rejects links. On Linux, also synchronizes the parent when the directory already exists.</remarks>
    /// <example><code>FileSystemDurability.EnsureDirectory(path);</code></example>
    public static void EnsureDirectory(string path)
    {
        FileSystemPaths.RejectLinks(path);
        var parent = Path.GetDirectoryName(path);

        if (Directory.Exists(path))
        {
            // Another initializer may have made this name visible before persisting its parent entry.
            if (OperatingSystem.IsLinux() && parent is not null)
            {
                SyncDirectory(parent);
            }

            return;
        }

        if (parent is null)
        {
            throw new DirectoryNotFoundException();
        }

        EnsureDirectory(parent);

        if (OperatingSystem.IsWindows())
        {
            var staging = Path.Combine(parent, $".directory-{Guid.NewGuid():N}");
            Directory.CreateDirectory(staging);

            try
            {
                try
                {
                    Move(staging, path, false, true);
                }
                catch (IOException)when (Directory.Exists(path))
                {
                }
            }
            finally
            {
                if (Directory.Exists(staging))
                {
                    Directory.Delete(staging);
                }
            }
        }
        else
        {
            Directory.CreateDirectory(path);
            SyncDirectory(parent);
        }
    }

    /// <summary>Opens an existing file for write access and flushes its data through the operating system.</summary>
    /// <remarks>Rejects links and closes the temporary handle. File flushing alone does not persist namespace publication.</remarks>
    /// <example><code>FileSystemDurability.FlushFile(staging);</code></example>
    public static void FlushFile(string path)
    {
        FileSystemPaths.RejectLinks(path);
        Interlocked.Increment(ref FileSystemMetrics.ManagedFileOpenAttempts);
        using var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read | FileShare.Delete);
        Flush(file);
    }

    /// <summary>Flushes a file stream through the operating system while leaving the stream open.</summary>
    /// <remarks>Namespace publication requires a separate ordered operation.</remarks>
    /// <example><code>FileSystemDurability.Flush(output);</code></example>
    public static void Flush(FileStream file)
    {
        file.Flush(true);
        Interlocked.Increment(ref FileSystemMetrics.FileFlushes);
    }

    /// <summary>Publishes a same-directory namespace entry using native atomic replacement or exclusive installation.</summary>
    /// <param name="source">An existing staged file, or a directory for Windows exclusive initialization.</param>
    /// <param name="destination">The destination in the exact same directory.</param>
    /// <param name="overwrite">Whether to atomically replace the destination or fail if it exists.</param>
    /// <param name="durable">Whether to synchronize the containing Linux directory or request Windows write-through persistence.</param>
    /// <remarks>Flush source file data first. Linux exclusive installation uses link/unlink; failed source cleanup can leave an extra name. Any publication failure may require outcome reconciliation.</remarks>
    /// <exception cref="InvalidOperationException">Source and destination have different directory strings.</exception>
    /// <exception cref="FileSystemConfigurationException">A required native operation is unsupported.</exception>
    /// <example><code>FileSystemDurability.Move(staging, target, overwrite: true, durable: true);</code></example>
    public static void Move(string source, string destination, bool overwrite, bool durable)
    {
        Interlocked.Increment(ref FileSystemMetrics.NamespaceMoves);

        if (Path.GetDirectoryName(source) != Path.GetDirectoryName(destination))
        {
            throw new InvalidOperationException("Publication must stay in one directory.");
        }

        if (OperatingSystem.IsWindows() && overwrite)
        {
            WindowsReplace(source, destination, durable);

            return;
        }

        if (OperatingSystem.IsWindows())
        {
            if (durable)
            {
                Interlocked.Increment(ref FileSystemMetrics.WriteThroughMoves);
            }

            if (!MoveFileEx(WindowsPath(source), WindowsPath(destination), (overwrite ? 1u : 0u) | (durable ? 8u : 0u)))
            {
                var error = Marshal.GetLastPInvokeError();

                if (error is 1 or 50)
                {
                    throw new FileSystemConfigurationException("Windows same-volume write-through publication is unsupported by this filesystem/server.");
                }

                throw new FileSystemNativeIOException("write-through-move", error);
            }
        }
        else
        {
            if (!OperatingSystem.IsLinux())
            {
                throw new PlatformNotSupportedException("No atomic namespace implementation for this host.");
            }

            // BCL Move(false) can stat then rename, or fall back to copy. Neither is an exclusive namespace install.
            if (overwrite)
            {
                if (Rename(source, destination) != 0)
                {
                    throw new FileSystemNativeIOException("atomic-rename", Marshal.GetLastPInvokeError());
                }
            }
            else
            {
                if (Link(source, destination) != 0)
                {
                    var code = Marshal.GetLastPInvokeError();

                    if (code is 38 or 95)
                    {
                        throw new FileSystemConfigurationException("Atomic no-replace hard-link installation is unsupported by this filesystem/server.");
                    }

                    throw new FileSystemNativeIOException("atomic-no-replace-link", code);
                }

                if (Unlink(source) != 0)
                {
                    Interlocked.Increment(ref FileSystemMetrics.CleanupFailures);
                }
            }

            if (durable)
            {
                SyncDirectory(Path.GetDirectoryName(destination));
            }
        }
    }

    /// <summary>Tests a native exclusive-publication failure for the host's destination-already-exists code.</summary>
    /// <example><code>catch (IOException error) when (FileSystemDurability.IsAlreadyExists(error)) { /* Another owner won. */ }</code></example>
    public static bool IsAlreadyExists(IOException exception)
    {
        return exception is FileSystemNativeIOException native && (OperatingSystem.IsLinux() ? native.Code == 17 : native.Code is 80 or 183);
    }

    private static void WindowsReplace(string source, string destination, bool durable)
    {
        // POSIX rename keeps existing handles valid without Win32 delete-pending name races.
        // WRITE_THROUGH on the source handle requests persistence of the rename's NTFS metadata.
        using var handle = CreateFile(WindowsPath(source), 0x40010000, 7, IntPtr.Zero, 3, durable ? 0x80000000u : 0u, IntPtr.Zero);

        if (handle.IsInvalid)
        {
            throw new FileSystemNativeIOException("open-publication-for-rename", Marshal.GetLastPInvokeError());
        }

        var name = Encoding.Unicode.GetBytes(WindowsPath(destination));
        var rootOffset = IntPtr.Size == 8 ? 8 : 4;
        var lengthOffset = rootOffset + IntPtr.Size;
        var nameOffset = lengthOffset + 4;
        var size = checked(nameOffset + name.Length + 2);
        var buffer = Marshal.AllocHGlobal(size);

        try
        {
            Marshal.WriteInt32(buffer, 3); // REPLACE_IF_EXISTS | POSIX_SEMANTICS.
            Marshal.WriteIntPtr(buffer, rootOffset, IntPtr.Zero);
            Marshal.WriteInt32(buffer, lengthOffset, name.Length);
            Marshal.Copy(name, 0, IntPtr.Add(buffer, nameOffset), name.Length);
            Marshal.WriteInt16(buffer, nameOffset + name.Length, 0);

            if (!SetFileInformationByHandle(handle, 22, buffer, (uint)size))
            {
                var code = Marshal.GetLastPInvokeError();
                Interlocked.Exchange(ref FileSystemMetrics.IndeterminateNativeCode, code);

                if (code is 1 or 50 or 87)
                {
                    throw new FileSystemConfigurationException("Windows POSIX namespace replacement is unsupported by this filesystem/server.");
                }

                throw new FileSystemNativeIOException("posix-namespace-replace", code);
            }

            if (durable)
            {
                Interlocked.Increment(ref FileSystemMetrics.WriteThroughMoves);

                if (!FlushFileBuffers(handle))
                {
                    throw new FileSystemNativeIOException("flush-publication-handle", Marshal.GetLastPInvokeError());
                }

                Interlocked.Increment(ref FileSystemMetrics.FileFlushes);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>Synchronizes a Linux directory through fsync, or returns on Windows where publication uses write-through operations.</summary>
    /// <remarks>On Windows this method alone requests no persistence. Unsupported Linux directory synchronization fails explicitly.</remarks>
    /// <exception cref="PlatformNotSupportedException">The host is neither Windows nor Linux.</exception>
    /// <exception cref="FileSystemConfigurationException">Directory fsync is unsupported.</exception>
    /// <example><code>FileSystemDurability.SyncDirectory(directory);</code></example>
    public static void SyncDirectory(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("No namespace persistence implementation for this host.");
        }

        Interlocked.Increment(ref FileSystemMetrics.DirectorySyncs);
        var descriptor = Open(path, 0x10000 | 0x80000); // O_RDONLY | O_DIRECTORY | O_CLOEXEC on Linux.

        if (descriptor < 0)
        {
            throw new FileSystemNativeIOException("open-directory-for-fsync", Marshal.GetLastPInvokeError());
        }

        try
        {
            int result;
            do
            {
                result = Fsync(descriptor);
            }
            while (result != 0 && Marshal.GetLastPInvokeError() == 4);

            if (result != 0)
            {
                var error = Marshal.GetLastPInvokeError();

                if (error is 22 or 95)
                {
                    throw new FileSystemConfigurationException("Containing-directory fsync is unsupported by this filesystem/server.");
                }

                throw new FileSystemNativeIOException("directory-fsync", error);
            }
        }
        finally
        {
            Close(descriptor);
        }
    }

    private static string WindowsPath(string path)
    {
        var full = Path.GetFullPath(path);

        return full.StartsWith(@"\\?\", StringComparison.Ordinal) ? full
            : full.StartsWith(@"\\", StringComparison.Ordinal) ? @"\\?\UNC\" + full[2..] : @"\\?\" + full;
    }

    [DllImport("kernel32.dll", EntryPoint = "MoveFileExW", CharSet = CharSet.Unicode, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MoveFileEx(string source, string destination, uint flags);

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int informationClass, IntPtr information, uint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlushFileBuffers(SafeFileHandle handle);

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern int Open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);

    [DllImport("libc", EntryPoint = "fsync", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern int Fsync(int descriptor);

    [DllImport("libc", EntryPoint = "close", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern int Close(int descriptor);

    [DllImport("libc", EntryPoint = "link", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern int Link([MarshalAs(UnmanagedType.LPUTF8Str)] string source, [MarshalAs(UnmanagedType.LPUTF8Str)] string destination);

    [DllImport("libc", EntryPoint = "rename", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern int Rename([MarshalAs(UnmanagedType.LPUTF8Str)] string source, [MarshalAs(UnmanagedType.LPUTF8Str)] string destination);

    [DllImport("libc", EntryPoint = "unlink", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern int Unlink([MarshalAs(UnmanagedType.LPUTF8Str)] string path);
}

/// <summary>Reports a native filesystem failure while preserving the host-specific error code.</summary>
/// <param name="operation">The attempted native operation name.</param>
/// <param name="code">The native Windows error or Linux errno value.</param>
/// <example><code>catch (FileSystemNativeIOException error) { Console.WriteLine(error.Code); }</code></example>
public sealed class FileSystemNativeIOException(string operation, int code) : IOException($"Required filesystem primitive {operation} failed with native code {code}.", new Win32Exception(code))
{
    /// <summary>Gets the native error code from the actual runtime OS.</summary>
    /// <example><code>var code = error.Code;</code></example>
    public int Code { get; } = code;
}