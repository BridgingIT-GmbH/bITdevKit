// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System.Diagnostics;
using BridgingIT.DevKit.Common;

/// <summary>
/// Provides a base implementation of IFileStorageProvider with default Result-based methods,
/// translating exceptions into typed IResultError instances or ExceptionError for unhandled errors.
/// Intended for inheritance by concrete providers, ensuring testability and consistency.
/// </summary>
[DebuggerDisplay("LocationName={LocationName}")]
public abstract class BaseFileStorageProvider(string locationName) : IFileStorageProvider
{
    public string LocationName { get; } = locationName ?? throw new ArgumentNullException(nameof(locationName));

    public virtual bool SupportsNotifications { get; }

    public virtual Task<Result> ExistsAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Placeholder for concrete implementation
            throw new NotImplementedException("ExistsAsync must be implemented by concrete providers.");
        }
        catch (FileNotFoundException ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new FileSystemError("File not found", path, ex))
                .WithMessage($"Failed to check existence of file at '{path}'"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new PermissionError("Access denied", path, ex))
                .WithMessage($"Permission denied for file at '{path}'"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error checking file existence at '{path}'"));
        }
    }

    public virtual Task<Result<Stream>> ReadFileAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Placeholder for concrete implementation
            throw new NotImplementedException("ReadFileAsync must be implemented by concrete providers.");
        }
        catch (FileNotFoundException ex)
        {
            return Task.FromResult(Result<Stream>.Failure()
                .WithError(new FileSystemError("File not found", path, ex))
                .WithMessage($"Failed to read file at '{path}'"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Task.FromResult(Result<Stream>.Failure()
                .WithError(new PermissionError("Access denied", path, ex))
                .WithMessage($"Permission denied for file at '{path}'"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<Stream>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error reading file at '{path}'"));
        }
    }

    public virtual Task<Result> WriteFileAsync(string path, Stream content, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Placeholder for concrete implementation
            throw new NotImplementedException("WriteFileAsync must be implemented by concrete providers.");
        }
        catch (IOException ex) when (ex is DirectoryNotFoundException or DriveNotFoundException)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new FileSystemError("Directory or drive not found", path, ex))
                .WithMessage($"Failed to write file at '{path}' due to directory/drive issue"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new PermissionError("Access denied", path, ex))
                .WithMessage($"Permission denied for file at '{path}'"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error writing file at '{path}'"));
        }
    }

    public virtual Task<Result> DeleteFileAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Placeholder for concrete implementation
            throw new NotImplementedException("DeleteFileAsync must be implemented by concrete providers.");
        }
        catch (FileNotFoundException ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new FileSystemError("File not found", path, ex))
                .WithMessage($"Failed to delete file at '{path}'"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new PermissionError("Access denied", path, ex))
                .WithMessage($"Permission denied for file at '{path}'"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error deleting file at '{path}'"));
        }
    }

    public virtual Task<Result<string>> GetChecksumAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            // Placeholder for concrete implementation
            throw new NotImplementedException("GetChecksumAsync must be implemented by concrete providers.");
        }
        catch (FileNotFoundException ex)
        {
            return Task.FromResult(Result<string>.Failure()
                .WithError(new FileSystemError("File not found", path, ex))
                .WithMessage($"Failed to compute checksum for file at '{path}'"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Task.FromResult(Result<string>.Failure()
                .WithError(new PermissionError("Access denied", path, ex))
                .WithMessage($"Permission denied for file at '{path}'"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<string>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error computing checksum for file at '{path}'"));
        }
    }

    public virtual Task<Result<FileMetadata>> GetFileMetadataAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            // Placeholder for concrete implementation
            throw new NotImplementedException("GetFileInfoAsync must be implemented by concrete providers.");
        }
        catch (FileNotFoundException ex)
        {
            return Task.FromResult(Result<FileMetadata>.Failure()
                .WithError(new FileSystemError("File not found", path, ex))
                .WithMessage($"Failed to retrieve metadata for file at '{path}'"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Task.FromResult(Result<FileMetadata>.Failure()
                .WithError(new PermissionError("Access denied", path, ex))
                .WithMessage($"Permission denied for file at '{path}'"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<FileMetadata>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error retrieving metadata for file at '{path}'"));
        }
    }

    public virtual Task<Result> SetFileMetadataAsync(string path, FileMetadata metadata, CancellationToken cancellationToken = default)
    {
        try
        {
            // Placeholder for concrete implementation
            throw new NotImplementedException("SetFileMetadataAsync must be implemented by concrete providers.");
        }
        catch (FileNotFoundException ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new FileSystemError("File not found", path, ex))
                .WithMessage($"Failed to set metadata for file at '{path}'"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new PermissionError("Access denied", path, ex))
                .WithMessage($"Permission denied for file at '{path}'"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error setting metadata for file at '{path}'"));
        }
    }

    public virtual Task<Result<FileMetadata>> UpdateFileMetadataAsync(string path, Func<FileMetadata, FileMetadata> metadataUpdate, CancellationToken cancellationToken = default)
    {
        try
        {
            var currentMetadata = this.GetFileMetadataAsync(path, cancellationToken).Result;
            if (currentMetadata.IsFailure)
            {
                return Task.FromResult(Result<FileMetadata>.Failure()
                    .WithErrors(currentMetadata.Errors)
                    .WithMessages(currentMetadata.Messages));
            }

            var updatedMetadata = metadataUpdate(currentMetadata.Value);
            var setResult = this.SetFileMetadataAsync(path, updatedMetadata, cancellationToken).Result;
            if (setResult.IsFailure)
            {
                return Task.FromResult(Result<FileMetadata>.Failure()
                    .WithErrors(setResult.Errors)
                    .WithMessages(setResult.Messages));
            }

            return Task.FromResult(Result<FileMetadata>.Success(updatedMetadata)
                .WithMessage($"Updated metadata for file at '{path}'"));
        }
        catch (FileNotFoundException ex)
        {
            return Task.FromResult(Result<FileMetadata>.Failure()
                .WithError(new FileSystemError("File not found", path, ex))
                .WithMessage($"Failed to update metadata for file at '{path}'"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Task.FromResult(Result<FileMetadata>.Failure()
                .WithError(new PermissionError("Access denied", path, ex))
                .WithMessage($"Permission denied for file at '{path}'"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<FileMetadata>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error updating metadata for file at '{path}'"));
        }
    }

    public virtual Task<Result<(IEnumerable<string> Files, string NextContinuationToken)>> ListFilesAsync(
        string path, string searchPattern, bool recursive, string continuationToken = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Placeholder for concrete implementation
            throw new NotImplementedException("ListFilesAsync must be implemented by concrete providers.");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Task.FromResult(Result<(IEnumerable<string> Files, string NextContinuationToken)>.Failure()
                .WithError(new PermissionError("Access denied", path, ex))
                .WithMessage($"Failed to list files in '{path}'"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<(IEnumerable<string> Files, string NextContinuationToken)>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error listing files in '{path}'"));
        }
    }

    public virtual Task<Result> CopyFileAsync(string path, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Placeholder for concrete implementation
            throw new NotImplementedException("CopyFileAsync must be implemented by concrete providers.");
        }
        catch (FileNotFoundException ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new FileSystemError("Source file not found", path, ex))
                .WithMessage($"Failed to copy file from '{path}' to '{destinationPath}'"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new PermissionError("Access denied", path, ex))
                .WithMessage($"Permission denied for file at '{path}' or '{destinationPath}'"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error copying file from '{path}' to '{destinationPath}'"));
        }
    }

    public virtual Task<Result> RenameFileAsync(string path, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Placeholder for concrete implementation
            throw new NotImplementedException("RenameFileAsync must be implemented by concrete providers.");
        }
        catch (FileNotFoundException ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new FileSystemError("File not found", path, ex))
                .WithMessage($"Failed to rename file from '{path}' to '{destinationPath}'"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new PermissionError("Access denied", path, ex))
                .WithMessage($"Permission denied for file at '{path}' or '{destinationPath}'"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error renaming file from '{path}' to '{destinationPath}'"));
        }
    }

    public virtual Task<Result> MoveFileAsync(string path, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Placeholder for concrete implementation
            throw new NotImplementedException("MoveFileAsync must be implemented by concrete providers.");
        }
        catch (FileNotFoundException ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new FileSystemError("Source file not found", path, ex))
                .WithMessage($"Failed to move file from '{path}' to '{destinationPath}'"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new PermissionError("Access denied", path, ex))
                .WithMessage($"Permission denied for file at '{path}' or '{destinationPath}'"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error moving file from '{path}' to '{destinationPath}'"));
        }
    }

    public virtual async Task<Result> CopyFilesAsync(IEnumerable<(string SourcePath, string DestinationPath)> filePairs, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        if (filePairs?.Any() != true)
        {
            return Result.Failure()
                .WithError(new FileSystemError("No file pairs provided", ""))
                .WithMessage("Invalid file pairs for copy operation");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage("Cancelled copying multiple files");
        }

        try
        {
            var failedPaths = new List<string>();
            long totalBytes = 0;
            long filesProcessed = 0;
            var totalFiles = filePairs.Count();

            foreach (var (source, dest) in filePairs)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return Result.Failure()
                        .WithError(new OperationCancelledError("Operation cancelled during batch copy"))
                        .WithMessage($"Cancelled copying files after processing {filesProcessed}/{totalFiles}");
                }

                var result = await this.CopyFileAsync(source, dest, progress, cancellationToken);
                if (result.IsFailure)
                {
                    failedPaths.Add(source);
                    continue;
                }

                filesProcessed++;
                var metadata = await this.GetFileMetadataAsync(source, cancellationToken);
                totalBytes += metadata.Value.Length;
                this.ReportProgress(progress, source, totalBytes, filesProcessed, totalFiles);
            }

            if (failedPaths.Count != 0)
            {
                return Result.Failure()
                    .WithError(new PartialOperationError("Partial copy failure", failedPaths))
                    .WithMessage($"Copied {filesProcessed}/{totalFiles} files, {failedPaths.Count} failed");
            }

            return Result.Success()
                .WithMessage($"Copied all {totalFiles} files");
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage("Unexpected error copying multiple files");
        }
    }

    public virtual async Task<Result> DeleteFilesAsync(IEnumerable<string> paths, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        if (paths?.Any() != true)
        {
            return Result.Failure()
                .WithError(new FileSystemError("No paths provided", ""))
                .WithMessage("Invalid paths for delete operation");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage("Cancelled deleting multiple files");
        }

        try
        {
            var failedPaths = new List<string>();
            long totalBytes = 0;
            long filesProcessed = 0;
            var totalFiles = paths.Count();

            foreach (var path in paths)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return Result.Failure()
                        .WithError(new OperationCancelledError("Operation cancelled during batch delete"))
                        .WithMessage($"Cancelled deleting files after processing {filesProcessed}/{totalFiles}");
                }

                var metadata = await this.GetFileMetadataAsync(path, cancellationToken);
                var result = await this.DeleteFileAsync(path, progress, cancellationToken);
                if (result.IsFailure)
                {
                    failedPaths.Add(path);
                    continue;
                }

                filesProcessed++;
                totalBytes += metadata.Value.Length;
                this.ReportProgress(progress, path, totalBytes, filesProcessed, totalFiles);
            }

            if (failedPaths.Count != 0)
            {
                return Result.Failure()
                    .WithError(new PartialOperationError("Partial delete failure", failedPaths))
                    .WithMessage($"Deleted {filesProcessed}/{totalFiles} files, {failedPaths.Count} failed");
            }

            return Result.Success()
                .WithMessage($"Deleted all {totalFiles} files");
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage("Unexpected error deleting multiple files");
        }
    }

    public virtual async Task<Result> MoveFilesAsync(IEnumerable<(string SourcePath, string DestinationPath)> filePairs, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        if (filePairs?.Any() != true)
        {
            return Result.Failure()
                .WithError(new FileSystemError("No file pairs provided", ""))
                .WithMessage("Invalid file pairs for move operation");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage("Cancelled moving multiple files");
        }

        try
        {
            var failedPaths = new List<string>();
            long totalBytes = 0;
            long filesProcessed = 0;
            var totalFiles = filePairs.Count();

            foreach (var (source, dest) in filePairs)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return Result.Failure()
                        .WithError(new OperationCancelledError("Operation cancelled during batch move"))
                        .WithMessage($"Cancelled moving files after processing {filesProcessed}/{totalFiles}");
                }

                var result = await this.MoveFileAsync(source, dest, progress, cancellationToken);
                if (result.IsFailure)
                {
                    failedPaths.Add(source);
                    continue;
                }

                filesProcessed++;
                var metadata = await this.GetFileMetadataAsync(dest, cancellationToken);
                totalBytes += metadata.Value.Length;
                this.ReportProgress(progress, source, totalBytes, filesProcessed, totalFiles);
            }

            if (failedPaths.Count != 0)
            {
                return Result.Failure()
                    .WithError(new PartialOperationError("Partial move failure", failedPaths))
                    .WithMessage($"Moved {filesProcessed}/{totalFiles} files, {failedPaths.Count} failed");
            }

            return Result.Success()
                .WithMessage($"Moved all {totalFiles} files");
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage("Unexpected error moving multiple files");
        }
    }

    public virtual Task<Result> IsDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            throw new NotImplementedException("IsDirectoryAsync must be implemented by concrete providers.");
        }
        catch (DirectoryNotFoundException ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new FileSystemError("Directory not found", path, ex))
                .WithMessage($"Failed to check if '{path}' is a directory"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new PermissionError("Access denied", path, ex))
                .WithMessage($"Permission denied for path '{path}'"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error checking directory at '{path}'"));
        }
    }

    public virtual Task<Result> CreateDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            // Placeholder for concrete implementation
            throw new NotImplementedException("CreateDirectoryAsync must be implemented by concrete providers.");
        }
        catch (IOException ex) when (ex is DirectoryNotFoundException or DriveNotFoundException)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new FileSystemError("Parent directory or drive not found", path, ex))
                .WithMessage($"Failed to create directory at '{path}' due to directory/drive issue"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new PermissionError("Access denied", path, ex))
                .WithMessage($"Permission denied for path '{path}'"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error creating directory at '{path}'"));
        }
    }

    public virtual Task<Result> DeleteDirectoryAsync(string path, bool recursive, CancellationToken cancellationToken = default)
    {
        try
        {
            // Placeholder for concrete implementation
            throw new NotImplementedException("DeleteDirectoryAsync must be implemented by concrete providers.");
        }
        catch (DirectoryNotFoundException ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new FileSystemError("Directory not found", path, ex))
                .WithMessage($"Failed to delete directory at '{path}'"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new PermissionError("Access denied", path, ex))
                .WithMessage($"Permission denied for path '{path}'"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error deleting directory at '{path}'"));
        }
    }

    public virtual Task<Result<IEnumerable<string>>> ListDirectoriesAsync(
        string path, string searchPattern, bool recursive, CancellationToken cancellationToken = default)
    {
        try
        {
            // Placeholder for concrete implementation
            throw new NotImplementedException("ListDirectoriesAsync must be implemented by concrete providers.");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Task.FromResult(Result<IEnumerable<string>>.Failure()
                .WithError(new PermissionError("Access denied", path, ex))
                .WithMessage($"Failed to list directories in '{path}'"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<IEnumerable<string>>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error listing directories in '{path}'"));
        }
    }

    public virtual Task<Result> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Placeholder for concrete implementation
            throw new NotImplementedException("CheckHealthAsync must be implemented by concrete providers.");
        }
        catch (IOException ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new FileSystemError("Storage connectivity issue", this.LocationName, ex))
                .WithMessage($"Failed to check health of storage at '{this.LocationName}'"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new PermissionError("Access denied", this.LocationName, ex))
                .WithMessage($"Permission denied for storage at '{this.LocationName}'"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error checking health of storage at '{this.LocationName}'"));
        }
    }

    public virtual void ReportProgress(IProgress<FileProgress> progress, string path, long bytesProcessed, long filesProcessed, long totalFiles = 1)
    {
        progress?.Report(new FileProgress
        {
            BytesProcessed = bytesProcessed,
            FilesProcessed = filesProcessed,
            TotalFiles = totalFiles
        });
    }
}