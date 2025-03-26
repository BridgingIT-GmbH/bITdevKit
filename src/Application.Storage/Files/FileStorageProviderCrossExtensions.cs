// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BridgingIT.DevKit.Common;

public static class FileStorageProviderCrossExtensions
{
    /// <summary>
    /// Copies a file from a source provider to a destination provider.
    /// </summary>
    public static async Task<Result> CopyFileAsync(
        this IFileStorageProvider sourceProvider,
        IFileStorageProvider destinationProvider,
        string sourcePath,
        string destinationPath,
        IProgress<FileProgress> progress = null,
        CancellationToken cancellationToken = default)
    {
        if (sourceProvider == null || destinationProvider == null)
        {
            return Result.Failure()
                .WithError(new ArgumentError("Source or destination provider cannot be null"))
                .WithMessage("Invalid provider provided for cross-provider write");
        }

        if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destinationPath))
        {
            return Result.Failure()
                .WithError(new FileSystemError("Source or destination path cannot be null or empty", $"{sourcePath} -> {destinationPath}"))
                .WithMessage("Invalid paths provided for cross-provider write");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled writing file from '{sourcePath}' to '{destinationPath}'");
        }

        try
        {
            // Read the file from the source provider
            var readResult = await sourceProvider.ReadFileAsync(sourcePath, null, cancellationToken);
            if (readResult.IsFailure)
            {
                return Result.Failure()
                    .WithErrors(readResult.Errors)
                    .WithMessages(readResult.Messages);
            }

            // Write the file to the destination provider
            await using var stream = readResult.Value;
            var writeResult = await destinationProvider.WriteFileAsync(destinationPath, stream, null, cancellationToken);
            if (writeResult.IsFailure)
            {
                return Result.Failure()
                    .WithErrors(writeResult.Errors)
                    .WithMessages(writeResult.Messages);
            }

            // Report progress (total bytes processed)
            var metadataResult = await sourceProvider.GetFileMetadataAsync(sourcePath, cancellationToken);
            if (metadataResult.IsSuccess)
            {
                progress?.Report(new FileProgress { BytesProcessed = metadataResult.Value.Length });
            }

            return Result.Success()
                .WithMessage($"Wrote file from '{sourcePath}' (source provider) to '{destinationPath}' (destination provider)");
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during cross-provider write"))
                .WithMessage($"Cancelled writing file from '{sourcePath}' to '{destinationPath}'");
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error writing file from '{sourcePath}' to '{destinationPath}'");
        }
    }

    /// <summary>
    /// Copies multiple files from a source provider to a destination provider.
    /// </summary>
    public static async Task<Result> CopyFilesAsync(
        this IFileStorageProvider sourceProvider,
        IFileStorageProvider destinationProvider,
        IEnumerable<(string SourcePath, string DestinationPath)> filePairs,
        IProgress<FileProgress> progress = null,
        CancellationToken cancellationToken = default)
    {
        if (sourceProvider == null || destinationProvider == null)
        {
            return Result.Failure()
                .WithError(new ArgumentError("Source or destination provider cannot be null"))
                .WithMessage("Invalid provider provided for cross-provider write");
        }

        if (filePairs?.Any() != true)
        {
            return Result.Failure()
                .WithError(new ArgumentError("File pairs cannot be null or empty"))
                .WithMessage("Invalid file pairs provided for cross-provider write");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage("Cancelled writing multiple files");
        }

        try
        {
            var errors = new List<IResultError>();
            long totalBytes = 0;
            var filesProcessed = 0;
            var totalFiles = filePairs.Count();

            foreach (var (sourcePath, destPath) in filePairs)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return Result.Failure()
                        .WithError(new OperationCancelledError("Operation cancelled during cross-provider write"))
                        .WithMessage($"Cancelled writing files after processing {filesProcessed}/{totalFiles} files");
                }

                var writeResult = await sourceProvider.CopyFileAsync(destinationProvider, sourcePath, destPath, null, cancellationToken);
                if (writeResult.IsFailure)
                {
                    errors.AddRange(writeResult.Errors);
                    continue;
                }

                filesProcessed++;
                var metadataResult = await sourceProvider.GetFileMetadataAsync(sourcePath, cancellationToken);
                if (metadataResult.IsSuccess)
                {
                    totalBytes += metadataResult.Value.Length;
                }

                progress?.Report(new FileProgress { BytesProcessed = totalBytes, FilesProcessed = filesProcessed, TotalFiles = totalFiles });
            }

            if (errors.Count > 0)
            {
                return Result.Failure()
                    .WithErrors(errors)
                    .WithMessage($"Failed to write some files: {filesProcessed}/{totalFiles} succeeded");
            }

            return Result.Success()
                .WithMessage($"Wrote all {totalFiles} files from source provider to destination provider");
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during cross-provider write"))
                .WithMessage("Cancelled writing multiple files");
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage("Unexpected error writing multiple files");
        }
    }

    /// <summary>
    /// Moves a file from a source provider to a destination provider by copying and then deleting the source.
    /// </summary>
    public static async Task<Result> MoveFileAsync(
        this IFileStorageProvider sourceProvider,
        IFileStorageProvider destinationProvider,
        string sourcePath,
        string destinationPath,
        IProgress<FileProgress> progress = null,
        CancellationToken cancellationToken = default)
    {
        if (sourceProvider == null || destinationProvider == null)
        {
            return Result.Failure()
                .WithError(new ArgumentError("Source or destination provider cannot be null"))
                .WithMessage("Invalid provider provided for cross-provider move");
        }

        if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destinationPath))
        {
            return Result.Failure()
                .WithError(new FileSystemError("Source or destination path cannot be null or empty", $"{sourcePath} -> {destinationPath}"))
                .WithMessage("Invalid paths provided for cross-provider move");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled moving file from '{sourcePath}' to '{destinationPath}'");
        }

        try
        {
            // Copy the file from source to destination
            var copyResult = await sourceProvider.CopyFileAsync(destinationProvider, sourcePath, destinationPath, progress, cancellationToken);
            if (copyResult.IsFailure)
            {
                return Result.Failure()
                    .WithErrors(copyResult.Errors)
                    .WithMessages(copyResult.Messages);
            }

            // Delete the file from the source provider
            var deleteResult = await sourceProvider.DeleteFileAsync(sourcePath, null, cancellationToken);
            if (deleteResult.IsFailure)
            {
                return Result.Failure()
                    .WithErrors(deleteResult.Errors)
                    .WithMessages(deleteResult.Messages)
                    .WithMessage("Failed to delete source file after copying; destination file may still exist");
            }

            return Result.Success()
                .WithMessage($"Moved file from '{sourcePath}' (source provider) to '{destinationPath}' (destination provider)");
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during cross-provider move"))
                .WithMessage($"Cancelled moving file from '{sourcePath}' to '{destinationPath}'");
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error moving file from '{sourcePath}' to '{destinationPath}'");
        }
    }

    /// <summary>
    /// Moves multiple files from a source provider to a destination provider.
    /// </summary>
    public static async Task<Result> MoveFilesAsync(
        this IFileStorageProvider sourceProvider,
        IFileStorageProvider destinationProvider,
        IEnumerable<(string SourcePath, string DestinationPath)> filePairs,
        IProgress<FileProgress> progress = null,
        CancellationToken cancellationToken = default)
    {
        if (sourceProvider == null || destinationProvider == null)
        {
            return Result.Failure()
                .WithError(new ArgumentError("Source or destination provider cannot be null"))
                .WithMessage("Invalid provider provided for cross-provider move");
        }

        if (filePairs?.Any() != true)
        {
            return Result.Failure()
                .WithError(new ArgumentError("File pairs cannot be null or empty"))
                .WithMessage("Invalid file pairs provided for cross-provider move");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage("Cancelled moving multiple files");
        }

        try
        {
            var errors = new List<IResultError>();
            long totalBytes = 0;
            var filesProcessed = 0;
            var totalFiles = filePairs.Count();

            foreach (var (sourcePath, destPath) in filePairs)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return Result.Failure()
                        .WithError(new OperationCancelledError("Operation cancelled during cross-provider move"))
                        .WithMessage($"Cancelled moving files after processing {filesProcessed}/{totalFiles} files");
                }

                var moveResult = await sourceProvider.MoveFileAsync(destinationProvider, sourcePath, destPath, null, cancellationToken);
                if (moveResult.IsFailure)
                {
                    errors.AddRange(moveResult.Errors);
                    continue;
                }

                filesProcessed++;
                var metadataResult = await sourceProvider.GetFileMetadataAsync(sourcePath, cancellationToken);
                if (metadataResult.IsSuccess)
                {
                    totalBytes += metadataResult.Value.Length;
                }

                progress?.Report(new FileProgress { BytesProcessed = totalBytes, FilesProcessed = filesProcessed, TotalFiles = totalFiles });
            }

            if (errors.Count > 0)
            {
                return Result.Failure()
                    .WithErrors(errors)
                    .WithMessage($"Failed to move some files: {filesProcessed}/{totalFiles} succeeded");
            }

            return Result.Success()
                .WithMessage($"Moved all {totalFiles} files from source provider to destination provider");
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during cross-provider move"))
                .WithMessage("Cancelled moving multiple files");
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage("Unexpected error moving multiple files");
        }
    }

    /// <summary>
    /// Performs a deep copy of a file or directory structure from a source provider to a destination provider.
    /// </summary>
    public static async Task<Result> DeepCopyAsync(
        this IFileStorageProvider sourceProvider,
        IFileStorageProvider destinationProvider,
        string sourcePath,
        string destinationPath,
        bool skipFiles = false,
        string searchPattern = null,
        IProgress<FileProgress> progress = null,
        CancellationToken cancellationToken = default)
    {
        if (sourceProvider == null || destinationProvider == null)
        {
            return Result.Failure()
                .WithError(new ArgumentError("Source or destination provider cannot be null"))
                .WithMessage("Invalid provider provided for cross-provider deep copy");
        }

        if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destinationPath))
        {
            return Result.Failure()
                .WithError(new FileSystemError("Source or destination path cannot be null or empty", $"{sourcePath} -> {destinationPath}"))
                .WithMessage("Invalid paths provided for cross-provider deep copy");
        }

        if (sourcePath.Equals(destinationPath, StringComparison.OrdinalIgnoreCase) && sourceProvider == destinationProvider)
        {
            return Result.Failure()
                .WithError(new FileSystemError("Source and destination paths cannot be the same", sourcePath))
                .WithMessage("Source and destination paths must be different for deep copying");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled deep copying from '{sourcePath}' to '{destinationPath}'");
        }

        try
        {
            // Check if the source exists (file or directory)
            var fileExistsResult = await sourceProvider.FileExistsAsync(sourcePath, cancellationToken: cancellationToken);
            var directoryExistsResult = await sourceProvider.DirectoryExistsAsync(sourcePath, cancellationToken);

            if (fileExistsResult.IsFailure && directoryExistsResult.IsFailure)
            {
                return Result.Failure()
                    .WithErrors(fileExistsResult.Errors.Concat(directoryExistsResult.Errors))
                    .WithMessages(fileExistsResult.Messages.Concat(directoryExistsResult.Messages));
            }

            // Handle single file copy
            if (fileExistsResult.IsSuccess)
            {
                if (skipFiles)
                {
                    return Result.Success()
                        .WithMessage($"Skipped copying file from '{sourcePath}' to '{destinationPath}' as skipFiles is true");
                }

                // Check if the file matches the search pattern (if provided)
                if (!string.IsNullOrEmpty(searchPattern) && !Path.GetFileName(sourcePath).Match(searchPattern))
                {
                    return Result.Success()
                        .WithMessage($"Skipped copying file from '{sourcePath}' to '{destinationPath}' as it does not match the search pattern '{searchPattern}'");
                }

                // Copy the single file
                var copyResult = await sourceProvider.CopyFileAsync(destinationProvider, sourcePath, destinationPath, progress, cancellationToken);
                if (copyResult.IsFailure)
                {
                    return copyResult;
                }

                return Result.Success()
                    .WithMessage($"Deep copied file from '{sourcePath}' (source provider) to '{destinationPath}' (destination provider)");
            }

            // Handle directory copy
            // List all directories recursively under the source path, including empty ones
            var dirListResult = await sourceProvider.ListDirectoriesAsync(sourcePath, null, true, cancellationToken);
            if (dirListResult.IsFailure)
            {
                return Result.Failure()
                    .WithErrors(dirListResult.Errors)
                    .WithMessages(dirListResult.Messages);
            }

            var directories = dirListResult.Value?.ToList() ?? [];
            directories.Insert(0, sourcePath); // Include the root directory

            // List all files recursively under the source path, applying the search pattern
            var fileListResult = await sourceProvider.ListFilesAsync(sourcePath, searchPattern ?? "*.*", true, null, cancellationToken);
            if (fileListResult.IsFailure)
            {
                return Result.Failure()
                    .WithErrors(fileListResult.Errors)
                    .WithMessages(fileListResult.Messages);
            }

            var files = fileListResult.Value.Files?.ToList() ?? [];
            var totalItems = directories.Count + (skipFiles ? 0 : files.Count); // Total items = directories + filtered files (if not skipped)
            long totalBytes = 0;
            long itemsProcessed = 0;
            var failedPaths = new List<string>();

            // Create directory structure
            foreach (var dir in directories.OrderBy(d => d.Length)) // Order by length to ensure parent directories are created first
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return Result.Failure()
                        .WithError(new OperationCancelledError("Operation cancelled during cross-provider deep copy"))
                        .WithMessage($"Cancelled deep copying from '{sourcePath}' to '{destinationPath}' after processing {itemsProcessed}/{totalItems} items");
                }

                // Compute the relative path and destination directory path
                var relativePath = dir.StartsWith(sourcePath, StringComparison.OrdinalIgnoreCase)
                    ? dir[sourcePath.Length..].TrimStart('/')
                    : dir;
                var destDirPath = string.IsNullOrEmpty(relativePath)
                    ? destinationPath
                    : Path.Combine(destinationPath, relativePath).Replace("\\", "/");

                // Create the directory at the destination
                var createDirResult = await destinationProvider.CreateDirectoryAsync(destDirPath, cancellationToken);
                if (createDirResult.IsFailure)
                {
                    failedPaths.Add(dir);
                    continue;
                }

                itemsProcessed++;
                progress?.Report(new FileProgress { BytesProcessed = totalBytes, FilesProcessed = itemsProcessed, TotalFiles = totalItems });
            }

            // Copy files if not skipping
            if (!skipFiles)
            {
                foreach (var file in files)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return Result.Failure()
                            .WithError(new OperationCancelledError("Operation cancelled during cross-provider deep copy"))
                            .WithMessage($"Cancelled deep copying from '{sourcePath}' to '{destinationPath}' after processing {itemsProcessed}/{totalItems} items");
                    }

                    // Compute the relative path and destination file path
                    var relativePath = file.StartsWith(sourcePath, StringComparison.OrdinalIgnoreCase)
                        ? file[sourcePath.Length..].TrimStart('/')
                        : file;
                    var destFilePath = Path.Combine(destinationPath, relativePath).Replace("\\", "/");

                    // Copy the file
                    var copyResult = await sourceProvider.CopyFileAsync(destinationProvider, file, destFilePath, null, cancellationToken);
                    if (copyResult.IsFailure)
                    {
                        failedPaths.Add(file);
                        continue;
                    }

                    itemsProcessed++;
                    var metadataResult = await sourceProvider.GetFileMetadataAsync(file, cancellationToken);
                    if (metadataResult.IsSuccess)
                    {
                        totalBytes += metadataResult.Value.Length;
                    }

                    progress?.Report(new FileProgress { BytesProcessed = totalBytes, FilesProcessed = itemsProcessed, TotalFiles = totalItems });
                }
            }

            if (failedPaths.Count > 0)
            {
                return Result.Failure()
                    .WithError(new PartialOperationError("Partial deep copy failure", failedPaths))
                    .WithMessage($"Deep copied {itemsProcessed}/{totalItems} items from '{sourcePath}' to '{destinationPath}', {failedPaths.Count} failed");
            }

            return Result.Success()
                .WithMessage($"Deep copied structure from '{sourcePath}' (source provider) to '{destinationPath}' (destination provider){(skipFiles ? " (files skipped)" : string.IsNullOrEmpty(searchPattern) ? "" : $" (filtered by '{searchPattern}')")}");
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during cross-provider deep copy"))
                .WithMessage($"Cancelled deep copying from '{sourcePath}' to '{destinationPath}'");
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error deep copying from '{sourcePath}' to '{destinationPath}'");
        }
    }
}