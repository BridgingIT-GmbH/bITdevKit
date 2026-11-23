// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using System.IO.Enumeration;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;

/// <summary>Provides a native virtual filesystem with explicit directories and atomic chunked publication.</summary>
/// <param name="store">The shared native content owner.</param>
/// <param name="locationName">The exact logical storage location.</param>
/// <example><code>services.AddFileStorage(factory => factory.RegisterProvider("ledger", files => files.UseLiteDb("ledger")));</code></example>
public sealed class LiteDbFileStorageProvider(LiteDbFileContentStore store, string locationName) : BaseFileStorageProvider(locationName)
{
    /// <inheritdoc />
    public override string Description { get; set; } = "LiteDB virtual file storage";

    /// <inheritdoc />
    public override bool SupportsNotifications => false;

    /// <inheritdoc />
    public override async Task<Result> FileExistsAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        await this.RunAsync(() => { _ = this.File(path); }, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public override async Task<Result<Stream>> ReadFileAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        LiteDbFileEntry pinned = null;
        var opened = await this.RunAsync(() =>
        {
            var file = this.File(path);
            store.AddReader(file.Generation);
            pinned = file;
            return file;
        }, cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure && pinned is not null) store.RemoveReader(pinned.Generation);
        return opened.IsFailure ? Result<Stream>.Failure(opened) : Result<Stream>.Success(new LiteDbFileReadStream(store, opened.Value.Generation, opened.Value.Length, progress));
    }

    /// <inheritdoc />
    public override async Task<Result<Stream>> OpenWriteFileAsync(string path, bool useTemporaryWrite = false, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        var opened = await this.RunAsync(() => store.Begin(this.LocationName, LiteDbFileContentStore.Normalize(path)), cancellationToken).ConfigureAwait(false);
        return opened.IsFailure ? Result<Stream>.Failure(opened) : Result<Stream>.Success(new LiteDbFileWriteStream(store, opened.Value.Id, progress, cancellationToken));
    }

    /// <inheritdoc />
    public override async Task<Result> WriteFileAsync(string path, Stream content, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var opened = await this.OpenWriteFileAsync(path, true, progress, cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure) return Result.Failure().WithErrors(opened.Errors);
        var stream = (LiteDbFileWriteStream)opened.Value;
        try
        {
            await content.CopyToAsync(stream, LiteDbFileContentStore.ChunkSize, cancellationToken).ConfigureAwait(false);
            await stream.DisposeAsync().ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception error)
        {
            await stream.AbortAsync().ConfigureAwait(false);
            return Failure(error, path);
        }
    }

    /// <inheritdoc />
    public override Task<Result> DeleteFileAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) => this.RunAsync(() =>
    {
        var file = this.File(path);
        store.Release(file.Generation);
        store.Entries.Remove(LiteDbFileContentStore.Key(this.LocationName, file.Path));
        this.Fence();
    }, cancellationToken);

    /// <inheritdoc />
    public override async Task<Result<string>> GetChecksumAsync(string path, CancellationToken cancellationToken = default)
    {
        var opened = await this.ReadFileAsync(path, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure) return Result<string>.Failure(opened);
        await using var stream = opened.Value;
        try { return Result<string>.Success(Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false))); }
        catch (Exception error) { return Result<string>.Failure().WithErrors(Failure(error, path).Errors); }
    }

    /// <inheritdoc />
    public override Task<Result<FileMetadata>> GetFileMetadataAsync(string path, CancellationToken cancellationToken = default) => this.RunAsync(() => Metadata(this.File(path)), cancellationToken);

    /// <inheritdoc />
    public override Task<Result> SetFileMetadataAsync(string path, FileMetadata metadata, CancellationToken cancellationToken = default) => this.RunAsync(() =>
    {
        var file = this.File(path);
        Apply(file, metadata);
    }, cancellationToken);

    /// <inheritdoc />
    public override Task<Result<FileMetadata>> UpdateFileMetadataAsync(string path, Func<FileMetadata, FileMetadata> metadataUpdate, CancellationToken cancellationToken = default) => this.RunAsync(() =>
    {
        ArgumentNullException.ThrowIfNull(metadataUpdate);
        var file = this.File(path);
        Apply(file, metadataUpdate(Metadata(file)));
        return Metadata(file);
    }, cancellationToken);

    /// <inheritdoc />
    public override Task<Result<(IEnumerable<string> Files, string NextContinuationToken)>> ListFilesAsync(string path, string searchPattern = null, bool recursive = false, string continuationToken = null, CancellationToken cancellationToken = default) => this.RunAsync(() =>
    {
        var root = LiteDbFileContentStore.Normalize(path);
        this.Directory(root);
        var fingerprint = JsonSerializer.Serialize(new { this.LocationName, Root = root, searchPattern, recursive });
        string after = null;
        if (continuationToken is not null)
        {
            if (continuationToken.Length > 16_384) throw new ArgumentException("Invalid file continuation.");
            var cursor = JsonSerializer.Deserialize<FileCursor>(Encoding.UTF8.GetString(Base64UrlHelper.Decode(continuationToken)));
            if (cursor is null || cursor.Selection != fingerprint || cursor.After is null) throw new ArgumentException("The file continuation does not match this query.");
            after = cursor.After;
        }

        var selected = this.Select(root, recursive, false).Select(entry => entry.Path)
            .Where(value => searchPattern is null || FileSystemName.MatchesSimpleExpression(searchPattern, value.AsSpan(value.LastIndexOf('/') + 1), false))
            .Order(StringComparer.Ordinal).Where(value => after is null || StringComparer.Ordinal.Compare(value, after) > 0).Take(101).ToArray();
        var next = selected.Length > 100 ? Base64UrlHelper.Encode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new FileCursor(fingerprint, selected[99])))) : null;
        return ((IEnumerable<string>)selected.Take(100).ToArray(), next);
    }, cancellationToken);

    /// <inheritdoc />
    public override Task<Result> CopyFileAsync(string path, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) => this.TransferAsync(path, destinationPath, false, cancellationToken);

    /// <inheritdoc />
    public override Task<Result> MoveFileAsync(string path, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) => this.TransferAsync(path, destinationPath, true, cancellationToken);

    /// <inheritdoc />
    public override Task<Result> RenameFileAsync(string path, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) => this.TransferAsync(path, destinationPath, true, cancellationToken);

    /// <inheritdoc />
    public override Task<Result> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default) => this.RunAsync(() => this.Directory(LiteDbFileContentStore.Normalize(path)), cancellationToken);

    /// <inheritdoc />
    public override Task<Result> CreateDirectoryAsync(string path, CancellationToken cancellationToken = default) => this.RunAsync(() =>
    {
        var normalized = LiteDbFileContentStore.Normalize(path);
        if (normalized.Length == 0) return;
        var key = LiteDbFileContentStore.Key(this.LocationName, normalized);
        if (store.Entries.TryGetValue(key, out var existing) && !existing.Directory) throw new ArgumentException("A file owns this directory path.");
        store.EnsureParents(this.LocationName, normalized);
        if (existing is null) store.Entries[key] = new() { Location = this.LocationName, Path = normalized, Directory = true, ModifiedUtc = DateTime.UtcNow };
    }, cancellationToken);

    /// <inheritdoc />
    public override Task<Result> DeleteDirectoryAsync(string path, bool recursive, CancellationToken cancellationToken = default) => this.RunAsync(() =>
    {
        var normalized = LiteDbFileContentStore.Normalize(path);
        if (normalized.Length == 0) throw new ArgumentException("The virtual root cannot be deleted.");
        this.Directory(normalized);
        var children = this.Select(normalized, true, null);
        if (!recursive && children.Count > 0) throw new ArgumentException("The directory is not empty.");
        foreach (var child in children)
        {
            if (!child.Directory) store.Release(child.Generation);
            store.Entries.Remove(LiteDbFileContentStore.Key(this.LocationName, child.Path));
        }

        store.Entries.Remove(LiteDbFileContentStore.Key(this.LocationName, normalized));
        this.Fence();
    }, cancellationToken);

    /// <inheritdoc />
    public override Task<Result> RenameDirectoryAsync(string path, string destinationPath, CancellationToken cancellationToken = default) => this.RunAsync(() =>
    {
        var source = LiteDbFileContentStore.Normalize(path);
        var target = LiteDbFileContentStore.Normalize(destinationPath);
        if (source.Length == 0 || target.Length == 0 || target == source || target.StartsWith(source + "/", StringComparison.Ordinal)) throw new ArgumentException("A directory cannot be moved into itself or its descendants.");
        this.Directory(source);
        var sourceKey = LiteDbFileContentStore.Key(this.LocationName, source);
        var entries = this.Select(source, true, null).Append(store.Entries[sourceKey]).ToArray();
        var moves = entries.Select(entry => (Entry: entry, Path: target + entry.Path[source.Length..])).ToArray();
        if (moves.Any(move => store.Entries.ContainsKey(LiteDbFileContentStore.Key(this.LocationName, move.Path)))) throw new ArgumentException("A destination path already exists.");
        store.EnsureParents(this.LocationName, target);
        foreach (var move in moves)
        {
            store.Entries.Remove(LiteDbFileContentStore.Key(this.LocationName, move.Entry.Path));
            move.Entry.Path = move.Path;
            store.Entries[LiteDbFileContentStore.Key(this.LocationName, move.Path)] = move.Entry;
        }

        this.Fence();
    }, cancellationToken);

    /// <inheritdoc />
    public override Task<Result<IEnumerable<string>>> ListDirectoriesAsync(string path, string searchPattern = null, bool recursive = false, CancellationToken cancellationToken = default) => this.RunAsync<IEnumerable<string>>(() =>
    {
        var root = LiteDbFileContentStore.Normalize(path);
        this.Directory(root);
        return this.Select(root, recursive, true).Select(entry => entry.Path).Where(value => searchPattern is null || FileSystemName.MatchesSimpleExpression(searchPattern, value.AsSpan(value.LastIndexOf('/') + 1), false)).Order(StringComparer.Ordinal).ToArray();
    }, cancellationToken);

    /// <inheritdoc />
    public override async Task<Result> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try { _ = await store.Database.InspectAsync(db => db.GetCollectionNames().ToArray(), cancellationToken).ConfigureAwait(false); return Result.Success(); }
        catch (Exception error) { return Failure(error, this.LocationName); }
    }

    private Task<Result> TransferAsync(string sourcePath, string targetPath, bool move, CancellationToken token) => this.RunAsync(() =>
    {
        var source = this.File(sourcePath);
        var target = LiteDbFileContentStore.Normalize(targetPath);
        if (target.Length == 0) throw new ArgumentException("A destination file path is required.");
        if (target == source.Path) return;
        var targetKey = LiteDbFileContentStore.Key(this.LocationName, target);
        store.Entries.TryGetValue(targetKey, out var previous);
        if (previous?.Directory == true) throw new ArgumentException("A directory owns the destination path.");
        store.EnsureParents(this.LocationName, target);
        if (previous is not null) store.Release(previous.Generation);
        store.Generations[source.Generation].References++;
        store.Entries[targetKey] = new() { Location = this.LocationName, Path = target, Generation = source.Generation, Length = source.Length, ModifiedUtc = source.ModifiedUtc };
        if (move)
        {
            store.Entries.Remove(LiteDbFileContentStore.Key(this.LocationName, source.Path));
            store.Release(source.Generation);
            this.Fence();
        }
    }, token);

    private IReadOnlyList<LiteDbFileEntry> Select(string root, bool recursive, bool? directory)
    {
        var expression = "$.Location = @0 AND " + (recursive ? "$.Path LIKE @1" : "$.Parent = @1");
        var values = new List<BsonValue> { LiteDbDocumentRecord.EncodeOrdinal(this.LocationName), recursive ? LiteDbDocumentRecord.EncodeOrdinal(root.Length == 0 ? "" : root + "/") + "%" : LiteDbDocumentRecord.EncodeOrdinal(root) };
        if (directory.HasValue) { expression += " AND $.Directory = @2"; values.Add(directory.Value); }

        return store.Database.Query<LiteDbFileEntry>("entries", BsonExpression.Create(expression, values.ToArray()), store.Database.Options.MaxMaterializedRows, "$.Path", failOnOverflow: true);
    }

    private LiteDbFileEntry File(string path)
    {
        var normalized = LiteDbFileContentStore.Normalize(path);
        if (!store.Entries.TryGetValue(LiteDbFileContentStore.Key(this.LocationName, normalized), out var file) || file.Directory) throw new LiteDbProviderException(new FileSystemError("File not found.", normalized));
        return file;
    }

    private void Directory(string path)
    {
        if (path.Length == 0) return;
        if (!store.Entries.TryGetValue(LiteDbFileContentStore.Key(this.LocationName, path), out var directory) || !directory.Directory) throw new LiteDbProviderException(new FileSystemError("Directory not found.", path));
    }

    private void Fence() => store.Fences[this.LocationName] = Guid.NewGuid();

    private static FileMetadata Metadata(LiteDbFileEntry file) => new() { Path = file.Path, Length = file.Length, LastModified = file.ModifiedUtc };

    private static void Apply(LiteDbFileEntry file, FileMetadata metadata)
    {
        if (metadata is null || metadata.Path is not null && LiteDbFileContentStore.Normalize(metadata.Path) != file.Path || metadata.Length != file.Length) throw new ArgumentException("Metadata cannot change a file path or its actual content length.");
        if (metadata.LastModified.HasValue) file.ModifiedUtc = metadata.LastModified.Value.ToUniversalTime();
    }

    private async Task<Result<T>> RunAsync<T>(Func<T> action, CancellationToken token)
    {
        try { return Result<T>.Success(await store.Database.ExecuteAsync(() => Task.FromResult(action()), token).ConfigureAwait(false)); }
        catch (Exception error) { return Result<T>.Failure().WithErrors(Failure(error, this.LocationName).Errors); }
    }

    private async Task<Result> RunAsync(Action action, CancellationToken token)
    {
        var result = await this.RunAsync(() => { action(); return true; }, token).ConfigureAwait(false);
        return result.IsSuccess ? Result.Success() : Result.Failure().WithErrors(result.Errors);
    }

    private static Result Failure(Exception error, string path) => error switch
    {
        OperationCanceledException => Result.Failure(new OperationCancelledError("File operation canceled.")),
        LiteDbProviderException storage => Result.Failure().WithErrors(storage.Errors),
        _ => Result.Failure(new FileSystemError(error is ArgumentException or FormatException or JsonException ? "Invalid virtual file operation." : "The native file operation failed.", path))
    };

    private sealed record FileCursor(string Selection, string After);
}
