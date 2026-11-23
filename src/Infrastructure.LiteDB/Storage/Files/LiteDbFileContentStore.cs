// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Application.Storage;

/// <summary>Owns immutable file generations, bounded chunks, and native namespace transactions.</summary>
/// <param name="database">The native feature database shared by the registered locations.</param>
/// <param name="clock">The upload expiry clock.</param>
public sealed class LiteDbFileContentStore(LiteDbFeatureDatabase database, TimeProvider clock = null)
{
    /// <summary>The maximum bytes retained in one chunk buffer.</summary>
    public const int ChunkSize = 65_536;
    private readonly TimeProvider clock = clock ?? TimeProvider.System;
    private readonly ConcurrentDictionary<Guid, int> readers = new();

    /// <summary>Gets the named native database.</summary>
    public LiteDbFeatureDatabase Database => database;

    /// <summary>Gets command-local filesystem entries.</summary>
    public LiteDbKeyedCollection<string, LiteDbFileEntry> Entries { get; } = database.Collection<string, LiteDbFileEntry>("entries");

    /// <summary>Gets immutable content generation descriptors.</summary>
    public LiteDbKeyedCollection<Guid, LiteDbFileGeneration> Generations { get; } = database.Collection<Guid, LiteDbFileGeneration>("generations");

    /// <summary>Gets immutable binary chunks.</summary>
    public LiteDbKeyedCollection<string, LiteDbFileChunk> Chunks { get; } = database.Collection<string, LiteDbFileChunk>("chunks");

    /// <summary>Gets namespace fences for each logical location.</summary>
    public LiteDbKeyedCollection<string, Guid> Fences { get; } = database.Collection<string, Guid>("fences");

    /// <summary>Creates an exact composite key without mapping a logical path to the host filesystem.</summary>
    /// <param name="location">The configured location.</param>
    /// <param name="path">The normalized logical path.</param>
    /// <returns>The durable entry key.</returns>
    public static string Key(string location, string path) => JsonSerializer.Serialize(new[] { location, path });

    /// <summary>Normalizes separators and rejects traversal and invalid path segments.</summary>
    /// <param name="path">A logical root-relative path.</param>
    /// <returns>The normalized path, with an empty string representing the root.</returns>
    public static string Normalize(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.Length > 4096) throw new ArgumentException("Logical file paths cannot exceed 4096 characters.");
        var parts = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(part => part is "." or ".." || part.Any(char.IsControl))) throw new ArgumentException("Logical paths cannot contain traversal or control characters.");
        return string.Join('/', parts);
    }

    /// <summary>Gets the normalized parent path.</summary>
    /// <param name="path">The normalized path.</param>
    /// <returns>The parent, or the empty root.</returns>
    public static string Parent(string path) => path.LastIndexOf('/') is var index && index >= 0 ? path[..index] : "";

    /// <summary>Ensures every ancestor is a directory inside the current native transaction.</summary>
    /// <param name="location">The configured location.</param>
    /// <param name="path">The normalized descendant.</param>
    public void EnsureParents(string location, string path)
    {
        var parents = new Stack<string>();
        for (var parent = Parent(path); parent.Length > 0; parent = Parent(parent)) parents.Push(parent);
        foreach (var parent in parents)
        {
            var key = Key(location, parent);
            if (this.Entries.TryGetValue(key, out var existing) && !existing.Directory) throw new LiteDbProviderException(new FileSystemError("An ancestor is a file.", parent));
            if (existing is null) this.Entries[key] = new() { Location = location, Path = parent, Directory = true, ModifiedUtc = this.clock.GetUtcNow().UtcDateTime };
        }
    }

    /// <summary>Starts an unpublished generation after capturing the current file and namespace fences.</summary>
    /// <param name="location">The configured location.</param>
    /// <param name="path">The normalized file path.</param>
    /// <returns>The detached upload descriptor.</returns>
    public LiteDbFileGeneration Begin(string location, string path)
    {
        if (path.Length == 0) throw new ArgumentException("A file path is required.");
        this.Entries.TryGetValue(Key(location, path), out var current);
        if (current?.Directory == true) throw new LiteDbProviderException(new FileSystemError("A directory owns this path.", path));
        var generation = new LiteDbFileGeneration
        {
            Id = Guid.NewGuid(), Location = location, Path = path, ExpectedGeneration = current?.Generation,
            NamespaceFence = this.Fences.GetOrAdd(location, _ => Guid.NewGuid()), ExpiresAt = this.clock.GetUtcNow().AddMinutes(30)
        };
        this.Generations.Add(generation.Id, generation);
        return generation;
    }

    /// <summary>Commits one complete bounded upload chunk without publishing a partial file.</summary>
    /// <param name="generation">The upload identity.</param>
    /// <param name="index">The current zero-based staging chunk.</param>
    /// <param name="content">One complete chunk.</param>
    /// <param name="cancellationToken">Cancels upload progress.</param>
    /// <returns>The committed chunk operation.</returns>
    public Task WriteChunkAsync(Guid generation, long index, byte[] content, CancellationToken cancellationToken) => database.ExecuteAsync(() =>
    {
        var upload = this.Generations[generation];
        if (upload.Published || upload.ExpiresAt <= this.clock.GetUtcNow() || content.Length is <= 0 or > ChunkSize) throw new LiteDbProviderException(new ConflictError("The file upload is no longer writable."));
        var key = ChunkKey(generation, index);
        if (index < Math.Max(0, upload.ChunkCount - 1) || index > upload.ChunkCount) throw new IOException("The upload chunk is outside its sequential window.");
        this.Chunks[key] = new() { Key = key, Generation = generation, Content = content };
        upload.ChunkCount = Math.Max(upload.ChunkCount, index + 1);
        upload.Length = Math.Max(upload.Length, checked(index * ChunkSize + content.Length));
        upload.ExpiresAt = this.clock.GetUtcNow().AddMinutes(30);
        return Task.CompletedTask;
    }, cancellationToken);

    /// <summary>Publishes complete content and matching metadata under the original ownership fences.</summary>
    /// <param name="generation">The completed upload.</param>
    /// <param name="cancellationToken">Cancels publication before commit.</param>
    /// <returns>The atomic publication operation.</returns>
    public Task PublishAsync(Guid generation, CancellationToken cancellationToken) => database.ExecuteAsync(() =>
    {
        var upload = this.Generations[generation];
        var key = Key(upload.Location, upload.Path);
        this.Entries.TryGetValue(key, out var current);
        if (upload.Published || upload.ExpiresAt <= this.clock.GetUtcNow() || current?.Directory == true || current?.Generation != upload.ExpectedGeneration || this.Fences[upload.Location] != upload.NamespaceFence)
            throw new LiteDbProviderException(new ConflictError("The file or namespace changed while content was being written."));
        this.EnsureParents(upload.Location, upload.Path);
        if (current is not null) this.Release(current.Generation);
        upload.Published = true;
        upload.References = 1;
        this.Entries[key] = new() { Location = upload.Location, Path = upload.Path, Generation = generation, Length = upload.Length, ModifiedUtc = this.clock.GetUtcNow().UtcDateTime };
        return Task.CompletedTask;
    }, cancellationToken);

    /// <summary>Abandons an unpublished generation so bounded maintenance can reclaim its chunks.</summary>
    /// <param name="generation">The upload identity.</param>
    /// <returns>The durable abandonment operation.</returns>
    public Task AbortAsync(Guid generation) => database.ExecuteAsync(() =>
    {
        if (this.Generations.TryGetValue(generation, out var upload) && !upload.Published) upload.ExpiresAt = DateTimeOffset.MinValue;
        return Task.CompletedTask;
    });

    /// <summary>Reads one immutable chunk of an open generation.</summary>
    /// <param name="generation">The content identity.</param>
    /// <param name="index">The zero-based chunk index.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A detached bounded byte array.</returns>
    public Task<byte[]> ReadChunkAsync(Guid generation, long index, CancellationToken cancellationToken) => database.ExecuteAsync(() =>
        Task.FromResult(this.Chunks.TryGetValue(ChunkKey(generation, index), out var chunk) ? chunk.Content : throw new IOException("The immutable file chunk is unavailable.")), cancellationToken);

    /// <summary>Protects an open generation from same-process content collection.</summary>
    /// <param name="generation">The content identity.</param>
    public void AddReader(Guid generation) => this.readers.AddOrUpdate(generation, 1, (_, count) => checked(count + 1));

    /// <summary>Releases an open generation after stream disposal.</summary>
    /// <param name="generation">The content identity.</param>
    public void RemoveReader(Guid generation)
    {
        while (this.readers.TryGetValue(generation, out var count))
        {
            if (count == 1)
            {
                if (((ICollection<KeyValuePair<Guid, int>>)this.readers).Remove(new(generation, count))) return;
            }
            else if (this.readers.TryUpdate(generation, count - 1, count)) return;
        }
    }

    /// <summary>Decrements a namespace reference while retaining open-reader content.</summary>
    /// <param name="generation">The content identity.</param>
    public void Release(Guid generation) => this.Generations[generation].References--;

    /// <summary>Reclaims a bounded number of unreferenced or abandoned content chunks.</summary>
    /// <param name="maximumChunks">The maximum chunks deleted in this transaction.</param>
    /// <param name="cancellationToken">Cancels maintenance before commit.</param>
    /// <returns>The removed chunk count.</returns>
    public Task<int> SweepContentAsync(int maximumChunks = 100, CancellationToken cancellationToken = default) => database.ExecuteAsync(() =>
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumChunks);
        var removed = 0;
        foreach (var generation in database.Query<LiteDbFileGeneration>("generations", BsonExpression.Create("$.References = 0 AND ($.Published = true OR $.ExpiresTicks <= @0)", this.clock.GetUtcNow().UtcTicks), Math.Min(maximumChunks, database.Options.MaxMaterializedRows / 4)))
        {
            if (this.readers.TryGetValue(generation.Id, out var count) && count > 0) continue;
            var chunks = database.Query<LiteDbFileChunk>("chunks", BsonExpression.Create("$.Generation = @0", generation.Id), Math.Min(maximumChunks - removed, database.Options.MaxMaterializedRows / 2));
            foreach (var chunk in chunks) { this.Chunks.Remove(chunk.Key); removed++; }

            if (database.Query<LiteDbFileChunk>("chunks", BsonExpression.Create("$.Generation = @0", generation.Id), 1).Count == 0) this.Generations.Remove(generation.Id);
            if (removed >= maximumChunks) break;
        }

        return Task.FromResult(removed);
    }, cancellationToken);

    private static string ChunkKey(Guid generation, long index) => generation.ToString("N") + ":" + index.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Stores one explicit directory or published file descriptor.</summary>
public sealed class LiteDbFileEntry : ILiteDbIndexedRecord
{
    /// <summary>Gets or sets the configured location.</summary>
    public string Location { get; set; }

    /// <summary>Gets or sets the logical path.</summary>
    public string Path { get; set; }

    /// <summary>Gets or sets whether this entry is a directory.</summary>
    public bool Directory { get; set; }

    /// <summary>Gets or sets the immutable content identity.</summary>
    public Guid Generation { get; set; }

    /// <summary>Gets or sets the actual content length.</summary>
    public long Length { get; set; }

    /// <summary>Gets or sets the UTC metadata timestamp.</summary>
    public DateTime ModifiedUtc { get; set; }

    /// <inheritdoc />
    public void WriteIndexes(BsonDocument document)
    {
        document["Location"] = LiteDbDocumentRecord.EncodeOrdinal(this.Location);
        document["Path"] = LiteDbDocumentRecord.EncodeOrdinal(this.Path);
        document["Parent"] = LiteDbDocumentRecord.EncodeOrdinal(LiteDbFileContentStore.Parent(this.Path));
        document["Directory"] = this.Directory;
    }
}

/// <summary>Stores ownership, publication, and retention state for immutable file content.</summary>
public sealed class LiteDbFileGeneration : ILiteDbIndexedRecord
{
    /// <summary>Gets or sets the generation identity.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets its location.</summary>
    public string Location { get; set; }

    /// <summary>Gets or sets its original publication path.</summary>
    public string Path { get; set; }

    /// <summary>Gets or sets the expected previous generation.</summary>
    public Guid? ExpectedGeneration { get; set; }

    /// <summary>Gets or sets the namespace ownership fence.</summary>
    public Guid NamespaceFence { get; set; }

    /// <summary>Gets or sets upload expiry.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Gets or sets whether publication completed.</summary>
    public bool Published { get; set; }

    /// <summary>Gets or sets the number of namespace references.</summary>
    public int References { get; set; }

    /// <summary>Gets or sets the chunk count.</summary>
    public long ChunkCount { get; set; }

    /// <summary>Gets or sets the complete byte length.</summary>
    public long Length { get; set; }

    /// <inheritdoc />
    public void WriteIndexes(BsonDocument document)
    {
        document["References"] = this.References;
        document["Published"] = this.Published;
        document["ExpiresTicks"] = this.ExpiresAt.UtcTicks;
    }
}

/// <summary>Stores one immutable bounded file chunk.</summary>
public sealed class LiteDbFileChunk : ILiteDbIndexedRecord
{
    /// <summary>Gets or sets the original logical chunk key.</summary>
    public string Key { get; set; }

    /// <summary>Gets or sets the generation identity.</summary>
    public Guid Generation { get; set; }

    /// <summary>Gets or sets complete chunk bytes.</summary>
    public byte[] Content { get; set; }

    /// <inheritdoc />
    public void WriteIndexes(BsonDocument document) => document["Generation"] = this.Generation;
}
