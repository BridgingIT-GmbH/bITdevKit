// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Storage;

using BridgingIT.DevKit.Application.Storage;

public class CustomFileStorageProvider : IFileStorageProvider
{
    public string LocationName => "CustomStorage";

    public string Description => "CustomStorage";

    public bool SupportsNotifications => false;

    public Task<Result> FileExistsAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Failure().WithError(new FileSystemError("Not implemented")));
    }

    public Task<Result<Stream>> ReadFileAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result<Stream>.Failure().WithError(new FileSystemError("Not implemented")));
    }

    public Task<Result> WriteFileAsync(string path, Stream content, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Failure().WithError(new FileSystemError("Not implemented")));
    }

    public Task<Result> DeleteFileAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Failure().WithError(new FileSystemError("Not implemented")));
    }

    public Task<Result<string>> GetChecksumAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result<string>.Failure().WithError(new FileSystemError("Not implemented")));
    }

    public Task<Result<FileMetadata>> GetFileMetadataAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result<FileMetadata>.Failure().WithError(new FileSystemError("Not implemented")));
    }

    public Task<Result> SetFileMetadataAsync(string path, FileMetadata metadata, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Failure().WithError(new FileSystemError("Not implemented")));
    }

    public Task<Result<FileMetadata>> UpdateFileMetadataAsync(string path, Func<FileMetadata, FileMetadata> metadataUpdate, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result<FileMetadata>.Failure().WithError(new FileSystemError("Not implemented")));
    }

    public Task<Result<(IEnumerable<string> Files, string NextContinuationToken)>> ListFilesAsync(string path, string searchPattern, bool recursive, string continuationToken = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result<(IEnumerable<string> Files, string NextContinuationToken)>.Failure().WithError(new FileSystemError("Not implemented")));
    }

    public Task<Result> CopyFileAsync(string sourcePath, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Failure().WithError(new FileSystemError("Not implemented")));
    }

    public Task<Result> RenameFileAsync(string oldPath, string newPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Failure().WithError(new FileSystemError("Not implemented")));
    }

    public Task<Result> MoveFileAsync(string sourcePath, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Failure().WithError(new FileSystemError("Not implemented")));
    }

    public Task<Result> CopyFilesAsync(IEnumerable<(string SourcePath, string DestinationPath)> filePairs, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Failure().WithError(new FileSystemError("Not implemented")));
    }

    public Task<Result> MoveFilesAsync(IEnumerable<(string SourcePath, string DestinationPath)> filePairs, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Failure().WithError(new FileSystemError("Not implemented")));
    }

    public Task<Result> DeleteFilesAsync(IEnumerable<string> paths, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Failure().WithError(new FileSystemError("Not implemented")));
    }

    public Task<Result> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Failure().WithError(new FileSystemError("Not implemented")));
    }

    public Task<Result> CreateDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Failure().WithError(new FileSystemError("Not implemented")));
    }

    public Task<Result> DeleteDirectoryAsync(string path, bool recursive, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Failure().WithError(new FileSystemError("Not implemented")));
    }

    public Task<Result<IEnumerable<string>>> ListDirectoriesAsync(string path, string searchPattern, bool recursive, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result<IEnumerable<string>>.Failure().WithError(new FileSystemError("Not implemented")));
    }

    public Task<Result> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Failure().WithError(new FileSystemError("Not implemented")));
    }
}
