// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Audits selected file-provider calls using only configured location and operation identity.</summary>
/// <param name="innerProvider">The decorated provider; stream and enumerable ownership remain unchanged.</param>
/// <param name="scopes">Resolves optional capture per call without retaining scoped services.</param>
/// <param name="options">Optional meaningful read and logical resource selection.</param>
/// <example><code>builder.UseInMemory("documents").WithAuditing();</code></example>
public sealed class AuditingFileStorageBehavior(IFileStorageProvider innerProvider, IServiceScopeFactory scopes, StorageAuditOptions options = null) : IFileStorageBehavior
{
    /// <inheritdoc />
    /// <example><code>var value = behavior.InnerProvider;</code></example>
    public IFileStorageProvider InnerProvider { get; } = innerProvider ?? throw new ArgumentNullException(nameof(innerProvider));

    /// <inheritdoc />
    /// <example><code>var value = behavior.LocationName;</code></example>
    public string LocationName => this.InnerProvider.LocationName;

    /// <inheritdoc />
    /// <example><code>var value = behavior.Description;</code></example>
    public string Description => this.InnerProvider.Description;

    /// <inheritdoc />
    /// <example><code>var value = behavior.SupportsNotifications;</code></example>
    public bool SupportsNotifications => this.InnerProvider.SupportsNotifications;

    /// <inheritdoc />
    /// <example><code>await files.FileExistsAsync("report.txt");</code></example>
    public Task<Result> FileExistsAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.AuditAsync("exists", () => this.InnerProvider.FileExistsAsync(path, progress, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.ReadFileAsync("report.txt");</code></example>
    public Task<Result<Stream>> ReadFileAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.AuditAsync(
            "read",
            () => this.InnerProvider.ReadFileAsync(path, progress, cancellationToken),
            cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.RenameDirectoryAsync("old", "new");</code></example>
    public Task<Result> RenameDirectoryAsync(string path, string destinationPath, CancellationToken cancellationToken = default) =>
        this.AuditAsync("rename-directory", () => this.InnerProvider.RenameDirectoryAsync(path, destinationPath, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.WriteFileAsync("report.txt", Stream.Null);</code></example>
    public Task<Result> WriteFileAsync(string path, Stream content, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.AuditAsync(
            "write",
            () => this.InnerProvider.WriteFileAsync(path, content, progress, cancellationToken),
            cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.OpenWriteFileAsync("report.txt");</code></example>
    public Task<Result<Stream>> OpenWriteFileAsync(string path, bool useTemporaryWrite = false, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.AuditAsync("open-write", () => this.InnerProvider.OpenWriteFileAsync(path, useTemporaryWrite, progress, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.DeleteFileAsync("report.txt");</code></example>
    public Task<Result> DeleteFileAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.AuditAsync("delete", () => this.InnerProvider.DeleteFileAsync(path, progress, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.GetChecksumAsync("report.txt");</code></example>
    public Task<Result<string>> GetChecksumAsync(string path, CancellationToken cancellationToken = default) =>
        this.AuditAsync("checksum", () => this.InnerProvider.GetChecksumAsync(path, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.GetFileMetadataAsync("report.txt");</code></example>
    public Task<Result<FileMetadata>> GetFileMetadataAsync(string path, CancellationToken cancellationToken = default) =>
        this.AuditAsync("metadata-get", () => this.InnerProvider.GetFileMetadataAsync(path, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.SetFileMetadataAsync("report.txt", new FileMetadata());</code></example>
    public Task<Result> SetFileMetadataAsync(string path, FileMetadata metadata, CancellationToken cancellationToken = default) =>
        this.AuditAsync("metadata-set", () => this.InnerProvider.SetFileMetadataAsync(path, metadata, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.UpdateFileMetadataAsync("report.txt", metadata =&gt; metadata);</code></example>
    public Task<Result<FileMetadata>> UpdateFileMetadataAsync(string path, Func<FileMetadata, FileMetadata> metadataUpdate, CancellationToken cancellationToken = default) =>
        this.AuditAsync("metadata-update", () => this.InnerProvider.UpdateFileMetadataAsync(path, metadataUpdate, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.ListFilesAsync("reports");</code></example>
    public Task<Result<(IEnumerable<string> Files, string NextContinuationToken)>> ListFilesAsync(
        string path,
        string searchPattern = null,
        bool recursive = false,
        string continuationToken = null,
        CancellationToken cancellationToken = default) =>
        this.AuditAsync(
            "list-files",
            () => this.InnerProvider.ListFilesAsync(path, searchPattern, recursive, continuationToken, cancellationToken),
            cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.CopyFileAsync("source.txt", "copy.txt");</code></example>
    public Task<Result> CopyFileAsync(string path, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.AuditAsync("copy", () => this.InnerProvider.CopyFileAsync(path, destinationPath, progress, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.RenameFileAsync("old.txt", "new.txt");</code></example>
    public Task<Result> RenameFileAsync(string path, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.AuditAsync("rename", () => this.InnerProvider.RenameFileAsync(path, destinationPath, progress, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.MoveFileAsync("source.txt", "archive/report.txt");</code></example>
    public Task<Result> MoveFileAsync(string path, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.AuditAsync("move", () => this.InnerProvider.MoveFileAsync(path, destinationPath, progress, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.CopyFilesAsync([("source.txt", "copy.txt")]);</code></example>
    public Task<Result> CopyFilesAsync(IEnumerable<(string SourcePath, string DestinationPath)> filePairs, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.AuditAsync("copy-batch", () => this.InnerProvider.CopyFilesAsync(filePairs, progress, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.MoveFilesAsync([("source.txt", "archive/report.txt")]);</code></example>
    public Task<Result> MoveFilesAsync(IEnumerable<(string SourcePath, string DestinationPath)> filePairs, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.AuditAsync("move-batch", () => this.InnerProvider.MoveFilesAsync(filePairs, progress, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.DeleteFilesAsync(["report.txt"]);</code></example>
    public Task<Result> DeleteFilesAsync(IEnumerable<string> paths, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.AuditAsync("delete-batch", () => this.InnerProvider.DeleteFilesAsync(paths, progress, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.DirectoryExistsAsync("reports");</code></example>
    public Task<Result> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default) =>
        this.AuditAsync("directory-exists", () => this.InnerProvider.DirectoryExistsAsync(path, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.CreateDirectoryAsync("reports");</code></example>
    public Task<Result> CreateDirectoryAsync(string path, CancellationToken cancellationToken = default) =>
        this.AuditAsync("directory-create", () => this.InnerProvider.CreateDirectoryAsync(path, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.DeleteDirectoryAsync("reports", recursive: true);</code></example>
    public Task<Result> DeleteDirectoryAsync(string path, bool recursive, CancellationToken cancellationToken = default) =>
        this.AuditAsync("directory-delete", () => this.InnerProvider.DeleteDirectoryAsync(path, recursive, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.ListDirectoriesAsync("reports");</code></example>
    public Task<Result<IEnumerable<string>>> ListDirectoriesAsync(string path, string searchPattern = null, bool recursive = false, CancellationToken cancellationToken = default) =>
        this.AuditAsync(
            "list-directories",
            () => this.InnerProvider.ListDirectoriesAsync(path, searchPattern, recursive, cancellationToken),
            cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.CheckHealthAsync();</code></example>
    public Task<Result> CheckHealthAsync(CancellationToken cancellationToken = default) =>
        this.AuditAsync("health", () => this.InnerProvider.CheckHealthAsync(cancellationToken), cancellationToken);

    private Task<TResult> AuditAsync<TResult>(string operation, Func<Task<TResult>> next, CancellationToken cancellationToken) where TResult : IResult
        => StorageAuditBehaviorExecutor.RunAsync(scopes, "FileStorage", operation, () => this.LocationName,
            operation is "exists" or "read" or "checksum" or "metadata-get" or "list-files" or "list-directories" or "directory-exists" or "health", options, next);
}
