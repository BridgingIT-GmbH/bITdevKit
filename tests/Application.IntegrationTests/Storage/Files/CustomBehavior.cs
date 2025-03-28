// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Storage;

using BridgingIT.DevKit.Application.Storage;

public class CustomBehavior(IFileStorageProvider innerProvider) : IFileStorageBehavior
{
    private readonly IFileStorageProvider innerProvider = innerProvider ?? throw new ArgumentNullException(nameof(innerProvider));

    public string LocationName => this.InnerProvider.LocationName;

    public string Description => this.InnerProvider.Description;

    public bool SupportsNotifications => this.InnerProvider.SupportsNotifications;

    public IFileStorageProvider InnerProvider => this.innerProvider;

    public Task<Result> FileExistsAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.innerProvider.FileExistsAsync(path, progress, cancellationToken).ContinueWith(t => t.Result.WithMessage($"Custom behavior applied to existence check for '{path}'"), cancellationToken);

    public Task<Result<Stream>> ReadFileAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.innerProvider.ReadFileAsync(path, progress, cancellationToken);

    public Task<Result> WriteFileAsync(string path, Stream content, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.innerProvider.WriteFileAsync(path, content, progress, cancellationToken);

    public Task<Result> DeleteFileAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.innerProvider.DeleteFileAsync(path, progress, cancellationToken);

    public Task<Result<string>> GetChecksumAsync(string path, CancellationToken cancellationToken = default) =>
        this.innerProvider.GetChecksumAsync(path, cancellationToken);

    public Task<Result<FileMetadata>> GetFileMetadataAsync(string path, CancellationToken cancellationToken = default) =>
        this.innerProvider.GetFileMetadataAsync(path, cancellationToken);

    public Task<Result> SetFileMetadataAsync(string path, FileMetadata metadata, CancellationToken cancellationToken = default) =>
        this.innerProvider.SetFileMetadataAsync(path, metadata, cancellationToken);

    public Task<Result<FileMetadata>> UpdateFileMetadataAsync(string path, Func<FileMetadata, FileMetadata> metadataUpdate, CancellationToken cancellationToken = default) =>
        this.innerProvider.UpdateFileMetadataAsync(path, metadataUpdate, cancellationToken);

    public Task<Result<(IEnumerable<string> Files, string NextContinuationToken)>> ListFilesAsync(string path, string searchPattern, bool recursive, string continuationToken = null, CancellationToken cancellationToken = default) =>
        this.innerProvider.ListFilesAsync(path, searchPattern, recursive, continuationToken, cancellationToken);

    public Task<Result> CopyFileAsync(string sourcePath, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.innerProvider.CopyFileAsync(sourcePath, destinationPath, progress, cancellationToken);

    public Task<Result> RenameFileAsync(string oldPath, string newPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.innerProvider.RenameFileAsync(oldPath, newPath, progress, cancellationToken);

    public Task<Result> MoveFileAsync(string sourcePath, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.innerProvider.MoveFileAsync(sourcePath, destinationPath, progress, cancellationToken);

    public Task<Result> CopyFilesAsync(IEnumerable<(string SourcePath, string DestinationPath)> filePairs, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.innerProvider.CopyFilesAsync(filePairs, progress, cancellationToken);

    public Task<Result> MoveFilesAsync(IEnumerable<(string SourcePath, string DestinationPath)> filePairs, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.innerProvider.MoveFilesAsync(filePairs, progress, cancellationToken);

    public Task<Result> DeleteFilesAsync(IEnumerable<string> paths, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.innerProvider.DeleteFilesAsync(paths, progress, cancellationToken);

    public Task<Result> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default) =>
        this.innerProvider.DirectoryExistsAsync(path, cancellationToken);

    public Task<Result> CreateDirectoryAsync(string path, CancellationToken cancellationToken = default) =>
        this.innerProvider.CreateDirectoryAsync(path, cancellationToken);

    public Task<Result> DeleteDirectoryAsync(string path, bool recursive, CancellationToken cancellationToken = default) =>
        this.innerProvider.DeleteDirectoryAsync(path, recursive, cancellationToken);

    public Task<Result<IEnumerable<string>>> ListDirectoriesAsync(string path, string searchPattern, bool recursive, CancellationToken cancellationToken = default) =>
        this.innerProvider.ListDirectoriesAsync(path, searchPattern, recursive, cancellationToken);

    public Task<Result> CheckHealthAsync(CancellationToken cancellationToken = default) =>
        this.innerProvider.CheckHealthAsync(cancellationToken);
}
