// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using BridgingIT.DevKit.Common;

/// <summary>
/// A thread-safe local file system implementation of IFileStorageProvider for file operations on disk.
/// </summary>
public class LocalFileStorageProvider : BaseFileStorageProvider, IDisposable
{
    private readonly string rootPath;
    private readonly TimeSpan lockTimeout;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> fileLocks = new();
    private bool disposed;

    public LocalFileStorageProvider(string rootPath, string locationName, TimeSpan? lockTimeout = null)
        : base(locationName)
    {
        this.rootPath = Path.GetFullPath(rootPath);
        this.lockTimeout = lockTimeout ?? TimeSpan.FromSeconds(30); // Default timeout of 30 seconds
        Directory.CreateDirectory(this.rootPath); // Ensure root exists
    }

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
                .WithMessage($"Cancelled checking existence of file at '{path}'");
        }

        var fullPath = this.GetFullPath(path);
        var semaphore = this.GetSemaphore(fullPath);

        try
        {
            if (!await semaphore.WaitAsync(this.lockTimeout, cancellationToken))
            {
                return Result.Failure()
                    .WithError(new TimeoutError("Timeout waiting for file access"))
                    .WithMessage($"Failed to acquire lock for checking existence of file at '{path}'");
            }

            var exists = File.Exists(fullPath);
            if (!exists)
            {
                return Result.Failure()
                    .WithError(new NotFoundError("File not found"));
            }

            var length = exists ? new FileInfo(fullPath).Length : 0;
            this.ReportProgress(progress, path, length, 1);

            return Result.Success()
                .WithMessage($"Checked existence of file at '{path}'");
        }
        finally
        {
            this.ReleaseSemaphore(fullPath, semaphore);
        }
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

        var fullPath = this.GetFullPath(path);
        var semaphore = this.GetSemaphore(fullPath);

        try
        {
            if (!await semaphore.WaitAsync(this.lockTimeout, cancellationToken))
            {
                return Result<Stream>.Failure()
                    .WithError(new TimeoutError("Timeout waiting for file access"))
                    .WithMessage($"Failed to acquire lock for reading file at '{path}'");
            }

            if (!File.Exists(fullPath))
            {
                return Result<Stream>.Failure()
                    .WithError(new FileSystemError("File not found", path))
                    .WithMessage($"Failed to read file at '{path}'");
            }

            try
            {
                var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                var length = new FileInfo(fullPath).Length;
                this.ReportProgress(progress, path, length, 1);
                return Result<Stream>.Success(stream)
                    .WithMessage($"Read file at '{path}'");
            }
            catch (UnauthorizedAccessException ex)
            {
                return Result<Stream>.Failure()
                    .WithError(new PermissionError("Access denied", path, ex))
                    .WithMessage($"Permission denied for file at '{path}'");
            }
            catch (Exception ex)
            {
                return Result<Stream>.Failure()
                    .WithError(new ExceptionError(ex))
                    .WithMessage($"Unexpected error reading file at '{path}'");
            }
        }
        finally
        {
            this.ReleaseSemaphore(fullPath, semaphore);
        }
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

        var fullPath = this.GetFullPath(path);
        var semaphore = this.GetSemaphore(fullPath);

        try
        {
            if (!await semaphore.WaitAsync(this.lockTimeout, cancellationToken))
            {
                return Result.Failure()
                    .WithError(new TimeoutError("Timeout waiting for file access"))
                    .WithMessage($"Failed to acquire lock for writing file at '{path}'");
            }

            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            try
            {
                await using var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
                var bytesRead = 0L;
                var buffer = new byte[4096];
                int read;
                while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException("Write operation cancelled");
                    }

                    await fs.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    bytesRead += read;
                    this.ReportProgress(progress, path, bytesRead, 1);
                }

                return Result.Success()
                    .WithMessage($"Wrote file at '{path}'");
            }
            catch (OperationCanceledException)
            {
                return Result.Failure()
                    .WithError(new OperationCancelledError("Operation cancelled during write"))
                    .WithMessage($"Cancelled writing file at '{path}'");
            }
            catch (UnauthorizedAccessException ex)
            {
                return Result.Failure()
                    .WithError(new PermissionError("Access denied", path, ex))
                    .WithMessage($"Permission denied for file at '{path}'");
            }
            catch (IOException ex)
            {
                return Result.Failure()
                    .WithError(new FileSystemError("Disk or file system error", path, ex))
                    .WithMessage($"Failed to write file at '{path}'");
            }
            catch (Exception ex)
            {
                return Result.Failure()
                    .WithError(new ExceptionError(ex))
                    .WithMessage($"Unexpected error writing file at '{path}'");
            }
        }
        finally
        {
            this.ReleaseSemaphore(fullPath, semaphore);
        }
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

        var fullPath = this.GetFullPath(path);
        var semaphore = this.GetSemaphore(fullPath);

        try
        {
            if (!await semaphore.WaitAsync(this.lockTimeout, cancellationToken))
            {
                return Result.Failure()
                    .WithError(new TimeoutError("Timeout waiting for file access"))
                    .WithMessage($"Failed to acquire lock for deleting file at '{path}'");
            }

            if (!File.Exists(fullPath))
            {
                return Result.Failure()
                    .WithError(new FileSystemError("File not found", path))
                    .WithMessage($"Failed to delete file at '{path}'");
            }

            try
            {
                File.Delete(fullPath);
                this.ReportProgress(progress, path, 0, 1);
                return Result.Success()
                    .WithMessage($"Deleted file at '{path}'");
            }
            catch (UnauthorizedAccessException ex)
            {
                return Result.Failure()
                    .WithError(new PermissionError("Access denied", path, ex))
                    .WithMessage($"Permission denied for file at '{path}'");
            }
            catch (Exception ex)
            {
                return Result.Failure()
                    .WithError(new ExceptionError(ex))
                    .WithMessage($"Unexpected error deleting file at '{path}'");
            }
        }
        finally
        {
            this.ReleaseSemaphore(fullPath, semaphore);
        }
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
                .WithMessage($"Cancelled computing checksum for file at '{path}'");
        }

        var fullPath = this.GetFullPath(path);
        var semaphore = this.GetSemaphore(fullPath);

        try
        {
            if (!await semaphore.WaitAsync(this.lockTimeout, cancellationToken))
            {
                return Result<string>.Failure()
                    .WithError(new TimeoutError("Timeout waiting for file access"))
                    .WithMessage($"Failed to acquire lock for computing checksum at '{path}'");
            }

            if (!File.Exists(fullPath))
            {
                return Result<string>.Failure()
                    .WithError(new FileSystemError("File not found", path))
                    .WithMessage($"Failed to compute checksum for file at '{path}'");
            }

            try
            {
                using var sha256 = SHA256.Create();
                await using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                var hash = Convert.ToBase64String(await sha256.ComputeHashAsync(stream, cancellationToken));
                return Result<string>.Success(hash)
                    .WithMessage($"Computed checksum for file at '{path}'");
            }
            catch (UnauthorizedAccessException ex)
            {
                return Result<string>.Failure()
                    .WithError(new PermissionError("Access denied", path, ex))
                    .WithMessage($"Permission denied for file at '{path}'");
            }
            catch (Exception ex)
            {
                return Result<string>.Failure()
                    .WithError(new ExceptionError(ex))
                    .WithMessage($"Unexpected error computing checksum for file at '{path}'");
            }
        }
        finally
        {
            this.ReleaseSemaphore(fullPath, semaphore);
        }
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
                .WithMessage($"Cancelled retrieving metadata for file at '{path}'");
        }

        var fullPath = this.GetFullPath(path);
        var semaphore = this.GetSemaphore(fullPath);

        try
        {
            if (!await semaphore.WaitAsync(this.lockTimeout, cancellationToken))
            {
                return Result<FileMetadata>.Failure()
                    .WithError(new TimeoutError("Timeout waiting for file access"))
                    .WithMessage($"Failed to acquire lock for retrieving metadata at '{path}'");
            }

            if (!File.Exists(fullPath))
            {
                return Result<FileMetadata>.Failure()
                    .WithError(new FileSystemError("File not found", path))
                    .WithMessage($"Failed to retrieve metadata for file at '{path}'");
            }

            try
            {
                var info = new FileInfo(fullPath);
                var metadata = new FileMetadata
                {
                    Path = path,
                    Length = info.Length,
                    LastModified = info.LastWriteTimeUtc
                };
                return Result<FileMetadata>.Success(metadata)
                    .WithMessage($"Retrieved metadata for file at '{path}' from disk");
            }
            catch (UnauthorizedAccessException ex)
            {
                return Result<FileMetadata>.Failure()
                    .WithError(new PermissionError("Access denied", path, ex))
                    .WithMessage($"Permission denied for file at '{path}'");
            }
            catch (Exception ex)
            {
                return Result<FileMetadata>.Failure()
                    .WithError(new ExceptionError(ex))
                    .WithMessage($"Unexpected error retrieving metadata for file at '{path}'");
            }
        }
        finally
        {
            this.ReleaseSemaphore(fullPath, semaphore);
        }
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
                .WithMessage($"Cancelled setting metadata for file at '{path}'");
        }

        var fullPath = this.GetFullPath(path);
        var semaphore = this.GetSemaphore(fullPath);

        try
        {
            if (!await semaphore.WaitAsync(this.lockTimeout, cancellationToken))
            {
                return Result.Failure()
                    .WithError(new TimeoutError("Timeout waiting for file access"))
                    .WithMessage($"Failed to acquire lock for setting metadata at '{path}'");
            }

            if (!File.Exists(fullPath))
            {
                return Result.Failure()
                    .WithError(new FileSystemError("File not found", path))
                    .WithMessage($"Failed to set metadata for file at '{path}'");
            }

            try
            {
                if (metadata.LastModified.HasValue)
                {
                    File.SetLastWriteTimeUtc(fullPath, metadata.LastModified.Value);
                }
                return Result.Success()
                    .WithMessage($"Set metadata for file at '{path}'");
            }
            catch (UnauthorizedAccessException ex)
            {
                return Result.Failure()
                    .WithError(new PermissionError("Access denied", path, ex))
                    .WithMessage($"Permission denied for file at '{path}'");
            }
            catch (Exception ex)
            {
                return Result.Failure()
                    .WithError(new ExceptionError(ex))
                    .WithMessage($"Unexpected error setting metadata for file at '{path}'");
            }
        }
        finally
        {
            this.ReleaseSemaphore(fullPath, semaphore);
        }
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
                .WithMessage($"Cancelled updating metadata for file at '{path}'");
        }

        var fullPath = this.GetFullPath(path);

        if (!File.Exists(fullPath))
        {
            return Result<FileMetadata>.Failure()
                .WithError(new FileSystemError("File not found", path))
                .WithMessage($"Failed to update metadata for file at '{path}'");
        }

        try
        {
            var currentMetadata = await this.GetFileInfoAsync(path, cancellationToken);
            if (currentMetadata.IsFailure)
            {
                return Result<FileMetadata>.Failure()
                    .WithErrors(currentMetadata.Errors)
                    .WithMessages(currentMetadata.Messages);
            }

            var updatedMetadata = metadataUpdate(currentMetadata.Value);
            var setResult = await this.SetFileMetadataAsync(path, updatedMetadata, cancellationToken);
            if (setResult.IsFailure)
            {
                return Result<FileMetadata>.Failure()
                    .WithErrors(setResult.Errors)
                    .WithMessages(setResult.Messages);
            }

            return Result<FileMetadata>.Success(updatedMetadata)
                .WithMessage($"Updated metadata for file at '{path}'");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Result<FileMetadata>.Failure()
                .WithError(new PermissionError("Access denied", path, ex))
                .WithMessage($"Permission denied for file at '{path}'");
        }
        catch (Exception ex)
        {
            return Result<FileMetadata>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error updating metadata for file at '{path}'");
        }
    }

    public override Task<Result<(IEnumerable<string> Files, string NextContinuationToken)>> ListFilesAsync(
        string path, string searchPattern = null, bool recursive = false, string continuationToken = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(path))
        {
            return Task.FromResult(Result<(IEnumerable<string> Files, string NextContinuationToken)>.Failure()
                .WithError(new FileSystemError("Path cannot be null or empty", path))
                .WithMessage("Invalid path provided"));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<(IEnumerable<string> Files, string NextContinuationToken)>.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled listing files in '{path}'"));
        }

        var fullPath = this.GetFullPath(path);
        // Note: Directory listing doesn't lock individual files, so no semaphore is used here
        try
        {
            if (!Directory.Exists(fullPath))
            {
                return Task.FromResult(Result<(IEnumerable<string> Files, string NextContinuationToken)>.Failure()
                        .WithError(new FileSystemError("Directory not found", path))
                        .WithMessage($"Failed to list files in '{path}'"));
            }

            var files = Directory.EnumerateFiles(fullPath, searchPattern ?? "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .Select(f => this.GetRelativePath(f)) // Returns whole paths relative to rootPath (e.g., "test/file1.txt")
                .Order()
                .ToList();

            var startIndex = continuationToken != null ? int.Parse(continuationToken) : 0;
            const int PageSize = 100;
            var pagedFiles = files.Skip(startIndex).Take(PageSize).ToList();
            var nextToken = pagedFiles.Count == PageSize ? (startIndex + PageSize).ToString() : null;

            return Task.FromResult(Result<(IEnumerable<string> Files, string NextContinuationToken)>.Success((pagedFiles, nextToken))
                .WithMessage($"Listed files in '{path}' with pattern '{searchPattern}'"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Task.FromResult(Result<(IEnumerable<string> Files, string NextContinuationToken)>.Failure()
                .WithError(new PermissionError("Access denied", path, ex))
                .WithMessage($"Failed to list files in '{path}' due to permissions"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<(IEnumerable<string> Files, string NextContinuationToken)>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error listing files in '{path}'"));
        }
    }

    public override async Task<Result> CopyFileAsync(string sourcePath, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destinationPath))
        {
            return Result.Failure()
                .WithError(new FileSystemError("Source or destination path cannot be null or empty", $"{sourcePath ?? destinationPath}"))
                .WithMessage("Invalid paths provided");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled copying file from '{sourcePath}' to '{destinationPath}'");
        }

        var fullSource = this.GetFullPath(sourcePath);
        var fullDest = this.GetFullPath(destinationPath);
        var sourceSemaphore = this.GetSemaphore(fullSource);
        var destSemaphore = this.GetSemaphore(fullDest);

        try
        {
            if (!await sourceSemaphore.WaitAsync(this.lockTimeout, cancellationToken))
            {
                return Result.Failure()
                    .WithError(new TimeoutError("Timeout waiting for source file access"))
                    .WithMessage($"Failed to acquire lock for copying file from '{sourcePath}'");
            }

            if (!await destSemaphore.WaitAsync(this.lockTimeout, cancellationToken))
            {
                return Result.Failure()
                    .WithError(new TimeoutError("Timeout waiting for destination file access"))
                    .WithMessage($"Failed to acquire lock for copying file to '{destinationPath}'");
            }

            if (!File.Exists(fullSource))
            {
                return Result.Failure()
                    .WithError(new FileSystemError("Source file not found", sourcePath))
                    .WithMessage($"Failed to copy file from '{sourcePath}' to '{destinationPath}'");
            }

            try
            {
                var directory = Path.GetDirectoryName(fullDest);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.Copy(fullSource, fullDest, true);
                var length = new FileInfo(fullSource).Length;
                this.ReportProgress(progress, sourcePath, length, 1);
                return Result.Success()
                    .WithMessage($"Copied file from '{sourcePath}' to '{destinationPath}'");
            }
            catch (UnauthorizedAccessException ex)
            {
                return Result.Failure()
                    .WithError(new PermissionError("Access denied", sourcePath, ex))
                    .WithMessage($"Permission denied for file at '{sourcePath}' or '{destinationPath}'");
            }
            catch (IOException ex)
            {
                return Result.Failure()
                    .WithError(new FileSystemError("Disk or file system error", sourcePath, ex))
                    .WithMessage($"Failed to copy file from '{sourcePath}' to '{destinationPath}'");
            }
            catch (Exception ex)
            {
                return Result.Failure()
                    .WithError(new ExceptionError(ex))
                    .WithMessage($"Unexpected error copying file from '{sourcePath}' to '{destinationPath}'");
            }
        }
        finally
        {
            this.ReleaseSemaphore(fullSource, sourceSemaphore);
            this.ReleaseSemaphore(fullDest, destSemaphore);
        }
    }

    public override async Task<Result> RenameFileAsync(string oldPath, string newPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(oldPath) || string.IsNullOrEmpty(newPath))
        {
            return Result.Failure()
                .WithError(new FileSystemError("Old or new path cannot be null or empty", $"{oldPath ?? newPath}"))
                .WithMessage("Invalid paths provided");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled renaming file from '{oldPath}' to '{newPath}'");
        }

        var fullOld = this.GetFullPath(oldPath);
        var fullNew = this.GetFullPath(newPath);
        var oldSemaphore = this.GetSemaphore(fullOld);
        var newSemaphore = this.GetSemaphore(fullNew);

        try
        {
            if (!await oldSemaphore.WaitAsync(this.lockTimeout, cancellationToken))
            {
                return Result.Failure()
                    .WithError(new TimeoutError("Timeout waiting for old file access"))
                    .WithMessage($"Failed to acquire lock for renaming file from '{oldPath}'");
            }

            if (!await newSemaphore.WaitAsync(this.lockTimeout, cancellationToken))
            {
                return Result.Failure()
                    .WithError(new TimeoutError("Timeout waiting for new file access"))
                    .WithMessage($"Failed to acquire lock for renaming file to '{newPath}'");
            }

            if (!File.Exists(fullOld))
            {
                return Result.Failure()
                    .WithError(new FileSystemError("File not found", oldPath))
                    .WithMessage($"Failed to rename file from '{oldPath}' to '{newPath}'");
            }

            try
            {
                var directory = Path.GetDirectoryName(fullNew);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.Move(fullOld, fullNew, true);
                var length = new FileInfo(fullNew).Length;
                this.ReportProgress(progress, oldPath, length, 1);
                return Result.Success()
                    .WithMessage($"Renamed file from '{oldPath}' to '{newPath}'");
            }
            catch (UnauthorizedAccessException ex)
            {
                return Result.Failure()
                    .WithError(new PermissionError("Access denied", oldPath, ex))
                    .WithMessage($"Permission denied for file at '{oldPath}' or '{newPath}'");
            }
            catch (IOException ex)
            {
                return Result.Failure()
                    .WithError(new FileSystemError("Disk or file system error", oldPath, ex))
                    .WithMessage($"Failed to rename file from '{oldPath}' to '{newPath}'");
            }
            catch (Exception ex)
            {
                return Result.Failure()
                    .WithError(new ExceptionError(ex))
                    .WithMessage($"Unexpected error renaming file from '{oldPath}' to '{newPath}'");
            }
        }
        finally
        {
            this.ReleaseSemaphore(fullOld, oldSemaphore);
            this.ReleaseSemaphore(fullNew, newSemaphore);
        }
    }

    public override async Task<Result> MoveFileAsync(string sourcePath, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destinationPath))
        {
            return Result.Failure()
                .WithError(new FileSystemError("Source or destination path cannot be null or empty", $"{sourcePath ?? destinationPath}"))
                .WithMessage("Invalid paths provided");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled moving file from '{sourcePath}' to '{destinationPath}'");
        }

        var fullSource = this.GetFullPath(sourcePath);
        var fullDest = this.GetFullPath(destinationPath);
        var sourceSemaphore = this.GetSemaphore(fullSource);
        var destSemaphore = this.GetSemaphore(fullDest);

        try
        {
            if (!await sourceSemaphore.WaitAsync(this.lockTimeout, cancellationToken))
            {
                return Result.Failure()
                    .WithError(new TimeoutError("Timeout waiting for source file access"))
                    .WithMessage($"Failed to acquire lock for moving file from '{sourcePath}'");
            }

            if (!await destSemaphore.WaitAsync(this.lockTimeout, cancellationToken))
            {
                return Result.Failure()
                    .WithError(new TimeoutError("Timeout waiting for destination file access"))
                    .WithMessage($"Failed to acquire lock for moving file to '{destinationPath}'");
            }

            if (!File.Exists(fullSource))
            {
                return Result.Failure()
                    .WithError(new FileSystemError("Source file not found", sourcePath))
                    .WithMessage($"Failed to move file from '{sourcePath}' to '{destinationPath}'");
            }

            try
            {
                var directory = Path.GetDirectoryName(fullDest);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.Move(fullSource, fullDest, true);
                var length = new FileInfo(fullDest).Length;
                this.ReportProgress(progress, sourcePath, length, 1);
                return Result.Success()
                    .WithMessage($"Moved file from '{sourcePath}' to '{destinationPath}'");
            }
            catch (UnauthorizedAccessException ex)
            {
                return Result.Failure()
                    .WithError(new PermissionError("Access denied", sourcePath, ex))
                    .WithMessage($"Permission denied for file at '{sourcePath}' or '{destinationPath}'");
            }
            catch (IOException ex)
            {
                return Result.Failure()
                    .WithError(new FileSystemError("Disk or file system error", sourcePath, ex))
                    .WithMessage($"Failed to move file from '{sourcePath}' to '{destinationPath}'");
            }
            catch (Exception ex)
            {
                return Result.Failure()
                    .WithError(new ExceptionError(ex))
                    .WithMessage($"Unexpected error moving file from '{sourcePath}' to '{destinationPath}'");
            }
        }
        finally
        {
            this.ReleaseSemaphore(fullSource, sourceSemaphore);
            this.ReleaseSemaphore(fullDest, destSemaphore);
        }
    }

    public override async Task<Result> CopyFilesAsync(IEnumerable<(string SourcePath, string DestinationPath)> filePairs, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
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
                var metadata = await this.GetFileInfoAsync(source, cancellationToken);
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

    public override async Task<Result> MoveFilesAsync(IEnumerable<(string SourcePath, string DestinationPath)> filePairs, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
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
                var metadata = await this.GetFileInfoAsync(dest, cancellationToken);
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

    public override async Task<Result> DeleteFilesAsync(IEnumerable<string> paths, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
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

                var metadata = await this.GetFileInfoAsync(path, cancellationToken);
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

    public override Task<Result> IsDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(path))
        {
            return Task.FromResult(Result.Failure()
                .WithError(new FileSystemError("Path cannot be null or empty", path))
                .WithMessage("Invalid path provided"));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled checking if '{path}' is a directory"));
        }

        var fullPath = this.GetFullPath(path);
        // No file-specific lock needed for directory check
        try
        {
            var isDirectory = Directory.Exists(fullPath);
            if (!isDirectory)
            {
                return Task.FromResult(Result.Failure()
                    .WithError(new NotFoundError("Directory not found")));
            }

            return Task.FromResult(Result.Success()
                .WithMessage($"Checked if '{path}' is a directory"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error checking if '{path}' is a directory"));
        }
    }

    public override Task<Result> CreateDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(path))
        {
            return Task.FromResult(Result.Failure()
                .WithError(new FileSystemError("Path cannot be null or empty", path))
                .WithMessage("Invalid path provided"));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled creating directory at '{path}'"));
        }

        var fullPath = this.GetFullPath(path);
        // No file-specific lock needed for directory creation
        try
        {
            if (Directory.Exists(fullPath))
            {
                return Task.FromResult(Result.Failure()
                    .WithError(new FileSystemError("Directory already exists", path))
                    .WithMessage($"Failed to create directory at '{path}'"));
            }

            Directory.CreateDirectory(fullPath);

            return Task.FromResult(Result.Success()
                .WithMessage($"Created directory at '{path}'"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new PermissionError("Access denied", path, ex))
                .WithMessage($"Permission denied for path '{path}'"));
        }
        catch (IOException ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new FileSystemError("Disk or file system error", path, ex))
                .WithMessage($"Failed to create directory at '{path}'"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error creating directory at '{path}'"));
        }
    }

    public override Task<Result> DeleteDirectoryAsync(string path, bool recursive, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(path))
        {
            return Task.FromResult(Result.Failure()
                .WithError(new FileSystemError("Path cannot be null or empty", path))
                .WithMessage("Invalid path provided"));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled deleting directory at '{path}'"));
        }

        var fullPath = this.GetFullPath(path);
        // No file-specific lock needed for directory deletion
        try
        {
            if (!Directory.Exists(fullPath))
            {
                return Task.FromResult(Result.Failure()
                    .WithError(new FileSystemError("Directory not found", path))
                    .WithMessage($"Failed to delete directory at '{path}'"));
            }

            Directory.Delete(fullPath, recursive);
            return Task.FromResult(Result.Success()
                .WithMessage($"Deleted directory at '{path}'"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new PermissionError("Access denied", path, ex))
                .WithMessage($"Permission denied for path '{path}'"));
        }
        catch (IOException ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new FileSystemError("Directory not empty or disk error", path, ex))
                .WithMessage($"Failed to delete directory at '{path}'"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error deleting directory at '{path}'"));
        }
    }

    public override Task<Result<IEnumerable<string>>> ListDirectoriesAsync(
        string path, string searchPattern = null, bool recursive = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(path))
        {
            return Task.FromResult(Result<IEnumerable<string>>.Failure()
                .WithError(new FileSystemError("Path cannot be null or empty", path))
                .WithMessage("Invalid path provided"));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<IEnumerable<string>>.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled listing directories in '{path}'"));
        }

        var fullPath = this.GetFullPath(path);
        // No file-specific lock needed for directory listing
        try
        {
            if (!Directory.Exists(fullPath))
            {
                return Task.FromResult(Result<IEnumerable<string>>.Failure()
                    .WithError(new FileSystemError("Directory not found", path))
                    .WithMessage($"Failed to list directories in '{path}'"));
            }

            var directories = Directory.EnumerateDirectories(fullPath, searchPattern ?? "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .Select(d => this.GetRelativePath(d))
                .Order()
                .ToList();

            return Task.FromResult(Result<IEnumerable<string>>.Success(directories)
                .WithMessage($"Listed directories in '{path}' with pattern '{searchPattern}'"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Task.FromResult(Result<IEnumerable<string>>.Failure()
                .WithError(new PermissionError("Access denied", path, ex))
                .WithMessage($"Failed to list directories in '{path}' due to permissions"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<IEnumerable<string>>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error listing directories in '{path}'"));
        }
    }

    public override async Task<Result> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled checking health of local storage at '{this.LocationName}'");
        }

        if (!Directory.Exists(this.rootPath))
        {
            return Result.Failure()
                .WithError(new FileSystemError("Root directory not found", this.rootPath))
                .WithMessage($"Failed to check health of local storage at '{this.LocationName}'");
        }

        //var testPath = Path.Combine(this.rootPath, "healthcheck.txt");
        //var fullTestPath = this.GetFullPath("healthcheck.txt");
        try
        {
            await using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("Health Check")))
            {
                var writeResult = await this.WriteFileAsync("healthcheck.txt", stream, null, cancellationToken);
                if (writeResult.IsFailure)
                {
                    return writeResult;
                }

                var readResult = await this.ReadFileAsync("healthcheck.txt", null, cancellationToken);
                if (readResult.IsFailure)
                {
                    return readResult;
                }

                await this.DeleteFileAsync("healthcheck.txt", null, cancellationToken);
            }

            return Result.Success()
                .WithMessage($"Local storage at '{this.LocationName}' is healthy");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Result.Failure()
                .WithError(new PermissionError("Access denied", this.rootPath, ex))
                .WithMessage($"Permission denied for local storage at '{this.LocationName}'");
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error checking health of local storage at '{this.LocationName}'");
        }
    }

    // Helper methods for semaphore management
    private SemaphoreSlim GetSemaphore(string path)
    {
        return this.fileLocks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
    }

    private void ReleaseSemaphore(string path, SemaphoreSlim semaphore)
    {
        semaphore.Release();
        if (semaphore.CurrentCount == 1)
        {
            this.fileLocks.TryRemove(path, out _);
        }
    }

    // Dispose pattern to clean up resources
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (this.disposed)
        {
            return;
        }

        if (disposing)
        {
            foreach (var semaphore in this.fileLocks.Values)
            {
                semaphore.Dispose();
            }
            this.fileLocks.Clear();
        }

        this.disposed = true;
    }

    private string GetFullPath(string path) => Path.Combine(this.rootPath, path.Replace("/", "\\").TrimStart('\\'));

    private string GetRelativePath(string fullPath) => fullPath[this.rootPath.Length..].TrimStart('\\').Replace("\\", "/");

    private void ReportProgress(IProgress<FileProgress> progress, string path, long bytesProcessed, long filesProcessed, long totalFiles = 1)
    {
        progress?.Report(new FileProgress
        {
            BytesProcessed = bytesProcessed,
            FilesProcessed = filesProcessed,
            TotalFiles = totalFiles
        });
    }
}