namespace BridgingIT.DevKit.Infrastructure.FileSystem;

using System.Diagnostics;

/// <summary>Owns cooperative filesystem authority for one key, staged generation or retention cursor.</summary>
/// <remarks>Instances can only be acquired. Shared ownership does not expire or transfer after handle revocation; crashed or uncertain owners require explicit quiescent recovery.</remarks>
/// <example><code>using var authority = await store.AcquireAsync(path, false, cancellationToken);</code></example>
public sealed class FileSystemAuthority : IDisposable
{
    private readonly FileStream handle;
    private readonly string marker;
    private readonly string identity;
    private bool disposed;
    private bool retain;
    private readonly long heldAt = Stopwatch.GetTimestamp();
    private readonly string scope;

    private FileSystemAuthority(string directory, FileStream handle, string marker, string identity)
    {
        this.Directory = directory;
        this.handle = handle;
        this.marker = marker;
        this.identity = identity;
        this.scope = Path.GetFileName(marker ?? handle.Name).StartsWith("stage-", StringComparison.Ordinal) ? "generation"
            : Path.GetFileName(Path.GetDirectoryName(directory)) == "retention" ? "cursor" : "key";
        Interlocked.Increment(ref FileSystemMetrics.AuthorityAcquisitions);
    }

    /// <summary>Gets the absolute directory for which this instance acquired authority.</summary>
    /// <example><code>var directory = authority.Directory;</code></example>
    public string Directory { get; }

    /// <summary>Gets whether authority uses a persistent shared namespace owner rather than a local exclusive handle.</summary>
    /// <example><code>var shared = authority.IsShared;</code></example>
    public bool IsShared => this.marker is not null;

    /// <summary>Acquires authority with optional bounded polling and cancellation.</summary>
    /// <param name="options">Configuration matching every participant's access protocol and acquisition budget.</param>
    /// <param name="directory">An existing absolute provider-owned directory.</param>
    /// <param name="name">The single filename of the cooperative authority marker.</param>
    /// <param name="immediate">Whether to return null immediately when busy.</param>
    /// <param name="cancellationToken">Cancels polling; outstanding native calls may exceed the budget.</param>
    /// <param name="fault">Optional synchronous diagnostic callback when the requested directory is absent.</param>
    /// <returns>Disposable authority, or null for a missing directory or busy immediate attempt.</returns>
    /// <exception cref="FileSystemAuthorityTimeoutException">The polling budget expires.</exception>
    /// <example><code>using var authority = await FileSystemAuthority.AcquireAsync(options, path, "authority", false, cancellationToken);</code></example>
    public static async Task<FileSystemAuthority> AcquireAsync(FileSystemStorageOptions options, string directory, string name, bool immediate, CancellationToken cancellationToken, Action<string> fault = null)
    {
        if (!System.IO.Directory.Exists(directory))
        {
            fault?.Invoke("authority-directory-missing");

            return null;
        }

        var watch = Stopwatch.StartNew();

        try
        {
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                var acquired = TryAcquire(options, directory, name);

                if (acquired is not null)
                {
                    return acquired;
                }

                if (immediate)
                {
                    return null;
                }

                if (watch.Elapsed >= options.LockAcquireTimeout)
                {
                    throw new FileSystemAuthorityTimeoutException(options.LockAcquireTimeout);
                }

                await Task.Delay(10, cancellationToken).ConfigureAwait(false);
            }
            while (true);
        }
        finally
        {
            Interlocked.Add(ref FileSystemMetrics.AuthorityWaitTicks, watch.ElapsedTicks);
        }
    }

    /// <summary>Attempts one authority acquisition without waiting or reclaiming an existing owner.</summary>
    /// <param name="options">Configuration matching every participant's access protocol.</param>
    /// <param name="directory">An existing absolute provider-owned directory.</param>
    /// <param name="name">The single filename of the cooperative authority marker.</param>
    /// <returns>Disposable authority, or null when an existing owner prevents acquisition.</returns>
    /// <example><code>using var authority = FileSystemAuthority.TryAcquire(options, path, "authority");</code></example>
    public static FileSystemAuthority TryAcquire(FileSystemStorageOptions options, string directory, string name)
    {
        var path = Path.Combine(directory, name);
        FileSystemStore.RejectLinks(path);

        if (options.AccessKind == FileSystemStorageAccessKind.Local)
        {
            Interlocked.Increment(ref FileSystemMetrics.ManagedFileOpenAttempts);

            try
            {
                return new(directory, new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None), null, null);
            }
            catch (IOException exception)when ((exception.HResult & 0xffff)is 32 or 33 or 11)
            {
                return null;
            }
        }

        if (!File.Exists(path))
        {
            var identity = Guid.NewGuid().ToString("N");
            var staging = Path.Combine(directory, $"owner-{identity}.tmp");

            try
            {
                Interlocked.Increment(ref FileSystemMetrics.ManagedFileOpenAttempts);
                using (var file = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    file.Write(Encoding.ASCII.GetBytes(identity));
                    FileSystemDurability.Flush(file);
                }

                try
                {
                    FileSystemDurability.Move(staging, path, false, true);

                    return new(directory, null, path, identity);
                }
                catch (IOException exception)when (FileSystemDurability.IsAlreadyExists(exception))
                {
                }
            }
            finally
            {
                if (File.Exists(staging))
                {
                    File.Delete(staging);
                }
            }
        }

        return null;
    }

    /// <summary>Checks that this instance is undisposed and belongs to the exact directory.</summary>
    /// <remarks>Does not establish server freshness or permit takeover. Root integrity and cooperative ownership remain required.</remarks>
    /// <exception cref="InvalidOperationException">The instance is disposed or belongs to a different directory.</exception>
    /// <example><code>authority.EnsureHeld(path);</code></example>
    public void EnsureHeld(string directory)
    {
        if (this.disposed || directory != this.Directory)
        {
            throw new InvalidOperationException("Publication requires its live key authority.");
        }
    }

    /// <summary>Prevents disposal from removing shared authority after an uncertain mutation.</summary>
    /// <remarks>Local handles still close on disposal. Retained shared ownership requires explicit quiescent recovery.</remarks>
    /// <example><code>authority.RetainIfShared();</code></example>
    public void RetainIfShared()
    {
        this.retain = this.marker is not null;
    }

    /// <summary>Closes local authority or attempts release of this confirmed shared owner unless it was retained.</summary>
    /// <remarks>Idempotent. A failed shared release may leave ownership in place for explicit recovery.</remarks>
    /// <example><code>authority.Dispose();</code></example>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        var held = Stopwatch.GetTimestamp() - this.heldAt;
        Interlocked.Add(ref FileSystemMetrics.AuthorityHoldTicks, held);

        if (this.scope == "generation")
        {
            Interlocked.Add(ref FileSystemMetrics.GenerationAuthorityHoldTicks, held);
        }
        else if (this.scope == "cursor")
        {
            Interlocked.Add(ref FileSystemMetrics.CursorAuthorityHoldTicks, held);
        }
        else
        {
            Interlocked.Add(ref FileSystemMetrics.KeyAuthorityHoldTicks, held);
        }

        if (this.retain)
        {
            Interlocked.Increment(ref FileSystemMetrics.RetainedOwnershipEvents);
        }

        this.handle?.Dispose();

        if (this.marker is not null && !this.retain)
        {
            try
            {
                Interlocked.Increment(ref FileSystemMetrics.ManagedFileOpenAttempts);

                if (File.ReadAllText(this.marker) != this.identity)
                {
                    return;
                }

                File.Delete(this.marker);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}

/// <summary>Reports an authority acquisition that exhausted its polling budget.</summary>
/// <param name="timeout">The configured acquisition budget.</param>
/// <example><code>catch (FileSystemAuthorityTimeoutException error) { Console.WriteLine(error.Timeout); }</code></example>
public sealed class FileSystemAuthorityTimeoutException(TimeSpan timeout) : TimeoutException("Filesystem authority is busy or needs quiescent recovery.")
{
    /// <summary>Gets the acquisition budget that expired.</summary>
    /// <example><code>var budget = error.Timeout;</code></example>
    public TimeSpan Timeout { get; } = timeout;
}