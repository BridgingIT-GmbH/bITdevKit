// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using BridgingIT.DevKit.Common;

/// <summary>Profiles file-provider calls using logical location and operation dimensions.</summary>
/// <param name="innerProvider">The decorated provider; stream and enumerable ownership remain unchanged.</param>
/// <param name="profiling">The optional shared operation profiler.</param>
/// <example><code>builder.UseInMemory("documents").WithProfiling();</code></example>
public sealed class ProfilingFileStorageBehavior(IFileStorageProvider innerProvider, IOperationProfiler profiling = null) : IFileStorageBehavior
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
        this.ProfileAsync("exists", () => this.InnerProvider.FileExistsAsync(path, progress, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.ReadFileAsync("report.txt");</code></example>
    public Task<Result<Stream>> ReadFileAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.ProfileAsync(
            "read",
            () => this.InnerProvider.ReadFileAsync(path, progress, cancellationToken),
            cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.RenameDirectoryAsync("old", "new");</code></example>
    public Task<Result> RenameDirectoryAsync(string path, string destinationPath, CancellationToken cancellationToken = default) =>
        this.ProfileAsync("rename-directory", () => this.InnerProvider.RenameDirectoryAsync(path, destinationPath, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.WriteFileAsync("report.txt", Stream.Null);</code></example>
    public Task<Result> WriteFileAsync(string path, Stream content, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.ProfileAsync(
            "write",
            () => this.InnerProvider.WriteFileAsync(path, content, progress, cancellationToken),
            cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.OpenWriteFileAsync("report.txt");</code></example>
    public Task<Result<Stream>> OpenWriteFileAsync(string path, bool useTemporaryWrite = false, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.ProfileAsync("open-write", () => this.InnerProvider.OpenWriteFileAsync(path, useTemporaryWrite, progress, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.DeleteFileAsync("report.txt");</code></example>
    public Task<Result> DeleteFileAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.ProfileAsync("delete", () => this.InnerProvider.DeleteFileAsync(path, progress, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.GetChecksumAsync("report.txt");</code></example>
    public Task<Result<string>> GetChecksumAsync(string path, CancellationToken cancellationToken = default) =>
        this.ProfileAsync("checksum", () => this.InnerProvider.GetChecksumAsync(path, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.GetFileMetadataAsync("report.txt");</code></example>
    public Task<Result<FileMetadata>> GetFileMetadataAsync(string path, CancellationToken cancellationToken = default) =>
        this.ProfileAsync("metadata-get", () => this.InnerProvider.GetFileMetadataAsync(path, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.SetFileMetadataAsync("report.txt", new FileMetadata());</code></example>
    public Task<Result> SetFileMetadataAsync(string path, FileMetadata metadata, CancellationToken cancellationToken = default) =>
        this.ProfileAsync("metadata-set", () => this.InnerProvider.SetFileMetadataAsync(path, metadata, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.UpdateFileMetadataAsync("report.txt", metadata =&gt; metadata);</code></example>
    public Task<Result<FileMetadata>> UpdateFileMetadataAsync(string path, Func<FileMetadata, FileMetadata> metadataUpdate, CancellationToken cancellationToken = default) =>
        this.ProfileAsync("metadata-update", () => this.InnerProvider.UpdateFileMetadataAsync(path, metadataUpdate, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.ListFilesAsync("reports");</code></example>
    public Task<Result<(IEnumerable<string> Files, string NextContinuationToken)>> ListFilesAsync(
        string path,
        string searchPattern = null,
        bool recursive = false,
        string continuationToken = null,
        CancellationToken cancellationToken = default) =>
        this.ProfileAsync(
            "list-files",
            () => this.InnerProvider.ListFilesAsync(path, searchPattern, recursive, continuationToken, cancellationToken),
            cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.CopyFileAsync("source.txt", "copy.txt");</code></example>
    public Task<Result> CopyFileAsync(string path, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.ProfileAsync("copy", () => this.InnerProvider.CopyFileAsync(path, destinationPath, progress, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.RenameFileAsync("old.txt", "new.txt");</code></example>
    public Task<Result> RenameFileAsync(string path, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.ProfileAsync("rename", () => this.InnerProvider.RenameFileAsync(path, destinationPath, progress, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.MoveFileAsync("source.txt", "archive/report.txt");</code></example>
    public Task<Result> MoveFileAsync(string path, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.ProfileAsync("move", () => this.InnerProvider.MoveFileAsync(path, destinationPath, progress, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.CopyFilesAsync([("source.txt", "copy.txt")]);</code></example>
    public Task<Result> CopyFilesAsync(IEnumerable<(string SourcePath, string DestinationPath)> filePairs, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.ProfileAsync("copy-batch", () => this.InnerProvider.CopyFilesAsync(filePairs, progress, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.MoveFilesAsync([("source.txt", "archive/report.txt")]);</code></example>
    public Task<Result> MoveFilesAsync(IEnumerable<(string SourcePath, string DestinationPath)> filePairs, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.ProfileAsync("move-batch", () => this.InnerProvider.MoveFilesAsync(filePairs, progress, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.DeleteFilesAsync(["report.txt"]);</code></example>
    public Task<Result> DeleteFilesAsync(IEnumerable<string> paths, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.ProfileAsync("delete-batch", () => this.InnerProvider.DeleteFilesAsync(paths, progress, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.DirectoryExistsAsync("reports");</code></example>
    public Task<Result> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default) =>
        this.ProfileAsync("directory-exists", () => this.InnerProvider.DirectoryExistsAsync(path, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.CreateDirectoryAsync("reports");</code></example>
    public Task<Result> CreateDirectoryAsync(string path, CancellationToken cancellationToken = default) =>
        this.ProfileAsync("directory-create", () => this.InnerProvider.CreateDirectoryAsync(path, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.DeleteDirectoryAsync("reports", recursive: true);</code></example>
    public Task<Result> DeleteDirectoryAsync(string path, bool recursive, CancellationToken cancellationToken = default) =>
        this.ProfileAsync("directory-delete", () => this.InnerProvider.DeleteDirectoryAsync(path, recursive, cancellationToken), cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.ListDirectoriesAsync("reports");</code></example>
    public Task<Result<IEnumerable<string>>> ListDirectoriesAsync(string path, string searchPattern = null, bool recursive = false, CancellationToken cancellationToken = default) =>
        this.ProfileAsync(
            "list-directories",
            () => this.InnerProvider.ListDirectoriesAsync(path, searchPattern, recursive, cancellationToken),
            cancellationToken);

    /// <inheritdoc />
    /// <example><code>await files.CheckHealthAsync();</code></example>
    public Task<Result> CheckHealthAsync(CancellationToken cancellationToken = default) =>
        this.ProfileAsync("health", () => this.InnerProvider.CheckHealthAsync(cancellationToken), cancellationToken);

    private Task<TResult> ProfileAsync<TResult>(string operation, Func<Task<TResult>> next, CancellationToken cancellationToken)
        where TResult : IResult
    {
        if (profiling is null) { return next(); }

        return profiling.JoinOrStartAsync(new OperationProfilingStartRequest
        {
            Key = "filestorage:" + operation, Kind = "FileStorage", CorrelationId = CorrelationId.Current,
        }, (scope, _) =>
        {
            try
            {
                if (scope.IsRecording)
                {
                    scope.SetDimension("filestorage.location", this.LocationName);
                    scope.SetDimension("filestorage.operation", operation);
                }
            }
            catch (Exception) { /* Profiling metadata cannot change provider execution. */ }

            return next();
        }, cancellationToken, result => OperationProfilingHelpers.ClassifyResult(result));
    }
}
