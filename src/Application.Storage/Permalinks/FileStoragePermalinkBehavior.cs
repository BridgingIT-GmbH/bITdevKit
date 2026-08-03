// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Adds asynchronous permalink tracking to one configured File Storage provider.
/// </summary>
/// <example>
/// <code>
/// builder.UseLocal("Files", rootPath).WithPermalinks();
/// </code>
/// </example>
public sealed class FileStoragePermalinkBehavior(
    IFileStorageProvider innerProvider,
    string providerName,
    IStoragePermalinkRegistry registry,
    IStoragePermalinkChangeQueue queue) : IFileStorageBehavior, IStoragePermalinkAccessor
{
    /// <inheritdoc />
    public IFileStorageProvider InnerProvider { get; } = innerProvider ?? throw new ArgumentNullException(nameof(innerProvider));

    /// <inheritdoc />
    public string RegistrationName { get; } = string.IsNullOrWhiteSpace(providerName) ? "default" : providerName.Trim().ToLowerInvariant();

    /// <inheritdoc />
    public StorageResourceKind ResourceKind => StorageResourceKind.File;

    /// <inheritdoc />
    public string LocationName => this.InnerProvider.LocationName;

    /// <inheritdoc />
    public string Description => this.InnerProvider.Description;

    /// <inheritdoc />
    public bool SupportsNotifications => this.InnerProvider.SupportsNotifications;

    /// <inheritdoc />
    public async Task<Result<StoragePermalinkEntry>> GetPermalinkAsync(StorageResourceLocation location, StoragePermalinkCreateOptions options = null, CancellationToken cancellationToken = default)
    {
        var exists = await this.InnerProvider.FileExistsAsync(location.Path, cancellationToken: cancellationToken).ConfigureAwait(false);
        return exists.IsFailure
            ? Result<StoragePermalinkEntry>.Failure(exists)
            : await registry.GetOrCreateAsync(location, options, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<Result> FileExistsAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) => this.InnerProvider.FileExistsAsync(path, progress, cancellationToken);
    /// <inheritdoc />
    public Task<Result<Stream>> ReadFileAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) => this.InnerProvider.ReadFileAsync(path, progress, cancellationToken);

    /// <inheritdoc />
    public async Task<Result> RenameDirectoryAsync(string path, string destinationPath, CancellationToken cancellationToken = default)
    {
        var result = await this.InnerProvider.RenameDirectoryAsync(path, destinationPath, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess) await this.EnqueueMoveAsync(path, destinationPath, prefix: true).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc />
    public async Task<Result> WriteFileAsync(string path, Stream content, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        var result = await this.InnerProvider.WriteFileAsync(path, content, progress, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess) await this.EnqueueAsync(StorageResourceChangeKind.Upserted, path).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc />
    public async Task<Result<Stream>> OpenWriteFileAsync(string path, bool useTemporaryWrite = false, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        var result = await this.InnerProvider.OpenWriteFileAsync(path, useTemporaryWrite, progress, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure) return result;
        return Result<Stream>.Success(new OpenWriteFileStream(result.Value, progress, cancellationToken, () => this.EnqueueAsync(StorageResourceChangeKind.Upserted, path).AsTask()));
    }

    /// <inheritdoc />
    public async Task<Result> DeleteFileAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        var result = await this.InnerProvider.DeleteFileAsync(path, progress, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess) await this.EnqueueAsync(StorageResourceChangeKind.Deleted, path).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc />
    public Task<Result<string>> GetChecksumAsync(string path, CancellationToken cancellationToken = default) => this.InnerProvider.GetChecksumAsync(path, cancellationToken);
    /// <inheritdoc />
    public Task<Result<FileMetadata>> GetFileMetadataAsync(string path, CancellationToken cancellationToken = default) => this.InnerProvider.GetFileMetadataAsync(path, cancellationToken);
    /// <inheritdoc />
    public Task<Result> SetFileMetadataAsync(string path, FileMetadata metadata, CancellationToken cancellationToken = default) => this.InnerProvider.SetFileMetadataAsync(path, metadata, cancellationToken);
    /// <inheritdoc />
    public Task<Result<FileMetadata>> UpdateFileMetadataAsync(string path, Func<FileMetadata, FileMetadata> metadataUpdate, CancellationToken cancellationToken = default) => this.InnerProvider.UpdateFileMetadataAsync(path, metadataUpdate, cancellationToken);
    /// <inheritdoc />
    public Task<Result<(IEnumerable<string> Files, string NextContinuationToken)>> ListFilesAsync(string path, string searchPattern = null, bool recursive = false, string continuationToken = null, CancellationToken cancellationToken = default) => this.InnerProvider.ListFilesAsync(path, searchPattern, recursive, continuationToken, cancellationToken);

    /// <inheritdoc />
    public async Task<Result> CopyFileAsync(string path, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        var result = await this.InnerProvider.CopyFileAsync(path, destinationPath, progress, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess) await this.EnqueueAsync(StorageResourceChangeKind.Upserted, destinationPath).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc />
    public async Task<Result> RenameFileAsync(string path, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        var result = await this.InnerProvider.RenameFileAsync(path, destinationPath, progress, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess) await this.EnqueueMoveAsync(path, destinationPath).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc />
    public async Task<Result> MoveFileAsync(string path, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        var result = await this.InnerProvider.MoveFileAsync(path, destinationPath, progress, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess) await this.EnqueueMoveAsync(path, destinationPath).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc />
    public async Task<Result> CopyFilesAsync(IEnumerable<(string SourcePath, string DestinationPath)> filePairs, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        var pairs = filePairs?.ToArray() ?? [];
        var result = await this.InnerProvider.CopyFilesAsync(pairs, progress, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess) foreach (var pair in pairs) await this.EnqueueAsync(StorageResourceChangeKind.Upserted, pair.DestinationPath).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc />
    public async Task<Result> MoveFilesAsync(IEnumerable<(string SourcePath, string DestinationPath)> filePairs, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        var pairs = filePairs?.ToArray() ?? [];
        var result = await this.InnerProvider.MoveFilesAsync(pairs, progress, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess) foreach (var pair in pairs) await this.EnqueueMoveAsync(pair.SourcePath, pair.DestinationPath).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc />
    public async Task<Result> DeleteFilesAsync(IEnumerable<string> paths, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        var materialized = paths?.ToArray() ?? [];
        var result = await this.InnerProvider.DeleteFilesAsync(materialized, progress, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess) foreach (var path in materialized) await this.EnqueueAsync(StorageResourceChangeKind.Deleted, path).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc />
    public Task<Result> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default) => this.InnerProvider.DirectoryExistsAsync(path, cancellationToken);
    /// <inheritdoc />
    public Task<Result> CreateDirectoryAsync(string path, CancellationToken cancellationToken = default) => this.InnerProvider.CreateDirectoryAsync(path, cancellationToken);

    /// <inheritdoc />
    public async Task<Result> DeleteDirectoryAsync(string path, bool recursive, CancellationToken cancellationToken = default)
    {
        var result = await this.InnerProvider.DeleteDirectoryAsync(path, recursive, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess && recursive) await this.EnqueueAsync(StorageResourceChangeKind.PrefixDeleted, path).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc />
    public Task<Result<IEnumerable<string>>> ListDirectoriesAsync(string path, string searchPattern = null, bool recursive = false, CancellationToken cancellationToken = default) => this.InnerProvider.ListDirectoriesAsync(path, searchPattern, recursive, cancellationToken);
    /// <inheritdoc />
    public Task<Result> CheckHealthAsync(CancellationToken cancellationToken = default) => this.InnerProvider.CheckHealthAsync(cancellationToken);

    private ValueTask<bool> EnqueueAsync(StorageResourceChangeKind kind, string path) => queue.EnqueueAsync(new(kind, StorageResourceLocation.ForFile(this.RegistrationName, path)));
    private ValueTask<bool> EnqueueMoveAsync(string path, string destinationPath, bool prefix = false) => queue.EnqueueAsync(new(prefix ? StorageResourceChangeKind.PrefixMoved : StorageResourceChangeKind.Moved, StorageResourceLocation.ForFile(this.RegistrationName, path), StorageResourceLocation.ForFile(this.RegistrationName, destinationPath)));
}
