namespace BridgingIT.DevKit.Infrastructure.FileSystem;

using System.Text.Json;

/// <summary>Describes one bounded retention-discovery result.</summary>
/// <param name="Paths">Candidate record directories requiring an expiration recheck under key authority.</param>
/// <param name="HasMore">Whether traversal has pending frames, including when cursor authority was busy. False completes a traversal; future calls wrap.</param>
/// <param name="Inspected">Managed discovery steps and reserved manifest inspections consumed by this call.</param>
/// <example><code>var page = await store.RetentionCandidatesAsync("blobs", null, cancellationToken);</code></example>
public sealed record FileSystemRetentionPage(IReadOnlyList<string> Paths, bool HasMore, int Inspected)
{
    /// <summary>Gets candidate record directories requiring an expiration recheck under key authority.</summary>
    /// <example><code>foreach (var path in page.Paths) { /* Recheck under key authority. */ }</code></example>
    public IReadOnlyList<string> Paths { get; init; } = Paths;

    /// <summary>Gets whether traversal has pending frames or cursor acquisition was busy. False completes a traversal; future calls wrap.</summary>
    /// <example><code>var more = page.HasMore;</code></example>
    public bool HasMore { get; init; } = HasMore;

    /// <summary>Gets the managed discovery steps and reserved manifest inspections consumed by the call.</summary>
    /// <example><code>var inspected = page.Inspected;</code></example>
    public int Inspected { get; init; } = Inspected;
}

/// <summary>Represents one persisted frame in a bounded virtual-radix directory traversal.</summary>
/// <param name="Directory">The validated relative store directory using slash separators.</param>
/// <param name="Level">The directory depth within the traversal namespace.</param>
/// <param name="Prefix">The lowercase hexadecimal prefix to inspect at this depth.</param>
/// <example><code>var frame = new FileSystemScanFrame("blobs", 0, "");</code></example>
public sealed record FileSystemScanFrame(string Directory, int Level, string Prefix)
{
    /// <summary>Gets the validated relative store directory using slash separators.</summary>
    /// <example><code>var directory = frame.Directory;</code></example>
    public string Directory { get; init; } = Directory;

    /// <summary>Gets the directory depth within the traversal namespace.</summary>
    /// <example><code>var level = frame.Level;</code></example>
    public int Level { get; init; } = Level;

    /// <summary>Gets the lowercase hexadecimal prefix to inspect at this depth.</summary>
    /// <example><code>var prefix = frame.Prefix;</code></example>
    public string Prefix { get; init; } = Prefix;
}

/// <summary>Persists a bounded virtual-radix retention walk that resumes across calls and store instances.</summary>
/// <remarks>Bounds managed traversal steps and frontier size. Native prefix enumeration cost is not a proven network-RPC or wall-time bound.</remarks>
/// <example><code>var page = await FileSystemRetentionCursor.DiscoverAsync(store, "blobs", null, cancellationToken);</code></example>
public static class FileSystemRetentionCursor
{
    private const string HEX = "0123456789abcdef";

    /// <summary>Acquires a non-waiting cursor owner, advances the configured discovery budget and durably checkpoints progress.</summary>
    /// <param name="store">The store owning the cursor and record namespaces.</param>
    /// <param name="kind">Either <c>blobs</c> or <c>documents</c>.</param>
    /// <param name="type">Optional document type identity; null for all types or blobs.</param>
    /// <param name="cancellationToken">Cancels between discovery steps.</param>
    /// <returns>A bounded page, or an empty page with HasMore true when the cursor owner is busy.</returns>
    /// <exception cref="InvalidDataException">A persisted frontier or managed directory name is invalid.</exception>
    /// <example><code>var page = await FileSystemRetentionCursor.DiscoverAsync(store, "documents", typeIdentity, cancellationToken);</code></example>
    public static async Task<FileSystemRetentionPage> DiscoverAsync(FileSystemStore store, string kind, string type, CancellationToken cancellationToken)
    {
        store.Initialize();
        var options = store.Options;
        var relative = type is null ? kind : kind + "/" + FileSystemStore.Hash([type]);
        var widths = kind == "blobs" ? new[]
        {
            64,
            2,
            64
        }
        : type is null ? [64, 64, 2, 64] : [64, 2, 64];
        var cursorDirectory = Path.Combine(options.RootPath, "retention", FileSystemStore.Hash([kind, type ?? ""]));
        FileSystemDurability.EnsureDirectory(cursorDirectory);
        using var authority = await store.AcquireAsync(cursorDirectory, true, cancellationToken).ConfigureAwait(false);

        if (authority is null)
        {
            return new([], true, 0);
        }

        var cursor = Path.Combine(cursorDirectory, "cursor.json");
        FileSystemStore.RejectLinks(cursor);
        List<FileSystemScanFrame> pending;

        if (File.Exists(cursor))
        {
            Interlocked.Increment(ref FileSystemMetrics.ManagedFileOpenAttempts);
            using var file = File.OpenRead(cursor);

            if (file.Length > 4_194_304)
            {
                throw new InvalidDataException("Retention cursor exceeds its bound.");
            }

            pending = JsonSerializer.Deserialize<List<FileSystemScanFrame>>(file) ?? throw new InvalidDataException("Missing retention cursor.");

            if (pending.Count > 4096)
            {
                throw new InvalidDataException("Invalid retention cursor frontier.");
            }

            foreach (var frame in pending)
            {
                Validate(frame, relative, widths);
            }
        }
        else
        {
            pending = [];
        }

        if (pending.Count == 0)
        {
            pending.Add(new(relative, 0, ""));
        }

        var paths = new List<string>();
        var inspected = 0;
        var budget = options.MaxRetentionMetadataEntries;

        while (pending.Count > 0 && inspected < budget)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var frame = pending[^1];
            pending.RemoveAt(pending.Count - 1);
            var directory = Path.Combine(options.RootPath, frame.Directory.Replace('/', Path.DirectorySeparatorChar));
            FileSystemStore.RejectLinks(directory);

            if (frame.Level == widths.Length)
            {
                inspected++;
                paths.Add(directory);
                continue;
            }

            if (frame.Prefix.Length == widths[frame.Level])
            {
                inspected++;
                var child = Path.Combine(directory, frame.Prefix);
                FileSystemStore.RejectLinks(child);

                if (System.IO.Directory.Exists(child))
                {
                    pending.Add(new(frame.Directory + "/" + frame.Prefix, frame.Level + 1, ""));
                }

                continue;
            }

            inspected++;

            if (!System.IO.Directory.Exists(directory))
            {
                continue;
            }

            using var children = System.IO.Directory.EnumerateDirectories(directory, frame.Prefix + "*", SearchOption.TopDirectoryOnly).GetEnumerator();

            if (!children.MoveNext())
            {
                continue;
            }

            var first = Path.GetFileName(children.Current);

            if (frame.Prefix.Length == 0 && FileSystemStore.IsDirectoryStaging(children.Current))
            {
                Expand(pending, frame);
                continue;
            }

            if (!ValidName(first, widths[frame.Level]))
            {
                throw new InvalidDataException("Unexpected directory in managed storage.");
            }

            if (children.MoveNext())
            {
                Expand(pending, frame);
            }
            else
            {
                pending.Add(new(frame.Directory + "/" + first, frame.Level + 1, ""));
            }
        }

        var bytes = JsonSerializer.SerializeToUtf8Bytes(pending);

        if (bytes.Length > 4_194_304)
        {
            throw new InvalidDataException("Retention cursor exceeds its bound.");
        }

        var staging = Path.Combine(cursorDirectory, $"cursor-{Guid.NewGuid():N}.tmp");

        try
        {
            Interlocked.Increment(ref FileSystemMetrics.ManagedFileOpenAttempts);
            File.WriteAllBytes(staging, bytes);
            FileSystemDurability.FlushFile(staging);

            try
            {
                FileSystemDurability.Move(staging, cursor, true, true);
                store.Fault?.Invoke("cursor-published");
            }
            catch
            {
                authority.RetainIfShared();
                throw;
            }
        }
        finally
        {
            if (File.Exists(staging))
            {
                File.Delete(staging);
            }
        }

        Interlocked.Add(ref FileSystemMetrics.DiscoveryInspections, inspected);

        return new(paths, pending.Count != 0, inspected);
    }

    private static void Expand(List<FileSystemScanFrame> pending, FileSystemScanFrame frame)
    {
        for (var i = HEX.Length - 1; i >= 0; i--)
        {
            pending.Add(frame with { Prefix = frame.Prefix + HEX[i] });
        }
    }

    private static bool ValidName(string value, int length)
    {
        return value is not null && value.Length == length && value.All(c => HEX.Contains(c));
    }

    private static void Validate(FileSystemScanFrame frame, string root, int[] widths)
    {
        if (frame is null || frame.Level < 0 || frame.Level > widths.Length || frame.Directory is null || frame.Prefix is null
            || (frame.Level == widths.Length ? frame.Prefix.Length != 0 : frame.Prefix.Length > widths[frame.Level])
            || !frame.Prefix.All(c => HEX.Contains(c)))
        {
            throw new InvalidDataException("Invalid retention cursor.");
        }

        if (frame.Directory != root && !frame.Directory.StartsWith(root + "/", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Retention cursor escapes its namespace.");
        }

        var suffix = frame.Directory == root ? "" : frame.Directory[(root.Length + 1)..];
        var parts = suffix.Length == 0 ? Array.Empty<string>() : suffix.Split('/');

        if (parts.Length != frame.Level || parts.Where((part, index) => !ValidName(part, widths[index])).Any())
        {
            throw new InvalidDataException("Retention cursor escapes its namespace.");
        }
    }
}