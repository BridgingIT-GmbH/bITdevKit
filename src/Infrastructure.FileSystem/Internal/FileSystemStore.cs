namespace BridgingIT.DevKit.Infrastructure.FileSystem;

using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

/// <summary>Describes a versioned blob or document manifest, including deletion tombstones.</summary>
/// <remarks>Use provider APIs for normal operations. Changing a manifest directly requires the storage protocol's ownership and persistence rules.</remarks>
/// <example><code>var record = store.Read(path, ["blobs", "reports", "daily"]);</code></example>
public sealed record FileSystemRecord
{
    /// <summary>Gets the on-disk format version. Current records use version 2.</summary>
    /// <example><code>var version = record.Version;</code></example>
    public int Version { get; init; } = 2;

    /// <summary>Gets the ordinal logical identity: kind/container/name or kind/type/partition/row.</summary>
    /// <example><code>var identity = record.Identity;</code></example>
    public string[] Identity { get; init; }

    /// <summary>Gets the immutable payload filename relative to its record directory, or null for a tombstone.</summary>
    /// <example><code>var filename = record.Payload;</code></example>
    public string Payload { get; init; }

    /// <summary>Gets the committed payload length in bytes.</summary>
    /// <example><code>var bytes = record.Length;</code></example>
    public long Length { get; init; }

    /// <summary>Gets blob metadata for a live blob record; otherwise null.</summary>
    /// <example><code>var etag = record.Blob?.ETag;</code></example>
    public BlobInfo Blob { get; init; }

    /// <summary>Gets document metadata for a live document record; otherwise null. Content is stored separately.</summary>
    /// <example><code>var etag = record.Document?.ETag;</code></example>
    public StoredDocument Document { get; init; }

    /// <summary>Gets whether this manifest is a deletion tombstone.</summary>
    /// <example><code>var deleted = record.Deleted;</code></example>
    public bool Deleted { get; init; }

    /// <summary>Gets the mutation identifier used to reconcile a tombstone or an uncertain publication.</summary>
    /// <example><code>var mutation = record.MutationId;</code></example>
    public string MutationId { get; init; }
}

/// <summary>Coordinates filesystem manifests, immutable payloads, retention and explicit recovery.</summary>
/// <remarks>
/// This low-level API is for trusted tooling operating on a provider-owned root. Paths and records must come from this store.
/// Acquire key authority before reading metadata used by a mutation or opening its selected payload. Keep it through publication.
/// All cooperating participants must use the same protocol. Shared deployment and power-loss qualification remain separate.
/// </remarks>
/// <example><code>var store = new FileSystemStore(options); var path = store.Resolve(["blobs", "reports", "daily"], false);</code></example>
public sealed class FileSystemStore
{
    private readonly FileSystemStorageOptions options;

    /// <summary>Gets the shared, read-only manifest serializer settings.</summary>
    /// <example><code>var json = JsonSerializer.Serialize(record, FileSystemStore.Json);</code></example>
    public static JsonSerializerOptions Json { get; } = CreateJson();

    private static readonly Encoding identityEncoding = new UTF8Encoding(false, true);
    private readonly object initialization = new();
    private bool initialized;

    /// <summary>Gets an independent copy of the configuration captured at construction.</summary>
    /// <remarks>Changing the returned object does not reconfigure this store or an existing provider.</remarks>
    /// <example><code>var root = store.Options.RootPath;</code></example>
    public FileSystemStorageOptions Options => new()
    {
        RootPath = this.options.RootPath,
        AccessKind = this.options.AccessKind,
        CreateRoot = this.options.CreateRoot,
        Durability = this.options.Durability,
        LockAcquireTimeout = this.options.LockAcquireTimeout,
        TransferBufferSize = this.options.TransferBufferSize,
        MaxManifestBytes = this.options.MaxManifestBytes,
        MaxQueryMetadataEntries = this.options.MaxQueryMetadataEntries,
        MaxRetentionMetadataEntries = this.options.MaxRetentionMetadataEntries
    };

    /// <summary>Gets the captured maximum number of metadata entries examined by a query.</summary>
    /// <example><code>var limit = store.MaxQueryMetadataEntries;</code></example>
    public int MaxQueryMetadataEntries => this.options.MaxQueryMetadataEntries;

    /// <summary>Gets or sets a synchronous diagnostic callback at staging, publication and reclamation boundaries.</summary>
    /// <remarks>
    /// Leave null in normal operation. Callbacks run within storage operations and may hold authority.
    /// Throwing simulates failure, can produce an indeterminate result, and may require explicit quiescent recovery.
    /// Boundary names are diagnostic events rather than a scheduling or concurrency contract.
    /// </remarks>
    /// <example><code>store.Fault = boundary => Console.WriteLine(boundary);</code></example>
    public Action<string> Fault { get; set; }

    /// <summary>Creates a store with a private configuration copy, without accessing the filesystem.</summary>
    /// <param name="options">Valid Windows or Linux configuration with an absolute provider-owned root.</param>
    /// <exception cref="ArgumentNullException">Configuration is null.</exception>
    /// <exception cref="ArgumentException">Configuration or host/protocol applicability is invalid.</exception>
    /// <example><code>var store = new FileSystemStore(options);</code></example>
    public FileSystemStore(FileSystemStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Validate().IsFailure)
        {
            throw new ArgumentException("Invalid filesystem options.", nameof(options));
        }

        this.options = new()
        {
            RootPath = Path.GetFullPath(options.RootPath),
            AccessKind = options.AccessKind,
            CreateRoot = options.CreateRoot,
            Durability = options.Durability,
            LockAcquireTimeout = options.LockAcquireTimeout,
            TransferBufferSize = options.TransferBufferSize,
            MaxManifestBytes = options.MaxManifestBytes,
            MaxQueryMetadataEntries = options.MaxQueryMetadataEntries,
            MaxRetentionMetadataEntries = options.MaxRetentionMetadataEntries
        };
    }

    /// <summary>Initializes or validates the versioned root marker and tests required publication operations once per instance.</summary>
    /// <remarks>May create the root when allowed. Unsupported operations fail; successful initialization does not qualify a server or hardware against power loss.</remarks>
    /// <example><code>store.Initialize();</code></example>
    public void Initialize()
    {
        if (Volatile.Read(ref this.initialized))
        {
            return;
        }

        lock (this.initialization)
        {
            if (this.initialized)
            {
                return;
            }

            this.InitializeCore();
            Volatile.Write(ref this.initialized, true);
        }
    }

    private void InitializeCore()
    {
        RejectLinks(this.options.RootPath);

        if (!this.options.CreateRoot && !Directory.Exists(this.options.RootPath))
        {
            throw new FileSystemConfigurationException("Configured root does not exist and creation is disabled.");
        }

        FileSystemDurability.EnsureDirectory(Path.GetFullPath(this.options.RootPath));
        FileSystemDurability.SyncDirectory(this.options.RootPath);
        var marker = Path.Combine(this.options.RootPath, "store.json");

        if (!this.options.CreateRoot && !File.Exists(marker))
        {
            throw new FileSystemConfigurationException("Creation is disabled and the store marker is missing.");
        }

        var staged = Path.Combine(this.options.RootPath, $"store-{Guid.NewGuid():N}.tmp");

        try
        {
            if (!File.Exists(marker))
            {
                Interlocked.Increment(ref FileSystemMetrics.ManagedFileOpenAttempts);
                File.WriteAllText(staged, "{\"version\":2,\"accessKind\":\"" + this.options.AccessKind + "\",\"storeId\":\"" + Guid.NewGuid().ToString("N") + "\"}");
                FileSystemDurability.FlushFile(staged);

                try
                {
                    FileSystemDurability.Move(staged, marker, false, true);
                }
                catch (IOException exception)when (FileSystemDurability.IsAlreadyExists(exception))
                {
                }
            }

            Interlocked.Increment(ref FileSystemMetrics.ManagedFileOpenAttempts);
            using var stream = new FileStream(marker, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            if (stream.Length > 4096)
            {
                throw new InvalidDataException("Invalid store marker.");
            }

            using var document = JsonDocument.Parse(stream);

            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number)
                || !document.RootElement.TryGetProperty("accessKind", out var access) || access.ValueKind != JsonValueKind.String
                || !document.RootElement.TryGetProperty("storeId", out var id) || id.ValueKind != JsonValueKind.String
                || !Guid.TryParseExact(id.GetString(), "N", out _))
            {
                throw new InvalidDataException("Malformed store marker.");
            }

            if (number != 2 || access.GetString() != this.options.AccessKind.ToString())
            {
                throw new FileSystemConfigurationException("Incompatible store format or access kind.");
            }

            FileSystemDurability.ValidatePublicationDirectory(this.options.RootPath);
        }
        finally
        {
            RemoveTemporary(staged);
        }
    }

    /// <summary>Resolves an ordinal logical identity, initializes the store, and optionally creates its record directory.</summary>
    /// <param name="identity">Exactly three segments for blobs or four for documents, beginning with <c>blobs</c> or <c>documents</c>.</param>
    /// <param name="create">Whether to create missing record directories. Use false for reads.</param>
    /// <returns>The absolute record directory. It may be absent when creation is disabled.</returns>
    /// <example><code>var path = store.Resolve(["blobs", "reports", "daily"], false);</code></example>
    public string Resolve(string[] identity, bool create = true)
    {
        var path = this.RecordPath(identity);
        this.Initialize();
        RejectLinks(path);

        if (create)
        {
            FileSystemDurability.EnsureDirectory(path);
        }

        return path;
    }

    private string RecordPath(string[] identity)
    {
        var hash = Hash(identity);
        var group = Path.Combine(this.options.RootPath, identity[0], Hash([identity[1]]));

        if (identity[0] == "documents")
        {
            group = Path.Combine(group, Hash([identity[2]]));
        }

        return Path.Combine(group, hash[..2], hash);
    }

    /// <summary>Hashes strict UTF-8 segments with big-endian byte-length prefixes using SHA-256.</summary>
    /// <remarks>Preserves ordinal case, Unicode and segment boundaries, independently of host filename comparison.</remarks>
    /// <param name="identity">Non-null logical identity segments with valid Unicode.</param>
    /// <returns>A lowercase hexadecimal digest used by the version 2 format.</returns>
    /// <exception cref="EncoderFallbackException">A segment contains malformed UTF-16.</exception>
    /// <example><code>var hash = FileSystemStore.Hash(["blobs", "reports", "daily"]);</code></example>
    public static string Hash(string[] identity)
    {
        using var bytes = new MemoryStream();
        using var writer = new BinaryWriter(bytes, Encoding.UTF8, true);
        Span<byte> length = stackalloc byte[4];

        foreach (var segment in identity)
        {
            var encoded = identityEncoding.GetBytes(segment);
            BinaryPrimitives.WriteInt32BigEndian(length, encoded.Length);
            writer.Write(length);
            writer.Write(encoded);
        }

        return Convert.ToHexStringLower(SHA256.HashData(bytes.GetBuffer().AsSpan(0, (int)bytes.Length)));
    }

    /// <summary>Acquires this store's key or retention-cursor authority.</summary>
    /// <param name="path">A resolved record or store-owned retention directory.</param>
    /// <param name="immediate">Whether to return null immediately when busy instead of polling.</param>
    /// <param name="cancellationToken">Cancels acquisition polling. An outstanding native call may exceed the polling budget.</param>
    /// <returns>Disposable authority, or null when the directory is missing or immediate acquisition is busy.</returns>
    /// <example><code>using var authority = await store.AcquireAsync(path, false, cancellationToken);</code></example>
    public Task<FileSystemAuthority> AcquireAsync(string path, bool immediate, CancellationToken cancellationToken)
    {
        return FileSystemAuthority.AcquireAsync(this.options, path, "authority", immediate, cancellationToken, this.Fault);
    }

    /// <summary>Reads a live manifest only while holding authority, treating missing authority as absence.</summary>
    /// <param name="path">The record directory resolved by this store.</param>
    /// <param name="authority">The acquired authority, or null when the directory was absent.</param>
    /// <param name="identity">Optional expected ordinal identity for additional validation.</param>
    /// <returns>The live record, or null for missing authority, absence, or a tombstone.</returns>
    /// <exception cref="InvalidOperationException">Authority is disposed or belongs to another directory.</exception>
    /// <example><code>var record = store.ReadUnderAuthority(path, authority, ["blobs", "reports", "daily"]);</code></example>
    public FileSystemRecord ReadUnderAuthority(string path, FileSystemAuthority authority, string[] identity = null)
    {
        if (authority is null)
        {
            return null;
        }

        authority.EnsureHeld(path);

        return this.Read(path, identity);
    }

    /// <summary>Reads and validates a manifest without opening its payload; returns null for absence or a tombstone.</summary>
    /// <remarks>Hold key authority when the result will select a payload or determine a mutation.</remarks>
    /// <param name="path">The record directory resolved by this store.</param>
    /// <param name="identity">Optional expected ordinal identity for additional validation.</param>
    /// <returns>The live record, or null.</returns>
    /// <exception cref="InvalidDataException">Metadata, identity, format or path validation fails.</exception>
    /// <example><code>var current = store.Read(path, ["blobs", "reports", "daily"]);</code></example>
    public FileSystemRecord Read(string path, string[] identity = null)
    {
        var record = this.ReadPhysical(path, identity);

        return record?.Deleted == true ? null : record;
    }

    private FileSystemRecord ReadPhysical(string path, string[] identity = null)
    {
        Interlocked.Increment(ref FileSystemMetrics.MetadataReads);
        RejectLinks(path);
        var manifest = Path.Combine(path, "current.json");
        FileStream opened;
        Interlocked.Increment(ref FileSystemMetrics.ManagedFileOpenAttempts);

        try
        {
            opened = new FileStream(manifest, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }

        using var stream = opened;

        if (stream.Length > this.options.MaxManifestBytes)
        {
            throw new InvalidDataException("Manifest bound exceeded.");
        }

        var record = JsonSerializer.Deserialize<FileSystemRecord>(stream, Json);

        if (record is null || record.Version != 2 || record.Identity is not { Length: >= 3 } || record.Identity.Any(s => s is null)
            || (record.Identity[0] == "blobs" ? record.Identity.Length != 3 : record.Identity[0] != "documents" || record.Identity.Length != 4)
            || (!record.Deleted && (record.Identity[0] == "blobs" ? record.Blob is null || record.Document is not null
                || record.Blob.Key?.Container != record.Identity[1] || record.Blob.Key?.Name != record.Identity[2]
                : record.Document is null || record.Blob is not null || record.Document.Key.PartitionKey != record.Identity[2] || record.Document.Key.RowKey != record.Identity[3]))
            || (identity is not null && !record.Identity.SequenceEqual(identity, StringComparer.Ordinal))
            || (record.Deleted && !Guid.TryParseExact(record.MutationId, "N", out _))
            || (!record.Deleted && (record.Blob is not null ? string.IsNullOrWhiteSpace(record.Blob.ETag) || record.Blob.Properties is null
                : string.IsNullOrWhiteSpace(record.Document.ETag) || record.Document.Properties is null))
            || (!record.Deleted && (record.Payload is null || !record.Payload.StartsWith("payload-", StringComparison.Ordinal)
                || !record.Payload.EndsWith(".bin", StringComparison.Ordinal)
                || !Guid.TryParseExact(record.Payload[8..^4], "N", out _) || record.Length < 0)))
        {
            throw new InvalidDataException("Corrupt manifest or identity.");
        }

        try
        {
            if (Path.GetFullPath(path) != this.RecordPath(record.Identity))
            {
                throw new InvalidDataException("Corrupt manifest identity path.");
            }
        }
        catch (EncoderFallbackException)
        {
            throw new InvalidDataException("Corrupt Unicode in manifest identity.");
        }

        return record;
    }

    /// <summary>Copies a stream into an immutable, flushed generation while holding a generation owner.</summary>
    /// <param name="path">An existing record directory resolved by this store.</param>
    /// <param name="source">Readable content. The caller retains ownership of the stream.</param>
    /// <param name="maximum">Maximum allowed bytes, or null for no explicit size bound.</param>
    /// <param name="cancellationToken">Cancels copying or acquisition before publication.</param>
    /// <returns>A disposable stage containing its filename, byte length and SHA-256 content hash.</returns>
    /// <exception cref="FileSystemPayloadSizeException">Content exceeds the specified bound.</exception>
    /// <example><code>using var payload = await store.StageAsync(path, content, 1048576, cancellationToken);</code></example>
    public async Task<FileSystemStagedPayload> StageAsync(string path, Stream source, long? maximum, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref FileSystemMetrics.PayloadStages);
        var generation = Guid.NewGuid().ToString("N");
        var name = $"payload-{generation}.bin";
        var temporary = Path.Combine(path, $"copy-{generation}.tmp");
        var authority = await FileSystemAuthority.AcquireAsync(this.options, path, $"stage-{generation}.owner", false, cancellationToken).ConfigureAwait(false);
        var buffer = ArrayPool<byte>.Shared.Rent(this.options.TransferBufferSize);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long length = 0;

        try
        {
            await using (var output = FileSystemFileOperations.Open(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.Asynchronous))
            {
                int read;

                while ((read = await source.ReadAsync(buffer.AsMemory(0, this.options.TransferBufferSize), cancellationToken).ConfigureAwait(false)) != 0)
                {
                    length = checked(length + read);

                    if (maximum is not null && length > maximum)
                    {
                        throw new FileSystemPayloadSizeException(length, maximum.Value);
                    }

                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                }

                this.Fault?.Invoke("content-staged");
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                FileSystemDurability.Flush(output);
                this.Fault?.Invoke("content-flushed");
            }

            FileSystemDurability.Move(temporary, Path.Combine(path, name), false, true);
            Interlocked.Add(ref FileSystemMetrics.StagedBytes, length);

            return new(path, name, length, "sha256:" + Convert.ToHexStringLower(hash.GetHashAndReset()), authority);
        }
        catch
        {
            authority?.Dispose();
            RemoveTemporary(temporary);

            if (this.options.AccessKind == FileSystemStorageAccessKind.Local)
            {
                RemoveTemporary(Path.Combine(path, $"stage-{generation}.owner"));
            }

            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>Publishes complete metadata under live authority, applying configured persistence and optional reclamation.</summary>
    /// <param name="path">The record directory owned by the supplied authority.</param>
    /// <param name="record">A valid version 2 manifest with an existing immutable payload, or a tombstone.</param>
    /// <param name="authority">Live key authority acquired from this store and retained until publication finishes.</param>
    /// <param name="cancellationToken">Cancels before namespace publication. Cancellation during publication can be indeterminate.</param>
    /// <param name="payload">Optional stage to preserve once publication may have started.</param>
    /// <param name="forceDurable">Whether to require Durable acknowledgement even for a Buffered store.</param>
    /// <param name="collect">Whether to attempt bounded cleanup after successful publication.</param>
    /// <exception cref="FileSystemIndeterminateException">Publication may have happened. Do not automatically replay the mutation.</exception>
    /// <exception cref="FileSystemPublicationLimitException">Serialized metadata exceeds the configured byte bound.</exception>
    /// <example><code>store.Publish(path, updated, authority, cancellationToken, payload);</code></example>
    public void Publish(string path, FileSystemRecord record, FileSystemAuthority authority, CancellationToken cancellationToken, FileSystemStagedPayload payload = null, bool forceDurable = false, bool collect = true)
    {
        authority.EnsureHeld(path);
        var durable = forceDurable || this.options.Durability == FileSystemStorageDurability.Durable;

        if (durable && !record.Deleted)
        {
            FileSystemDurability.FlushFile(Path.Combine(path, record.Payload));
            FileSystemDurability.SyncDirectory(path);
            this.Fault?.Invoke("referenced-payload-flushed");
        }

        var bytes = JsonSerializer.SerializeToUtf8Bytes(record, Json);

        if (bytes.Length > this.options.MaxManifestBytes)
        {
            throw new FileSystemPublicationLimitException();
        }

        var staging = Path.Combine(path, $"manifest-{Guid.NewGuid():N}.tmp");

        try
        {
            Interlocked.Increment(ref FileSystemMetrics.ManagedFileOpenAttempts);
            File.WriteAllBytes(staging, bytes);
            this.Fault?.Invoke("manifest-staged");
            FileSystemDurability.FlushFile(staging);
            this.Fault?.Invoke("manifest-flushed");
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                payload?.Preserve();
                this.Fault?.Invoke("before-publication");
                FileSystemDurability.Move(staging, Path.Combine(path, "current.json"), true, durable);
                Interlocked.Increment(ref FileSystemMetrics.Publications);
                this.Fault?.Invoke("published");
                this.Fault?.Invoke("commit-flushed");
                this.Fault?.Invoke("acknowledged");
            }
            catch (Exception exception)when (exception is IOException or OperationCanceledException or FileSystemConfigurationException)
            {
                if (exception is FileSystemNativeIOException native)
                {
                    Interlocked.Exchange(ref FileSystemMetrics.IndeterminateNativeCode, native.Code);
                }

                authority.RetainIfShared();
                throw new FileSystemIndeterminateException(exception, record.Deleted ? "delete" : "publish", record.MutationId ?? record.Blob?.ETag ?? record.Document?.ETag);
            }
        }
        finally
        {
            RemoveTemporary(staging);
        }

        if (collect)
        {
            this.TryCollect(path, authority, durable);
        }
    }

    /// <summary>Publishes a deletion tombstone for a live record, or does nothing when the record is null.</summary>
    /// <param name="path">The record directory owned by the supplied authority.</param>
    /// <param name="current">The current record read under this same authority.</param>
    /// <param name="authority">Live key authority retained throughout deletion.</param>
    /// <param name="cancellationToken">Cancels before publication. An uncertain publication must not be replayed automatically.</param>
    /// <example><code>store.Delete(path, current, authority, cancellationToken);</code></example>
    public void Delete(string path, FileSystemRecord current, FileSystemAuthority authority, CancellationToken cancellationToken)
    {
        if (current is null)
        {
            return;
        }

        this.Publish(path, new() { Identity = current.Identity, Deleted = true, MutationId = Guid.NewGuid().ToString("N") }, authority, cancellationToken);
    }

    /// <summary>Opens an immutable payload for asynchronous reading and validates its committed length.</summary>
    /// <remarks>Call while holding key authority. After opening, the handle preserves the selected generation through replace or delete.</remarks>
    /// <param name="path">The record directory resolved by this store.</param>
    /// <param name="record">A live manifest read under the same key authority.</param>
    /// <returns>A stream owned by the caller, opened with deletion sharing.</returns>
    /// <exception cref="InvalidDataException">The committed payload is missing or has a different length.</exception>
    /// <example><code>await using var content = store.OpenPayload(path, current);</code></example>
    public FileStream OpenPayload(string path, FileSystemRecord record)
    {
        Interlocked.Increment(ref FileSystemMetrics.PayloadOpens);
        var payload = Path.Combine(path, record.Payload);
        RejectLinks(payload);
        FileStream stream;
        try
        {
            stream = FileSystemFileOperations.Open(payload, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1, FileOptions.Asynchronous | FileOptions.SequentialScan);
        }
        catch (FileNotFoundException)
        {
            throw new InvalidDataException("Committed payload is missing.");
        }

        if (stream.Length == record.Length)
        {
            return stream;
        }

        stream.Dispose();
        throw new InvalidDataException("Payload length mismatch.");
    }

    /// <summary>Lazily enumerates record directories, pruning the hashed container or document type and partition when specified.</summary>
    /// <param name="kind">The namespace, either <c>blobs</c> or <c>documents</c>.</param>
    /// <param name="groupIdentity">Optional blob container or document type identity.</param>
    /// <param name="partitionIdentity">Optional document partition. Requires a document type.</param>
    /// <returns>Unordered record directory paths; each manifest still requires validation.</returns>
    /// <example><code>foreach (var path in store.Paths("blobs", "reports")) { /* inspect under key authority */ }</code></example>
    public IEnumerable<string> Paths(string kind, string groupIdentity = null, string partitionIdentity = null)
    {
        this.Initialize();
        var root = Path.Combine(this.options.RootPath, kind);

        if (groupIdentity is not null)
        {
            root = Path.Combine(root, Hash([groupIdentity]));
        }

        if (kind == "documents" && partitionIdentity is not null)
        {
            root = Path.Combine(root, Hash([partitionIdentity]));
        }

        if (!Directory.Exists(root))
        {
            yield break;
        }

        var levels = kind == "blobs" ? (groupIdentity is null ? 3 : 2) : groupIdentity is null ? 4 : partitionIdentity is null ? 3 : 2;

        foreach (var record in Enumerate(root, levels))
        {
            yield return record;
        }
    }

    private static IEnumerable<string> Enumerate(string root, int levels)
    {
        RejectLinks(root);

        if (levels == 0)
        {
            yield return root;
            yield break;
        }

        foreach (var child in Directory.EnumerateDirectories(root))
        {
            RejectLinks(child);

            if (IsDirectoryStaging(child))
            {
                continue;
            }

            foreach (var record in Enumerate(child, levels - 1))
            {
                yield return record;
            }
        }
    }

    /// <summary>Tests whether a path's filename matches a temporary directory-initialization name.</summary>
    /// <example><code>var staged = FileSystemStore.IsDirectoryStaging(path);</code></example>
    public static bool IsDirectoryStaging(string path)
    {
        var name = Path.GetFileName(path);

        return name.StartsWith(".directory-", StringComparison.Ordinal) && Guid.TryParseExact(name[11..], "N", out _);
    }

    /// <summary>Rejects existing symlinks or reparse points in a path and its ancestors.</summary>
    /// <remarks>This check does not sandbox a root against a concurrent hostile writer. Managed roots require application-only write access.</remarks>
    /// <exception cref="InvalidDataException">An existing path component is a link or reparse point.</exception>
    /// <example><code>FileSystemStore.RejectLinks(path);</code></example>
    public static void RejectLinks(string path)
    {
        FileSystemPaths.RejectLinks(path);
    }

    /// <summary>Advances persisted bounded retention discovery, resuming across store instances and wrapping after each traversal.</summary>
    /// <param name="kind">Either <c>blobs</c> or <c>documents</c>.</param>
    /// <param name="type">Optional document type identity; use null for all types or blobs.</param>
    /// <param name="cancellationToken">Cancels discovery before its next step.</param>
    /// <returns>Candidate directories. Expiration must be rechecked under key authority before deletion.</returns>
    /// <example><code>var page = await store.RetentionCandidatesAsync("blobs", null, cancellationToken);</code></example>
    public Task<FileSystemRetentionPage> RetentionCandidatesAsync(string kind, string type, CancellationToken cancellationToken)
    {
        return FileSystemRetentionCursor.DiscoverAsync(this, kind, type, cancellationToken);
    }

    /// <summary>Inspects bounded directory entries and reclaims unreferenced payloads and staging files protected by no active generation owner.</summary>
    /// <param name="path">The record directory owned by the supplied authority.</param>
    /// <param name="authority">Live key authority retained through cleanup.</param>
    /// <param name="maximum">Maximum directory entries to inspect.</param>
    /// <param name="currentIsDurable">True only when this same uninterrupted authority scope just confirmed Durable publication of the current manifest. Otherwise leave false to checkpoint before reclamation.</param>
    /// <remarks>Open readers retain their file handles. Shared owners left by crashes require explicit quiescent recovery.</remarks>
    /// <example><code>store.Collect(path, authority);</code></example>
    public void Collect(string path, FileSystemAuthority authority, int maximum = 32, bool currentIsDurable = false)
    {
        authority.EnsureHeld(path);
        var current = this.ReadPhysical(path);
        var inspected = 0;
        var checkpointed = currentIsDurable;

        foreach (var file in Directory.EnumerateFiles(path))
        {
            if (++inspected > maximum)
            {
                break;
            }

            var name = Path.GetFileName(file);

            if (name.StartsWith("manifest-", StringComparison.Ordinal) && name.EndsWith(".tmp", StringComparison.Ordinal) && Guid.TryParseExact(name[9..^4], "N", out _))
            {
                RejectLinks(file);
                File.Delete(file);
                continue;
            }

            var payload = name.StartsWith("payload-", StringComparison.Ordinal) && name.EndsWith(".bin", StringComparison.Ordinal);
            var copy = name.StartsWith("copy-", StringComparison.Ordinal) && name.EndsWith(".tmp", StringComparison.Ordinal);

            if (!payload && !copy)
            {
                continue;
            }

            if (current is { Deleted: false } && current.Payload == name)
            {
                continue;
            }

            var generation = payload ? name[8..^4] : name[5..^4];

            if (!Guid.TryParseExact(generation, "N", out _))
            {
                continue;
            }

            var owner = Path.Combine(path, $"stage-{generation}.owner");

            if (this.options.AccessKind != FileSystemStorageAccessKind.Local && File.Exists(owner))
            {
                continue;
            }

            FileSystemAuthority claim = null;

            try
            {
                claim = FileSystemAuthority.TryAcquire(this.options, path, $"stage-{generation}.owner");

                if (claim is null)
                {
                    continue;
                }

                if (!checkpointed && current is not null)
                {
                    this.Fault?.Invoke("before-reclamation-checkpoint");
                    this.Publish(path, current, authority, default, forceDurable: true, collect: false);
                    checkpointed = true;
                }

                RejectLinks(file);
                var reclaimedBytes = new FileInfo(file).Length;

                try
                {
                    File.Delete(file);
                } // Open readers use delete sharing and retain their immutable file handle.
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                Interlocked.Increment(ref FileSystemMetrics.ReclaimedFiles);
                Interlocked.Add(ref FileSystemMetrics.ReclaimedBytes, reclaimedBytes);
            }
            finally
            {
                claim?.Dispose();
            }

            try
            {
                if (this.options.AccessKind == FileSystemStorageAccessKind.Local && File.Exists(owner))
                {
                    File.Delete(owner);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>Attempts bounded reclamation and counts recoverable cleanup failures without failing an acknowledged mutation.</summary>
    /// <param name="path">The record directory owned by the supplied authority.</param>
    /// <param name="authority">Live key authority retained through cleanup.</param>
    /// <param name="currentIsDurable">Leave false unless this uninterrupted authority scope just confirmed Durable publication of the current manifest.</param>
    /// <example><code>store.TryCollect(path, authority);</code></example>
    public void TryCollect(string path, FileSystemAuthority authority, bool currentIsDurable = false)
    {
        try
        {
            this.Fault?.Invoke("before-reclamation");
            this.Collect(path, authority, currentIsDurable: currentIsDurable);
        }
        catch (Exception exception)when (exception is IOException or UnauthorizedAccessException or FileSystemConfigurationException or NotSupportedException or JsonException)
        {
            Interlocked.Increment(ref FileSystemMetrics.CleanupFailures);
        }
    }

    /// <summary>Explicitly clears retained ownership and collects abandoned data after operator-established global quiescence.</summary>
    /// <param name="allParticipantsStopped">An operator assertion that every reader, writer, sweeper and recovery process on every host is stopped, old server operations and session replays are drained or cancelled, and uncertain mutations have been reconciled against fresh server metadata.</param>
    /// <param name="cancellationToken">Cancels between recovery steps; completed cleanup is not rolled back.</param>
    /// <remarks>Cannot establish quiescence, drain server queues or reconcile mutation history itself. Never use as automatic timeout recovery.</remarks>
    /// <exception cref="FileSystemConfigurationException">The explicit quiescence assertion is false.</exception>
    /// <example><code>await store.RecoverQuiescentAsync(allParticipantsStopped: true, cancellationToken); // Only after the documented operator procedure.</code></example>
    public async Task RecoverQuiescentAsync(bool allParticipantsStopped, CancellationToken cancellationToken)
    {
        if (!allParticipantsStopped)
        {
            throw new FileSystemConfigurationException("Recovery requires explicit quiescence of every writer, reader and sweeper on every host.");
        }

        this.Initialize();
        RemoveQuiescentDirectoryStages(this.options.RootPath, cancellationToken);

        foreach (var file in Directory.EnumerateFiles(this.options.RootPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = Path.GetFileName(file);
            var publicationSuffix = name.EndsWith(".tmp", StringComparison.Ordinal) ? 4 : name.EndsWith(".check", StringComparison.Ordinal) ? 6 : 0;

            if ((name.StartsWith("store-", StringComparison.Ordinal) && name.EndsWith(".tmp", StringComparison.Ordinal) && Guid.TryParseExact(name[6..^4], "N", out _))
                || (name.StartsWith(".publication-", StringComparison.Ordinal) && publicationSuffix != 0 && Guid.TryParseExact(name[13..^publicationSuffix], "N", out _)))
            {
                RejectLinks(file);
                File.Delete(file);
            }
        }

        foreach (var path in this.Paths("blobs").Concat(this.Paths("documents")))
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.ReleaseQuiescentMarkers(path);
            using var authority = await this.AcquireAsync(path, false, cancellationToken).ConfigureAwait(false);
            this.Collect(path, authority, int.MaxValue);
        }

        var retention = Path.Combine(this.options.RootPath, "retention");

        if (Directory.Exists(retention))
        {
            foreach (var directory in Directory.EnumerateDirectories(retention))
            {
                RejectLinks(directory);
                this.ReleaseQuiescentMarkers(directory);
            }
        }
    }

    private static void RemoveQuiescentDirectoryStages(string directory, CancellationToken cancellationToken)
    {
        RejectLinks(directory);

        foreach (var child in Directory.EnumerateDirectories(directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            RejectLinks(child);

            if (IsDirectoryStaging(child))
            {
                Directory.Delete(child);
            }
            else
            {
                RemoveQuiescentDirectoryStages(child, cancellationToken);
            }
        }
    }

    private void ReleaseQuiescentMarkers(string directory)
    {
        foreach (var marker in Directory.EnumerateFiles(directory))
        {
            var name = Path.GetFileName(marker);

            if (name != "authority" && !(name.StartsWith("stage-", StringComparison.Ordinal) && name.EndsWith(".owner", StringComparison.Ordinal)
                && Guid.TryParseExact(name[6..^6], "N", out _)) && !(name.StartsWith("owner-", StringComparison.Ordinal) && name.EndsWith(".tmp", StringComparison.Ordinal)
                && Guid.TryParseExact(name[6..^4], "N", out _)))
            {
                continue;
            }

            RejectLinks(marker);

            if (this.options.AccessKind == FileSystemStorageAccessKind.Local)
            {
                using var check = new FileStream(marker, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }

            File.Delete(marker);
        }
    }

    private static JsonSerializerOptions CreateJson()
    {
        var options = new JsonSerializerOptions
        {
            MaxDepth = 32
        };
        options.Converters.Add(new FileSystemPropertyBagConverter());
        options.MakeReadOnly(true);

        return options;
    }

    private static void RemoveTemporary(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

/// <summary>Reports a mutation whose namespace publication may have completed despite a failed acknowledgement.</summary>
/// <param name="inner">The failure observed during publication.</param>
/// <param name="operation">The attempted operation name.</param>
/// <param name="mutationId">The attempted mutation identity, or null to generate one.</param>
/// <remarks>Do not automatically retry. Shared authority is retained until explicit quiescent recovery.</remarks>
/// <example><code>catch (FileSystemIndeterminateException error) { Console.WriteLine(error.MutationId); }</code></example>
public sealed class FileSystemIndeterminateException(Exception inner, string operation = "delete", string mutationId = null) : IOException("Publication outcome is indeterminate.", inner)
{
    /// <summary>Gets the attempted operation name.</summary>
    /// <example><code>var operation = error.Operation;</code></example>
    public string Operation { get; } = operation;

    /// <summary>Gets the identifier to reconcile against committed metadata.</summary>
    /// <example><code>var mutation = error.MutationId;</code></example>
    public string MutationId { get; } = mutationId ?? Guid.NewGuid().ToString("N");
}

/// <summary>Reports streamed payload content exceeding its configured byte limit.</summary>
/// <param name="length">The observed byte length when copying stopped.</param>
/// <param name="maximum">The permitted byte limit.</param>
/// <example><code>catch (FileSystemPayloadSizeException error) { Console.WriteLine(error.Maximum); }</code></example>
public sealed class FileSystemPayloadSizeException(long length, long maximum) : Exception
{
    /// <summary>Gets the observed byte length when copying stopped.</summary>
    /// <example><code>var observed = error.Length;</code></example>
    public long Length { get; } = length;

    /// <summary>Gets the permitted byte limit.</summary>
    /// <example><code>var limit = error.Maximum;</code></example>
    public long Maximum { get; } = maximum;
}

/// <summary>Reports a metadata query exceeding its configured inspection bound.</summary>
/// <param name="message">A diagnostic explanation of the exceeded bound.</param>
/// <example><code>catch (FileSystemMetadataLimitException error) { Console.WriteLine(error.Message); }</code></example>
public sealed class FileSystemMetadataLimitException(string message) : Exception(message);

/// <summary>Reports incompatible store configuration or an unsupported required filesystem operation.</summary>
/// <param name="message">A diagnostic explanation without credentials.</param>
/// <example><code>catch (FileSystemConfigurationException error) { Console.WriteLine(error.Message); }</code></example>
public sealed class FileSystemConfigurationException(string message) : Exception(message);

/// <summary>Reports serialized manifest bytes exceeding the configured publication bound.</summary>
/// <example><code>catch (FileSystemPublicationLimitException) { /* Reduce stored metadata. */ }</code></example>
public sealed class FileSystemPublicationLimitException : Exception;

/// <summary>Reports a malformed or mismatched document continuation cursor.</summary>
/// <param name="message">A diagnostic explanation of the invalid cursor.</param>
/// <example><code>catch (FileSystemContinuationException error) { Console.WriteLine(error.Message); }</code></example>
public sealed class FileSystemContinuationException(string message) : Exception(message);
