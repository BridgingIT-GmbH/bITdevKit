// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System;
using System.Security.Cryptography;
using BridgingIT.DevKit.Common;

/// <summary>
/// An in-memory implementation of IFileStorageProvider for testing or ephemeral storage, supporting both files and directories.
/// Maintains a dictionary of files and a hash set of directories, ensuring thread-safe operations and hierarchical structure.
/// </summary>
public class InMemoryFileStorageProvider(string locationName = "InMemory")
    : BaseFileStorageProvider(locationName)
{
    private readonly Dictionary<string, byte[]> files = [];
    private readonly HashSet<string> directories = [];
    private readonly SemaphoreSlim semaphore = new(1, 1);

    public override async Task<Result> ExistsAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(path))
        {
            return Result.Failure()
                .WithError(new FileSystemError("Path cannot be null or empty", path))
                .WithMessage("Invalid path provided");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled checking existence of '{path}'");
        }

        var normalizedPath = this.NormalizePath(path);
        return await Task.Run(() =>
        {
            if (!this.semaphore.Wait(0, cancellationToken)) // Non-blocking check, fail if locked
            {
                return Result.Failure()
                    .WithError(new ExceptionError(new TimeoutException("Operation timed out due to concurrent access")))
                    .WithMessage($"Failed to acquire lock for checking existence of '{path}'");
            }

            try
            {
                var exists = this.files.ContainsKey(normalizedPath) || this.directories.Contains(normalizedPath); 
                if (!exists)
                {
                    return Result.Failure()
                        .WithError(new NotFoundError("File not found"));
                }

                return Result.Success()
                    .WithMessage($"Checked existence of file at '{path}'");
            }
            catch (OperationCanceledException)
            {
                return Result.Failure()
                    .WithError(new OperationCancelledError("Operation cancelled during existence check"))
                    .WithMessage($"Cancelled checking existence of '{path}'");
            }
            catch (Exception ex)
            {
                return Result.Failure()
                    .WithError(new ExceptionError(ex))
                    .WithMessage($"Unexpected error checking existence of '{path}'");
            }
            finally
            {
                this.semaphore.Release();
            }
        }, cancellationToken);
    }

    public override async Task<Result<Stream>> ReadFileAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(path))
        {
            return Result<Stream>.Failure()
                .WithError(new FileSystemError("Path cannot be null or empty", path))
                .WithMessage("Invalid path provided");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result<Stream>.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled reading file at '{path}'");
        }

        var normalizedPath = this.NormalizePath(path);
        return await Task.Run(() =>
        {
            if (!this.semaphore.Wait(0, cancellationToken)) // Non-blocking check, fail if locked
            {
                return Result<Stream>.Failure()
                    .WithError(new ExceptionError(new TimeoutException("Operation timed out due to concurrent access")))
                    .WithMessage($"Failed to acquire lock for reading file at '{path}'");
            }

            try
            {
                if (!this.files.TryGetValue(normalizedPath, out var content))
                {
                    return Result<Stream>.Failure()
                        .WithError(new FileSystemError("File not found", path))
                        .WithMessage($"Failed to read file at '{path}'");
                }

                var stream = new MemoryStream(content);
                progress?.Report(new FileProgress { BytesProcessed = content.Length }); // Report bytes processed
                return Result<Stream>.Success(stream)
                    .WithMessage($"Read file at '{path}'");
            }
            catch (OperationCanceledException)
            {
                return Result<Stream>.Failure()
                    .WithError(new OperationCancelledError("Operation cancelled during file read"))
                    .WithMessage($"Cancelled reading file at '{path}'");
            }
            catch (Exception ex)
            {
                return Result<Stream>.Failure()
                    .WithError(new ExceptionError(ex))
                    .WithMessage($"Unexpected error reading file at '{path}'");
            }
            finally
            {
                this.semaphore.Release();
            }
        }, cancellationToken);
    }

    public override async Task<Result> WriteFileAsync(string path, Stream content, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(path))
        {
            return Result.Failure()
                .WithError(new FileSystemError("Path cannot be null or empty", path))
                .WithMessage("Invalid path provided");
        }

        if (content == null)
        {
            return Result.Failure()
                .WithError(new ArgumentError("Content stream cannot be null"))
                .WithMessage("Invalid content provided for writing");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled writing file at '{path}'");
        }

        var normalizedPath = this.NormalizePath(path);
        return await Task.Run(() =>
        {
            if (!this.semaphore.Wait(0, cancellationToken)) // Non-blocking check, fail if locked
            {
                return Result.Failure()
                    .WithError(new ExceptionError(new TimeoutException("Operation timed out due to concurrent access")))
                    .WithMessage($"Failed to acquire lock for writing file at '{path}'");
            }

            try
            {
                // Update directories: ensure parent directories exist
                var parentPath = this.GetParentPath(normalizedPath);
                while (!string.IsNullOrEmpty(parentPath))
                {
                    if (!this.directories.Contains(parentPath) && !this.files.ContainsKey(parentPath))
                    {
                        this.directories.Add(parentPath);
                    }
                    parentPath = this.GetParentPath(parentPath);
                }

                // Read the stream content
                using var memoryStream = new MemoryStream();
                content.CopyTo(memoryStream);
                var contentBytes = memoryStream.ToArray();

                // Remove if it exists as a directory (file takes precedence)
                if (this.directories.Contains(normalizedPath))
                {
                    this.directories.Remove(normalizedPath);
                }

                this.files[normalizedPath] = contentBytes;
                progress?.Report(new FileProgress { BytesProcessed = contentBytes.Length }); // Report bytes processed
                return Result.Success()
                    .WithMessage($"Wrote file at '{path}'");
            }
            catch (OperationCanceledException)
            {
                return Result.Failure()
                    .WithError(new OperationCancelledError("Operation cancelled during file write"))
                    .WithMessage($"Cancelled writing file at '{path}'");
            }
            catch (Exception ex)
            {
                return Result.Failure()
                    .WithError(new ExceptionError(ex))
                    .WithMessage($"Unexpected error writing file at '{path}'");
            }
            finally
            {
                this.semaphore.Release();
            }
        }, cancellationToken);
    }

    public override async Task<Result> DeleteFileAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(path))
        {
            return Result.Failure()
                .WithError(new FileSystemError("Path cannot be null or empty", path))
                .WithMessage("Invalid path provided");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled deleting file at '{path}'");
        }

        var normalizedPath = this.NormalizePath(path);
        return await Task.Run(() =>
        {
            if (!this.semaphore.Wait(0, cancellationToken)) // Non-blocking check, fail if locked
            {
                return Result.Failure()
                    .WithError(new ExceptionError(new TimeoutException("Operation timed out due to concurrent access")))
                    .WithMessage($"Failed to acquire lock for deleting file at '{path}'");
            }

            try
            {
                if (!this.files.Remove(normalizedPath))
                {
                    return Result.Failure()
                        .WithError(new FileSystemError("File not found", path))
                        .WithMessage($"Failed to delete file at '{path}'");
                }

                // Sync directories: check if parent directories are now empty and can be removed
                var parentPath = this.GetParentPath(normalizedPath);
                while (!string.IsNullOrEmpty(parentPath))
                {
                    var hasFilesOrDirs = this.files.Any(f => f.Key.StartsWith(parentPath + "/")) ||
                                         this.directories.Any(d => d.StartsWith(parentPath + "/") && d != parentPath);
                    if (!hasFilesOrDirs && this.directories.Contains(parentPath))
                    {
                        this.directories.Remove(parentPath);
                    }
                    parentPath = this.GetParentPath(parentPath);
                } // Report minimal progress for deletion
                return Result.Success()
                    .WithMessage($"Deleted file at '{path}'");
            }
            catch (OperationCanceledException)
            {
                return Result.Failure()
                    .WithError(new OperationCancelledError("Operation cancelled during file deletion"))
                    .WithMessage($"Cancelled deleting file at '{path}'");
            }
            catch (Exception ex)
            {
                return Result.Failure()
                    .WithError(new ExceptionError(ex))
                    .WithMessage($"Unexpected error deleting file at '{path}'");
            }
            finally
            {
                this.semaphore.Release();
            }
        }, cancellationToken);
    }

    public override async Task<Result<string>> GetChecksumAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(path))
        {
            return Result<string>.Failure()
                .WithError(new FileSystemError("Path cannot be null or empty", path))
                .WithMessage("Invalid path provided");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result<string>.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled getting checksum for '{path}'");
        }

        var normalizedPath = this.NormalizePath(path);
        return await Task.Run(() =>
        {
            if (!this.semaphore.Wait(0, cancellationToken)) // Non-blocking check, fail if locked
            {
                return Result<string>.Failure()
                    .WithError(new ExceptionError(new TimeoutException("Operation timed out due to concurrent access")))
                    .WithMessage($"Failed to acquire lock for getting checksum of '{path}'");
            }

            try
            {
                if (!this.files.TryGetValue(normalizedPath, out var content))
                {
                    return Result<string>.Failure()
                        .WithError(new FileSystemError("File not found", path))
                        .WithMessage($"Failed to get checksum for '{path}'");
                }
                var hash = SHA256.HashData(content);
                var checksum = Convert.ToBase64String(hash);
                return Result<string>.Success(checksum)
                    .WithMessage($"Computed checksum for file at '{path}'");
            }
            catch (OperationCanceledException)
            {
                return Result<string>.Failure()
                    .WithError(new OperationCancelledError("Operation cancelled during checksum computation"))
                    .WithMessage($"Cancelled getting checksum for '{path}'");
            }
            catch (Exception ex)
            {
                return Result<string>.Failure()
                    .WithError(new ExceptionError(ex))
                    .WithMessage($"Unexpected error getting checksum for '{path}'");
            }
            finally
            {
                this.semaphore.Release();
            }
        }, cancellationToken);
    }

    public override async Task<Result<FileMetadata>> GetFileInfoAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(path))
        {
            return Result<FileMetadata>.Failure()
                .WithError(new FileSystemError("Path cannot be null or empty", path))
                .WithMessage("Invalid path provided");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result<FileMetadata>.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled getting info for '{path}'");
        }

        var normalizedPath = this.NormalizePath(path);
        return await Task.Run(() =>
        {
            if (!this.semaphore.Wait(0, cancellationToken)) // Non-blocking check, fail if locked
            {
                return Result<FileMetadata>.Failure()
                    .WithError(new ExceptionError(new TimeoutException("Operation timed out due to concurrent access")))
                    .WithMessage($"Failed to acquire lock for getting info of '{path}'");
            }

            try
            {
                if (this.files.TryGetValue(normalizedPath, out var content))
                {
                    return Result<FileMetadata>.Success(new FileMetadata
                    {
                        Path = path,
                        Length = content.Length,
                        LastModified = DateTime.UtcNow
                    }).WithMessage($"Retrieved metadata for file at '{path}'");
                }

                if (this.directories.Contains(normalizedPath))
                {
                    return Result<FileMetadata>.Success(new FileMetadata
                    {
                        Path = path,
                        Length = 0,
                        LastModified = DateTime.UtcNow
                    }).WithMessage($"Retrieved metadata for directory at '{path}'");
                }

                return Result<FileMetadata>.Failure()
                    .WithError(new FileSystemError("File or directory not found", path))
                    .WithMessage($"Failed to get info for '{path}'");
            }
            catch (OperationCanceledException)
            {
                return Result<FileMetadata>.Failure()
                    .WithError(new OperationCancelledError("Operation cancelled during info retrieval"))
                    .WithMessage($"Cancelled getting info for '{path}'");
            }
            catch (Exception ex)
            {
                return Result<FileMetadata>.Failure()
                    .WithError(new ExceptionError(ex))
                    .WithMessage($"Unexpected error getting info for '{path}'");
            }
            finally
            {
                this.semaphore.Release();
            }
        }, cancellationToken);
    }

    public override async Task<Result> SetFileMetadataAsync(string path, FileMetadata metadata, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(path))
        {
            return Result.Failure()
                .WithError(new FileSystemError("Path cannot be null or empty", path))
                .WithMessage("Invalid path provided");
        }

        if (metadata == null)
        {
            return Result.Failure()
                .WithError(new ArgumentError("Metadata cannot be null"))
                .WithMessage("Invalid metadata provided");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled setting metadata for '{path}'");
        }

        var normalizedPath = this.NormalizePath(path);
        return await Task.Run(() =>
        {
            if (!this.semaphore.Wait(0, cancellationToken)) // Non-blocking check, fail if locked
            {
                return Result.Failure()
                    .WithError(new ExceptionError(new TimeoutException("Operation timed out due to concurrent access")))
                    .WithMessage($"Failed to acquire lock for setting metadata of '{path}'");
            }

            try
            {
                if (this.files.ContainsKey(normalizedPath) || this.directories.Contains(normalizedPath))
                {
                    return Result.Success()
                        .WithMessage($"Set metadata for file at '{path}'");
                }

                return Result.Failure()
                    .WithError(new FileSystemError("File or directory not found", path))
                    .WithMessage($"Failed to set metadata for '{path}'");
            }
            catch (OperationCanceledException)
            {
                return Result.Failure()
                    .WithError(new OperationCancelledError("Operation cancelled during metadata set"))
                    .WithMessage($"Cancelled setting metadata for '{path}'");
            }
            catch (Exception ex)
            {
                return Result.Failure()
                    .WithError(new ExceptionError(ex))
                    .WithMessage($"Unexpected error setting metadata for '{path}'");
            }
            finally
            {
                this.semaphore.Release();
            }
        }, cancellationToken);
    }

    public override async Task<Result<FileMetadata>> UpdateFileMetadataAsync(string path, Func<FileMetadata, FileMetadata> metadataUpdate, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(path))
        {
            return Result<FileMetadata>.Failure()
                .WithError(new FileSystemError("Path cannot be null or empty", path))
                .WithMessage("Invalid path provided");
        }

        if (metadataUpdate == null)
        {
            return Result<FileMetadata>.Failure()
                .WithError(new ArgumentError("Metadata update function cannot be null"))
                .WithMessage("Invalid metadata update provided");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result<FileMetadata>.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled updating metadata for '{path}'");
        }

        var normalizedPath = this.NormalizePath(path);
        return await Task.Run(() =>
        {
            if (!this.semaphore.Wait(0, cancellationToken)) // Non-blocking check, fail if locked
            {
                return Result<FileMetadata>.Failure()
                    .WithError(new ExceptionError(new TimeoutException("Operation timed out due to concurrent access")))
                    .WithMessage($"Failed to acquire lock for updating metadata of '{path}'");
            }

            try
            {
                FileMetadata currentMetadata;
                if (this.files.TryGetValue(normalizedPath, out var content))
                {
                    currentMetadata = new FileMetadata
                    {
                        Path = path,
                        Length = content.Length,
                        LastModified = DateTime.UtcNow
                    };
                }
                else if (this.directories.Contains(normalizedPath))
                {
                    currentMetadata = new FileMetadata
                    {
                        Path = path,
                        Length = 0,
                        LastModified = DateTime.UtcNow
                    };
                }
                else
                {
                    return Result<FileMetadata>.Failure()
                        .WithError(new FileSystemError("File or directory not found", path))
                        .WithMessage($"Failed to update metadata for '{path}'");
                }

                var updatedMetadata = metadataUpdate(currentMetadata);
                return Result<FileMetadata>.Success(updatedMetadata)
                    .WithMessage($"Updated metadata for file at '{path}'");
            }
            catch (OperationCanceledException)
            {
                return Result<FileMetadata>.Failure()
                    .WithError(new OperationCancelledError("Operation cancelled during metadata update"))
                    .WithMessage($"Cancelled updating metadata for '{path}'");
            }
            catch (Exception ex)
            {
                return Result<FileMetadata>.Failure()
                    .WithError(new ExceptionError(ex))
                    .WithMessage($"Unexpected error updating metadata for '{path}'");
            }
            finally
            {
                this.semaphore.Release();
            }
        }, cancellationToken);
    }

    public override async Task<Result<(IEnumerable<string> Files, string NextContinuationToken)>> ListFilesAsync(
        string path, string searchPattern = null, bool recursive = false, string continuationToken = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(path))
        {
            return Result<(IEnumerable<string> Files, string NextContinuationToken)>.Failure()
                .WithError(new FileSystemError("Path cannot be null or empty", path))
                .WithMessage("Invalid path provided");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result<(IEnumerable<string> Files, string NextContinuationToken)>.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled listing files at '{path}'");
        }

        var normalizedPath = this.NormalizePath(path.TrimEnd('/'));
        return await Task.Run(() =>
        {
            if (!this.semaphore.Wait(0, cancellationToken)) // Non-blocking check, fail if locked
            {
                return Result<(IEnumerable<string> Files, string NextContinuationToken)>.Failure()
                    .WithError(new ExceptionError(new TimeoutException("Operation timed out due to concurrent access")))
                    .WithMessage($"Failed to acquire lock for listing files at '{path}'");
            }

            try
            {
                var filesList = this.files
                    .Where(f => f.Key.StartsWith(normalizedPath + "/", StringComparison.OrdinalIgnoreCase) || f.Key == normalizedPath)
                    .Where(f => f.Key.Match(searchPattern))
                    .Select(f => f.Key)
                    //.Select(f => f.Key.Substring(normalizedPath.Length).TrimStart('/'))
                    //.Where(f => !string.IsNullOrEmpty(f))
                    .Distinct()
                    .Order()
                    .ToList();

                if (!recursive)
                {
                    filesList = [.. filesList.Where(f => !f.Contains('/'))];
                }

                return Result<(IEnumerable<string> Files, string NextContinuationToken)>.Success((filesList, null))
                    .WithMessage($"Listed files in '{path}'");
            }
            catch (OperationCanceledException)
            {
                return Result<(IEnumerable<string> Files, string NextContinuationToken)>.Failure()
                    .WithError(new OperationCancelledError("Operation cancelled during file listing"))
                    .WithMessage($"Cancelled listing files at '{path}'");
            }
            catch (Exception ex)
            {
                return Result<(IEnumerable<string> Files, string NextContinuationToken)>.Failure()
                    .WithError(new ExceptionError(ex))
                    .WithMessage($"Unexpected error listing files at '{path}'");
            }
            finally
            {
                this.semaphore.Release();
            }
        }, cancellationToken);
    }

    public override async Task<Result> CopyFileAsync(string sourcePath, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destinationPath))
        {
            return Result.Failure()
                .WithError(new FileSystemError("Source or destination path cannot be null or empty", $"{sourcePath} -> {destinationPath}"))
                .WithMessage("Invalid paths provided");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled copying file from '{sourcePath}' to '{destinationPath}'");
        }

        var normalizedSource = this.NormalizePath(sourcePath);
        var normalizedDest = this.NormalizePath(destinationPath);
        return await Task.Run(() =>
        {
            if (!this.semaphore.Wait(0, cancellationToken)) // Non-blocking check, fail if locked
            {
                return Result.Failure()
                    .WithError(new ExceptionError(new TimeoutException("Operation timed out due to concurrent access")))
                    .WithMessage($"Failed to acquire lock for copying file from '{sourcePath}' to '{destinationPath}'");
            }

            try
            {
                if (!this.files.TryGetValue(normalizedSource, out var content))
                {
                    return Result.Failure()
                        .WithError(new FileSystemError("Source file not found", sourcePath))
                        .WithMessage($"Failed to copy file from '{sourcePath}' to '{destinationPath}'");
                }

                // Update directories for destination
                var destParentPath = this.GetParentPath(normalizedDest);
                while (!string.IsNullOrEmpty(destParentPath))
                {
                    if (!this.directories.Contains(destParentPath) && !this.files.ContainsKey(destParentPath))
                    {
                        this.directories.Add(destParentPath);
                    }
                    destParentPath = this.GetParentPath(destParentPath);
                }

                // Remove if destination exists as a directory
                this.directories.Remove(normalizedDest);

                this.files[normalizedDest] = content;
                progress?.Report(new FileProgress { BytesProcessed = content.Length }); // Report bytes processed
                return Result.Success()
                    .WithMessage($"Copied file from '{sourcePath}' to '{destinationPath}'");
            }
            catch (OperationCanceledException)
            {
                return Result.Failure()
                    .WithError(new OperationCancelledError("Operation cancelled during file copy"))
                    .WithMessage($"Cancelled copying file from '{sourcePath}' to '{destinationPath}'");
            }
            catch (Exception ex)
            {
                return Result.Failure()
                    .WithError(new ExceptionError(ex))
                    .WithMessage($"Unexpected error copying file from '{sourcePath}' to '{destinationPath}'");
            }
            finally
            {
                this.semaphore.Release();
            }
        }, cancellationToken);
    }

    public override async Task<Result> RenameFileAsync(string oldPath, string newPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(oldPath) || string.IsNullOrEmpty(newPath))
        {
            return Result.Failure()
                .WithError(new FileSystemError("Old or new path cannot be null or empty", $"{oldPath} -> {newPath}"))
                .WithMessage("Invalid paths provided");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled renaming file from '{oldPath}' to '{newPath}'");
        }

        var normalizedOld = this.NormalizePath(oldPath);
        var normalizedNew = this.NormalizePath(newPath);
        return await Task.Run(() =>
        {
            if (!this.semaphore.Wait(0, cancellationToken)) // Non-blocking check, fail if locked
            {
                return Result.Failure()
                    .WithError(new ExceptionError(new TimeoutException("Operation timed out due to concurrent access")))
                    .WithMessage($"Failed to acquire lock for renaming file from '{oldPath}' to '{newPath}'");
            }

            try
            {
                if (!this.files.TryGetValue(normalizedOld, out var content))
                {
                    return Result.Failure()
                        .WithError(new FileSystemError("Source file not found", oldPath))
                        .WithMessage($"Failed to rename file from '{oldPath}' to '{newPath}'");
                }

                // Update directories for new path
                var newParentPath = this.GetParentPath(normalizedNew);
                while (!string.IsNullOrEmpty(newParentPath))
                {
                    if (!this.directories.Contains(newParentPath) && !this.files.ContainsKey(newParentPath))
                    {
                        this.directories.Add(newParentPath);
                    }
                    newParentPath = this.GetParentPath(newParentPath);
                }

                // Remove if new path exists as a directory
                this.directories.Remove(normalizedNew);

                this.files.Remove(normalizedOld);
                this.files[normalizedNew] = content;
                progress?.Report(new FileProgress { BytesProcessed = content.Length }); // Report bytes processed
                return Result.Success()
                    .WithMessage($"Renamed file from '{oldPath}' to '{newPath}'");
            }
            catch (OperationCanceledException)
            {
                return Result.Failure()
                    .WithError(new OperationCancelledError("Operation cancelled during file rename"))
                    .WithMessage($"Cancelled renaming file from '{oldPath}' to '{newPath}'");
            }
            catch (Exception ex)
            {
                return Result.Failure()
                    .WithError(new ExceptionError(ex))
                    .WithMessage($"Unexpected error renaming file from '{oldPath}' to '{newPath}'");
            }
            finally
            {
                this.semaphore.Release();
            }
        }, cancellationToken);
    }

    public override async Task<Result> MoveFileAsync(string sourcePath, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destinationPath))
        {
            return Result.Failure()
                .WithError(new FileSystemError("Source or destination path cannot be null or empty", $"{sourcePath} -> {destinationPath}"))
                .WithMessage("Invalid paths provided");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled moving file from '{sourcePath}' to '{destinationPath}'");
        }

        var normalizedSource = this.NormalizePath(sourcePath);
        var normalizedDest = this.NormalizePath(destinationPath);
        return await Task.Run(() =>
        {
            if (!this.semaphore.Wait(0, cancellationToken)) // Non-blocking check, fail if locked
            {
                return Result.Failure()
                    .WithError(new ExceptionError(new TimeoutException("Operation timed out due to concurrent access")))
                    .WithMessage($"Failed to acquire lock for moving file from '{sourcePath}' to '{destinationPath}'");
            }

            try
            {
                if (!this.files.TryGetValue(normalizedSource, out var content))
                {
                    return Result.Failure()
                        .WithError(new FileSystemError("Source file not found", sourcePath))
                        .WithMessage($"Failed to move file from '{sourcePath}' to '{destinationPath}'");
                }

                // Update directories for destination
                var destParentPath = this.GetParentPath(normalizedDest);
                while (!string.IsNullOrEmpty(destParentPath))
                {
                    if (!this.directories.Contains(destParentPath) && !this.files.ContainsKey(destParentPath))
                    {
                        this.directories.Add(destParentPath);
                    }
                    destParentPath = this.GetParentPath(destParentPath);
                }

                // Remove if destination exists as a directory
                this.directories.Remove(normalizedDest);

                this.files.Remove(normalizedSource);
                this.files[normalizedDest] = content;
                progress?.Report(new FileProgress { BytesProcessed = content.Length }); // Report bytes processed
                return Result.Success()
                    .WithMessage($"Moved file from '{sourcePath}' to '{destinationPath}'");
            }
            catch (OperationCanceledException)
            {
                return Result.Failure()
                    .WithError(new OperationCancelledError("Operation cancelled during file move"))
                    .WithMessage($"Cancelled moving file from '{sourcePath}' to '{destinationPath}'");
            }
            catch (Exception ex)
            {
                return Result.Failure()
                    .WithError(new ExceptionError(ex))
                    .WithMessage($"Unexpected error moving file from '{sourcePath}' to '{destinationPath}'");
            }
            finally
            {
                this.semaphore.Release();
            }
        }, cancellationToken);
    }

    public override async Task<Result> CopyFilesAsync(IEnumerable<(string SourcePath, string DestinationPath)> filePairs, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        if (filePairs?.Any() != true)
        {
            return Result.Failure()
                .WithError(new ArgumentError("File pairs cannot be null or empty"))
                .WithMessage("Invalid file pairs provided");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage("Cancelled copying multiple files");
        }

        return await Task.Run(() =>
        {
            if (!this.semaphore.Wait(0, cancellationToken)) // Non-blocking check, fail if locked
            {
                return Result.Failure()
                    .WithError(new ExceptionError(new TimeoutException("Operation timed out due to concurrent access")))
                    .WithMessage("Failed to acquire lock for copying multiple files");
            }

            try
            {
                var errors = new List<IResultError>();
                long totalBytes = 0;

                foreach (var (source, dest) in filePairs)
                {
                    if (!this.files.TryGetValue(this.NormalizePath(source), out var content))
                    {
                        errors.Add(new FileSystemError("Source file not found", source));
                        continue;
                    }

                    // Update directories for destination
                    var destNormalized = this.NormalizePath(dest);
                    var destParentPath = this.GetParentPath(destNormalized);
                    while (!string.IsNullOrEmpty(destParentPath))
                    {
                        if (!this.directories.Contains(destParentPath) && !this.files.ContainsKey(destParentPath))
                        {
                            this.directories.Add(destParentPath);
                        }
                        destParentPath = this.GetParentPath(destParentPath);
                    }

                    // Remove if destination exists as a directory
                    this.directories.Remove(destNormalized);

                    this.files[destNormalized] = content;
                    totalBytes += content.Length;
                    progress?.Report(new FileProgress { BytesProcessed = totalBytes, FilesProcessed = filePairs.TakeWhile(p => p.SourcePath != source).Count() + 1, TotalFiles = filePairs.Count() }); // Report bytes and file progress
                }

                if (errors.Count != 0)
                {
                    return Result.Failure()
                        .WithErrors(errors)
                        .WithMessage("Failed to copy some files");
                }

                return Result.Success()
                    .WithMessage($"Copied all {filePairs.Count()} files");
            }
            catch (OperationCanceledException)
            {
                return Result.Failure()
                    .WithError(new OperationCancelledError("Operation cancelled during file copy"))
                    .WithMessage("Cancelled copying multiple files");
            }
            catch (Exception ex)
            {
                return Result.Failure()
                    .WithError(new ExceptionError(ex))
                    .WithMessage("Unexpected error copying multiple files");
            }
            finally
            {
                this.semaphore.Release();
            }
        }, cancellationToken);
    }

    public override async Task<Result> MoveFilesAsync(IEnumerable<(string SourcePath, string DestinationPath)> filePairs, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        if (filePairs?.Any() != true)
        {
            return Result.Failure()
                .WithError(new ArgumentError("File pairs cannot be null or empty"))
                .WithMessage("Invalid file pairs provided");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage("Cancelled moving multiple files");
        }

        return await Task.Run(() =>
        {
            if (!this.semaphore.Wait(0, cancellationToken)) // Non-blocking check, fail if locked
            {
                return Result.Failure()
                    .WithError(new ExceptionError(new TimeoutException("Operation timed out due to concurrent access")))
                    .WithMessage("Failed to acquire lock for moving multiple files");
            }

            try
            {
                var errors = new List<IResultError>();
                long totalBytes = 0;

                foreach (var (source, dest) in filePairs)
                {
                    var normalizedSource = this.NormalizePath(source);
                    var normalizedDest = this.NormalizePath(dest);

                    if (!this.files.TryGetValue(normalizedSource, out var content))
                    {
                        errors.Add(new FileSystemError("Source file not found", source));
                        continue;
                    }

                    // Update directories for destination
                    var destParentPath = this.GetParentPath(normalizedDest);
                    while (!string.IsNullOrEmpty(destParentPath))
                    {
                        if (!this.directories.Contains(destParentPath) && !this.files.ContainsKey(destParentPath))
                        {
                            this.directories.Add(destParentPath);
                        }
                        destParentPath = this.GetParentPath(destParentPath);
                    }

                    // Remove if destination exists as a directory
                    this.directories.Remove(normalizedDest);

                    this.files.Remove(normalizedSource);
                    this.files[normalizedDest] = content;
                    totalBytes += content.Length;
                    progress?.Report(new FileProgress { BytesProcessed = totalBytes, FilesProcessed = filePairs.TakeWhile(p => p.SourcePath != source).Count() + 1, TotalFiles = filePairs.Count() }); // Report bytes and file progress
                }

                if (errors.Count != 0)
                {
                    return Result.Failure()
                        .WithErrors(errors)
                        .WithMessage("Failed to move some files");
                }

                return Result.Success()
                    .WithMessage($"Moved all {filePairs.Count()} files");
            }
            catch (OperationCanceledException)
            {
                return Result.Failure()
                    .WithError(new OperationCancelledError("Operation cancelled during file move"))
                    .WithMessage("Cancelled moving multiple files");
            }
            catch (Exception ex)
            {
                return Result.Failure()
                    .WithError(new ExceptionError(ex))
                    .WithMessage("Unexpected error moving multiple files");
            }
            finally
            {
                this.semaphore.Release();
            }
        }, cancellationToken);
    }

    public override async Task<Result> DeleteFilesAsync(IEnumerable<string> paths, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        if (paths?.Any() != true)
        {
            return Result.Failure()
                .WithError(new ArgumentError("Paths cannot be null or empty"))
                .WithMessage("Invalid paths provided");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage("Cancelled deleting multiple files");
        }

        return await Task.Run(() =>
        {
            if (!this.semaphore.Wait(0, cancellationToken)) // Non-blocking check, fail if locked
            {
                return Result.Failure()
                    .WithError(new ExceptionError(new TimeoutException("Operation timed out due to concurrent access")))
                    .WithMessage("Failed to acquire lock for deleting multiple files");
            }

            try
            {
                var errors = new List<IResultError>();
                long totalBytes = 0;

                foreach (var path in paths)
                {
                    var normalizedPath = this.NormalizePath(path);
                    if (this.files.TryGetValue(normalizedPath, out var content))
                    {
                        this.files.Remove(normalizedPath);
                        totalBytes += content.Length;
                        progress?.Report(new FileProgress { BytesProcessed = totalBytes, FilesProcessed = paths.TakeWhile(p => p != path).Count() + 1, TotalFiles = paths.Count() }); // Report bytes and file progress

                        // Sync directories: check if parent directories are now empty and can be removed
                        var parentPath = this.GetParentPath(normalizedPath);
                        while (!string.IsNullOrEmpty(parentPath))
                        {
                            var hasFilesOrDirs = this.files.Any(f => f.Key.StartsWith(parentPath + "/")) ||
                                                 this.directories.Any(d => d.StartsWith(parentPath + "/") && d != parentPath);
                            if (!hasFilesOrDirs && this.directories.Contains(parentPath))
                            {
                                this.directories.Remove(parentPath);
                            }
                            parentPath = this.GetParentPath(parentPath);
                        }
                    }
                    else
                    {
                        errors.Add(new FileSystemError("File not found", path));
                    }
                }

                if (errors.Count != 0)
                {
                    return Result.Failure()
                        .WithErrors(errors)
                        .WithMessage("Failed to delete some files");
                }

                return Result.Success()
                    .WithMessage($"Deleted all {paths.Count()} files");
            }
            catch (OperationCanceledException)
            {
                return Result.Failure()
                    .WithError(new OperationCancelledError("Operation cancelled during file deletion"))
                    .WithMessage("Cancelled deleting multiple files");
            }
            catch (Exception ex)
            {
                return Result.Failure()
                    .WithError(new ExceptionError(ex))
                    .WithMessage("Unexpected error deleting multiple files");
            }
            finally
            {
                this.semaphore.Release();
            }
        }, cancellationToken);
    }

    public override async Task<Result> IsDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(path))
        {
            return Result.Failure()
                .WithError(new FileSystemError("Path cannot be null or empty", path))
                .WithMessage("Invalid path provided");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled checking if '{path}' is a directory");
        }

        var normalizedPath = this.NormalizePath(path.TrimEnd('/'));
        return await Task.Run(() =>
        {
            if (!this.semaphore.Wait(0, cancellationToken)) // Non-blocking check, fail if locked
            {
                return Result.Failure()
                    .WithError(new ExceptionError(new TimeoutException("Operation timed out due to concurrent access")))
                    .WithMessage($"Failed to acquire lock for checking if '{path}' is a directory");
            }

            try
            {
                var isDirectory = this.directories.Contains(normalizedPath);
                if (!isDirectory)
                {
                    return Result.Failure()
                    .WithError(new NotFoundError("Directory not found"));
                }

                return Result.Success()
                    .WithMessage($"Checked if '{path}' is a directory");
            }
            catch (OperationCanceledException)
            {
                return Result.Failure()
                    .WithError(new OperationCancelledError("Operation cancelled during directory check"))
                    .WithMessage($"Cancelled checking if '{path}' is a directory");
            }
            catch (Exception ex)
            {
                return Result.Failure()
                    .WithError(new ExceptionError(ex))
                    .WithMessage($"Unexpected error checking if '{path}' is a directory");
            }
            finally
            {
                this.semaphore.Release();
            }
        }, cancellationToken);
    }

    public override async Task<Result> CreateDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(path))
        {
            return Result.Failure()
                .WithError(new FileSystemError("Path cannot be null or empty", path))
                .WithMessage("Invalid path provided");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled creating directory at '{path}'");
        }

        var normalizedPath = this.NormalizePath(path.TrimEnd('/'));
        return await Task.Run(() =>
        {
            if (!this.semaphore.Wait(0, cancellationToken)) // Non-blocking check, fail if locked
            {
                return Result.Failure()
                    .WithError(new ExceptionError(new TimeoutException("Operation timed out due to concurrent access")))
                    .WithMessage($"Failed to acquire lock for creating directory at '{path}'");
            }

            try
            {
                if (this.directories.Contains(normalizedPath) || this.files.ContainsKey(normalizedPath))
                {
                    return Result.Success()
                        .WithMessage($"Directory allready exists at '{path}'");
                }

                // Ensure parent directories exist
                var parentPath = this.GetParentPath(normalizedPath);
                while (!string.IsNullOrEmpty(parentPath))
                {
                    if (!this.directories.Contains(parentPath) && !this.files.ContainsKey(parentPath))
                    {
                        this.directories.Add(parentPath);
                    }
                    parentPath = this.GetParentPath(parentPath);
                }

                this.directories.Add(normalizedPath);
                return Result.Success()
                    .WithMessage($"Created directory at '{path}'");
            }
            catch (OperationCanceledException)
            {
                return Result.Failure()
                    .WithError(new OperationCancelledError("Operation cancelled during directory creation"))
                    .WithMessage($"Cancelled creating directory at '{path}'");
            }
            catch (Exception ex)
            {
                return Result.Failure()
                    .WithError(new ExceptionError(ex))
                    .WithMessage($"Unexpected error creating directory at '{path}'");
            }
            finally
            {
                this.semaphore.Release();
            }
        }, cancellationToken);
    }

    public override async Task<Result> DeleteDirectoryAsync(string path, bool recursive, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(path))
        {
            return Result.Failure()
                .WithError(new FileSystemError("Path cannot be null or empty", path))
                .WithMessage("Invalid path provided");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled deleting directory at '{path}'");
        }

        var normalizedPath = this.NormalizePath(path.TrimEnd('/'));
        return await Task.Run(() =>
        {
            if (!this.semaphore.Wait(0, cancellationToken)) // Non-blocking check, fail if locked
            {
                return Result.Failure()
                    .WithError(new ExceptionError(new TimeoutException("Operation timed out due to concurrent access")))
                    .WithMessage($"Failed to acquire lock for deleting directory at '{path}'");
            }

            try
            {
                if (!this.directories.Contains(normalizedPath))
                {
                    return Result.Failure()
                        .WithError(new FileSystemError("Directory not found", path))
                        .WithMessage($"Failed to delete directory at '{path}'");
                }

                if (recursive)
                {
                    var subPaths = this.files.Keys
                        .Where(k => k.StartsWith(normalizedPath + "/", StringComparison.OrdinalIgnoreCase) || k == normalizedPath)
                        .Concat(this.directories.Where(d => d.StartsWith(normalizedPath + "/", StringComparison.OrdinalIgnoreCase) || d == normalizedPath))
                        .ToList();

                    foreach (var subPath in subPaths)
                    {
                        this.files.Remove(subPath);
                        this.directories.Remove(subPath);
                    }
                }
                else
                {
                    if (this.files.Any(f => f.Key.StartsWith(normalizedPath + "/", StringComparison.OrdinalIgnoreCase)))
                    {
                        return Result.Failure()
                            .WithError(new FileSystemError("Directory not empty", path))
                            .WithMessage($"Failed to delete non-empty directory at '{path}'");
                    }

                    this.directories.Remove(normalizedPath);
                }

                // Sync directories: check if parent directories are now empty and can be removed
                var parentPath = this.GetParentPath(normalizedPath);
                while (!string.IsNullOrEmpty(parentPath))
                {
                    var hasFilesOrDirs = this.files.Any(f => f.Key.StartsWith(parentPath + "/")) ||
                                         this.directories.Any(d => d.StartsWith(parentPath + "/") && d != parentPath);
                    if (!hasFilesOrDirs && this.directories.Contains(parentPath))
                    {
                        this.directories.Remove(parentPath);
                    }
                    parentPath = this.GetParentPath(parentPath);
                }

                return Result.Success()
                    .WithMessage($"Deleted directory at '{path}'");
            }
            catch (OperationCanceledException)
            {
                return Result.Failure()
                    .WithError(new OperationCancelledError("Operation cancelled during directory deletion"))
                    .WithMessage($"Cancelled deleting directory at '{path}'");
            }
            catch (Exception ex)
            {
                return Result.Failure()
                    .WithError(new ExceptionError(ex))
                    .WithMessage($"Unexpected error deleting directory at '{path}'");
            }
            finally
            {
                this.semaphore.Release();
            }
        }, cancellationToken);
    }

    public override async Task<Result<IEnumerable<string>>> ListDirectoriesAsync(
        string path, string searchPattern = null, bool recursive = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(path))
        {
            return Result<IEnumerable<string>>.Failure()
                .WithError(new FileSystemError("Path cannot be null or empty", path))
                .WithMessage("Invalid path provided");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result<IEnumerable<string>>.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled listing directories at '{path}'");
        }

        var normalizedPath = this.NormalizePath(path.TrimEnd('/'));
        return await Task.Run(() =>
        {
            if (!this.semaphore.Wait(0, cancellationToken)) // Non-blocking check, fail if locked
            {
                return Result<IEnumerable<string>>.Failure()
                    .WithError(new ExceptionError(new TimeoutException("Operation timed out due to concurrent access")))
                    .WithMessage($"Failed to acquire lock for listing directories at '{path}'");
            }

            try
            {
                var directoriesList = this.directories
                    .Where(d => d.StartsWith(normalizedPath, StringComparison.OrdinalIgnoreCase))
                    .Where(d => d.Match(searchPattern))
                    //.Select(d => d.Substring(normalizedPath.Length).TrimStart('/'))
                    //.Where(d => !string.IsNullOrEmpty(d))
                    .Distinct()
                    .Order()
                    .ToList();

                if (!recursive)
                {
                    directoriesList = [.. directoriesList.Where(d => !d.Contains('/'))];
                }

                return Result<IEnumerable<string>>.Success(directoriesList)
                    .WithMessage($"Listed directories at '{path}'");
            }
            catch (OperationCanceledException)
            {
                return Result<IEnumerable<string>>.Failure()
                    .WithError(new OperationCancelledError("Operation cancelled during directory listing"))
                    .WithMessage($"Cancelled listing directories at '{path}'");
            }
            catch (Exception ex)
            {
                return Result<IEnumerable<string>>.Failure()
                    .WithError(new ExceptionError(ex))
                    .WithMessage($"Unexpected error listing directories at '{path}'");
            }
            finally
            {
                this.semaphore.Release();
            }
        }, cancellationToken);
    }

    public override async Task<Result> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled health check for '{this.LocationName}'");
        }

        return await Task.Run(() =>
        {
            if (!this.semaphore.Wait(0, cancellationToken)) // Non-blocking check, fail if locked
            {
                return Result.Failure()
                    .WithError(new ExceptionError(new TimeoutException("Operation timed out due to concurrent access")))
                    .WithMessage($"Failed to acquire lock for health check of '{this.LocationName}'");
            }

            try
            {
                return Result.Success()
                    .WithMessage($"In-memory storage at '{this.LocationName}' is healthy");
            }
            catch (OperationCanceledException)
            {
                return Result.Failure()
                    .WithError(new OperationCancelledError("Operation cancelled during health check"))
                    .WithMessage($"Cancelled health check for '{this.LocationName}'");
            }
            catch (Exception ex)
            {
                return Result.Failure()
                    .WithError(new ExceptionError(ex))
                    .WithMessage($"Unexpected error checking health of '{this.LocationName}'");
            }
            finally
            {
                this.semaphore.Release();
            }
        }, cancellationToken);
    }

    private string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        return path.Replace('\\', '/')
            .TrimStart('/')
            .TrimEnd('/')
            .ToLowerInvariant();
    }

    private string GetParentPath(string path)
    {
        if (string.IsNullOrEmpty(path) || !path.Contains('/'))
        {
            return string.Empty;
        }
        var parts = path.Split('/').ToList();
        parts.RemoveAt(parts.Count - 1);
        return string.Join("/", parts);
    }
}