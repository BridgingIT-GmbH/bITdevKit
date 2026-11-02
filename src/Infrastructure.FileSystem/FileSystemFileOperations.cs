namespace BridgingIT.DevKit.Infrastructure.FileSystem;

/// <summary>Provides instrumented file opening shared by ordinary files, blobs and documents.</summary>
/// <remarks>Callers choose sharing and buffering semantics. This helper does not acquire ownership, validate paths or publish a storage record.</remarks>
/// <example><code>using var file = FileSystemFileOperations.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);</code></example>
public static class FileSystemFileOperations
{
    /// <summary>Opens a file and records its managed open attempt and successful read or write access.</summary>
    /// <param name="path">The path validated by the calling provider.</param>
    /// <param name="mode">The creation or opening mode.</param>
    /// <param name="access">The required file access.</param>
    /// <param name="share">Access permitted to other handles.</param>
    /// <param name="bufferSize">The FileStream buffer size.</param>
    /// <param name="options">Asynchronous, sequential or other native open options.</param>
    /// <returns>A caller-owned file stream.</returns>
    /// <example><code>await using var file = FileSystemFileOperations.Open(path, FileMode.Create, FileAccess.Write, FileShare.None, options: FileOptions.Asynchronous);</code></example>
    public static FileStream Open(
        string path,
        FileMode mode,
        FileAccess access,
        FileShare share,
        int bufferSize = 65536,
        FileOptions options = FileOptions.None)
    {
        Interlocked.Increment(ref FileSystemMetrics.ManagedFileOpenAttempts);
        var stream = new FileStream(path, mode, access, share, bufferSize, options);

        if ((access & FileAccess.Read) != 0)
        {
            Interlocked.Increment(ref FileSystemMetrics.FileReadOpens);
        }

        if ((access & FileAccess.Write) != 0)
        {
            Interlocked.Increment(ref FileSystemMetrics.FileWriteOpens);
        }

        return stream;
    }
}
