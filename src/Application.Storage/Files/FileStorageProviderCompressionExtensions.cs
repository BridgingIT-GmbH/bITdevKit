// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System;
using System.IO;
using System.Threading;
using BridgingIT.DevKit.Common;
using ICSharpCode.SharpZipLib.Zip;

/// <summary>
/// Extension methods for IFileStorageProvider to add compression.
/// </summary>
public static class FileStorageProviderCompressionExtensions
{
    /// <summary>
    /// Writes a file to the storage provider with ZIP compression, optionally password-protected using SharpZipLib (PKZIP or AES-256).
    /// </summary>
    /// <remarks>
    /// This implementation uses SharpZipLib to create ZIP files with password protection compatible with popular ZIP tools (e.g., 7-Zip, WinZip).
    /// It supports PKZIP encryption (legacy) or AES-256 encryption, depending on SharpZipLib's configuration.
    /// </remarks>
    public static async Task<Result> CompressAsync(this IFileStorageProvider provider, string path, Stream content, string password = null, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        if (provider == null)
        {
            return Result.Failure()
                .WithError(new ArgumentError("Provider cannot be null"))
                .WithMessage("Invalid provider provided for writing compressed file");
        }

        if (string.IsNullOrEmpty(path))
        {
            return Result.Failure()
                .WithError(new FileSystemError("Path cannot be null or empty", path))
                .WithMessage("Invalid path provided for writing compressed file");
        }

        if (content == null)
        {
            return Result.Failure()
                .WithError(new ArgumentError("Content stream cannot be null"))
                .WithMessage("Invalid content provided for writing compressed file");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled writing compressed file at '{path}'");
        }

        try
        {
            var zipStream = new MemoryStream();
            await using (var zipOutputStream = new ZipOutputStream(zipStream) { IsStreamOwner = false })
            {
                if (!string.IsNullOrEmpty(password))
                {
                    zipOutputStream.Password = password; // Sets password for PKZIP or AES encryption (SharpZipLib defaults to PKZIP unless configured for AES)
                    zipOutputStream.UseZip64 = UseZip64.Off; // Optional: Disable Zip64 for compatibility with older tools
                }
                zipOutputStream.SetLevel(9); // Maximum compression level (0-9)

                var entry = new ZipEntry("data");
                zipOutputStream.PutNextEntry(entry);

                await content.CopyToAsync(zipOutputStream, 8192, cancellationToken);
                await zipOutputStream.FlushAsync(cancellationToken);
                zipOutputStream.CloseEntry();

                await zipOutputStream.FinishAsync(cancellationToken);
                //await zipStream.FlushAsync(cancellationToken);
                zipStream.Position = 0; // Reset position for writing
                var length = zipStream.Length;

                ReportProgress(progress, path, length, 1); // Report compressed size
            }

            return await provider.WriteFileAsync(path, zipStream, progress, cancellationToken)
                .ContinueWith(task =>
                {
                    if (task.IsFaulted || task.IsCanceled)
                    {
                        return Result.Failure()
                            .WithErrors(task.Result.Errors)
                            .WithMessages(task.Result.Messages);
                    }
                    return task.Result.WithMessage(!string.IsNullOrEmpty(password)
                        ? $"Password-protected compressed and wrote file at '{path}'"
                        : $"Compressed and wrote file at '{path}'");
                }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during compression"))
                .WithMessage($"Cancelled compressing file at '{path}'");
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error compressing file at '{path}'");
        }
    }

    /// <summary>
    /// Writes a ZIP file to the storage provider by compressing all files and subdirectories in a directory, optionally password-protected using SharpZipLib (PKZIP or AES-256).
    /// </summary>
    /// <remarks>
    /// This implementation uses SharpZipLib to create a ZIP file from a directory path in the storage provider, maintaining compatibility with popular ZIP tools (e.g., 7-Zip, WinZip).
    /// It supports PKZIP encryption (legacy) or AES-256 encryption, depending on SharpZipLib's configuration.
    /// </remarks>
    /// <summary>
    /// Writes a ZIP file to the storage provider by compressing all files and subdirectories in a directory, optionally password-protected using SharpZipLib (PKZIP or AES-256).
    /// </summary>
    /// <remarks>
    /// This implementation uses SharpZipLib to create a ZIP file from a directory path in the storage provider, maintaining compatibility with popular ZIP tools (e.g., 7-Zip, WinZip).
    /// It supports PKZIP encryption (legacy) or AES-256 encryption, depending on SharpZipLib's configuration.
    /// </remarks>
    public static async Task<Result> CompressAsync(this IFileStorageProvider provider, string path, string inputPath, string password = null, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        if (provider == null)
        {
            return Result.Failure()
                .WithError(new ArgumentError("Provider cannot be null"))
                .WithMessage("Invalid provider provided for writing compressed directory");
        }

        if (string.IsNullOrEmpty(path))
        {
            return Result.Failure()
                .WithError(new FileSystemError("Path cannot be null or empty", path))
                .WithMessage("Invalid path provided for writing compressed file");
        }

        if (string.IsNullOrEmpty(inputPath))
        {
            return Result.Failure()
                .WithError(new FileSystemError("Directory path (content) cannot be null or empty", inputPath))
                .WithMessage("Invalid directory path provided for reading content");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled writing compressed directory to '{path}'");
        }

        var fileResult = await provider.FileExistsAsync(inputPath, cancellationToken: cancellationToken);
        var directoryResult = await provider.DirectoryExistsAsync(inputPath, cancellationToken);

        if (fileResult.IsFailure && directoryResult.IsFailure)
        {
            return Result.Failure()
                .WithErrors(directoryResult.Errors)
                .WithMessages(directoryResult.Messages);
        }

        try
        {
            if (fileResult.IsSuccess) // zip single file
            {
                return await CompressFile(provider, path, inputPath, password, progress, cancellationToken);
            }
            else // zip full directory
            {
                return await CompressDirectory(provider, path, inputPath, password, progress, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during compression"))
                .WithMessage($"Cancelled compressing '{inputPath}' to '{path}'");
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error '{inputPath}' to '{path}'");
        }

        static async Task<Result> CompressFile(IFileStorageProvider provider, string path, string inputPath, string password, IProgress<FileProgress> progress, CancellationToken cancellationToken)
        {
            var zipStream = new MemoryStream();
            await using (var zipOutputStream = new ZipOutputStream(zipStream) { IsStreamOwner = false })
            {
                if (!string.IsNullOrEmpty(password))
                {
                    zipOutputStream.Password = password;
                    zipOutputStream.UseZip64 = UseZip64.Off;
                }
                zipOutputStream.SetLevel(9);

                var fileInfoResult = await provider.GetFileMetadataAsync(inputPath, cancellationToken);
                if (fileInfoResult.IsFailure) return fileInfoResult;

                var fileName = Path.GetFileName(inputPath);
                var entry = new ZipEntry(fileName);
                zipOutputStream.PutNextEntry(entry);

                await using var fileStream = (await provider.ReadFileAsync(inputPath, progress, cancellationToken)).Value;
                fileStream.Position = 0;
                await fileStream.CopyToAsync(zipOutputStream, 8192, cancellationToken);

                zipOutputStream.CloseEntry();
                ReportProgress(progress, inputPath, fileInfoResult.Value.Length, 1, 1);

                await zipOutputStream.FinishAsync(cancellationToken);
                zipStream.Position = 0;
            }

            return await provider.WriteFileAsync(path, zipStream, progress, cancellationToken)
                .ContinueWith(task =>
                {
                    if (task.IsFaulted || task.IsCanceled)
                    {
                        return Result.Failure()
                            .WithErrors(task.Result.Errors)
                            .WithMessages(task.Result.Messages);
                    }
                    return task.Result.WithMessage(!string.IsNullOrEmpty(password)
                        ? $"Password-protected compressed '{inputPath}' to '{path}'"
                        : $"Compressed '{inputPath}' to '{path}'");
                }, cancellationToken);
        }

        static async Task<Result> CompressDirectory(IFileStorageProvider provider, string path, string inputPath, string password, IProgress<FileProgress> progress, CancellationToken cancellationToken)
        {
            // zip full directory
            var zipStream = new MemoryStream();
            await using (var zipOutputStream = new ZipOutputStream(zipStream) { IsStreamOwner = false })
            {
                if (!string.IsNullOrEmpty(password))
                {
                    zipOutputStream.Password = password; // Sets password for PKZIP or AES encryption
                    zipOutputStream.UseZip64 = UseZip64.Off; // Optional: Disable Zip64 for compatibility with older tools
                }
                zipOutputStream.SetLevel(9); // Maximum compression level (0-9)

                // List all files and directories recursively
                var listResult = await provider.ListFilesAsync(inputPath, "*.*", true, null, cancellationToken);
                if (listResult.Value.Files?.Any() != true)
                {
                    return Result.Failure()
                        .WithError(new FileSystemError("No files found in directory", inputPath))
                        .WithMessage($"No files found in directory '{inputPath}' for compression");
                }

                long totalBytes = 0;
                long filesProcessed = 0;
                var totalFiles = listResult.Value.Files.Count();

                foreach (var file in listResult.Value.Files)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return Result.Failure()
                            .WithError(new OperationCancelledError("Operation cancelled during compression"))
                            .WithMessage($"Cancelled compressing '{inputPath}' after processing {filesProcessed}/{totalFiles} files");
                    }

                    var fileInfoResult = await provider.GetFileMetadataAsync(file, cancellationToken);
                    if (fileInfoResult.IsFailure)
                    {
                        continue; // Skip files that can't be accessed, but continue with others
                    }

                    // Calculate the relative path correctly by ensuring consistent path separators and trimming the directory prefix
                    var relativePath = GetRelativePath(file, inputPath);
                    var entry = new ZipEntry(relativePath);
                    zipOutputStream.PutNextEntry(entry);

                    await using var fileStream = (await provider.ReadFileAsync(file, progress, cancellationToken)).Value;
                    fileStream.Position = 0;
                    await fileStream.CopyToAsync(zipOutputStream, 8192, cancellationToken);

                    zipOutputStream.CloseEntry();
                    //await zipOutputStream.FlushAsync(cancellationToken);
                    totalBytes += fileInfoResult.Value.Length;
                    filesProcessed++;

                    ReportProgress(progress, file, totalBytes, filesProcessed, totalFiles); // Report progress for each file
                }

                await zipOutputStream.FinishAsync(cancellationToken);
                //await zipStream.FlushAsync(cancellationToken);
                zipStream.Position = 0; // Reset position for writing
                                        //var length = zipStream.Length;
                                        //ReportProgress(progress, path, length, filesProcessed); // Report total compressed size
            }

            return await provider.WriteFileAsync(path, zipStream, progress, cancellationToken)
                .ContinueWith(task =>
                {
                    if (task.IsFaulted || task.IsCanceled)
                    {
                        return Result.Failure()
                            .WithErrors(task.Result.Errors)
                            .WithMessages(task.Result.Messages);
                    }
                    return task.Result.WithMessage(!string.IsNullOrEmpty(password)
                        ? $"Password-protected compressed '{inputPath}' to '{path}'"
                        : $"Compressed '{inputPath}' to '{path}'");
                }, cancellationToken);
        }
    }

    /// <summary>
    /// Reads a ZIP file from the storage provider and uncompresses it to a directory in the storage provider, optionally handling password-protected compression using SharpZipLib.
    /// </summary>
    /// <remarks>
    /// This implementation uses SharpZipLib to read and extract ZIP files with password protection compatible with popular ZIP tools (e.g., 7-Zip, WinZip).
    /// It supports PKZIP encryption (legacy) or AES-256 encryption, depending on the original file's configuration.
    /// </remarks>
    public static async Task<Result> UncompressAsync(this IFileStorageProvider provider, string path, string destinationPath, string password = null, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        if (provider == null)
        {
            return Result.Failure()
                .WithError(new ArgumentError("Provider cannot be null"))
                .WithMessage("Invalid provider provided for uncompressing file");
        }

        if (string.IsNullOrEmpty(path))
        {
            return Result.Failure()
                .WithError(new FileSystemError("Path cannot be null or empty", path))
                .WithMessage("Invalid path provided for uncompressing file");
        }

        if (string.IsNullOrEmpty(destinationPath))
        {
            return Result.Failure()
                .WithError(new FileSystemError("Destination path cannot be null or empty", destinationPath))
                .WithMessage("Invalid destination path provided for uncompressing file");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled uncompressing file at '{path}' to '{destinationPath}'");
        }

        // Ensure the destination directory exists
        var createDirResult = await provider.CreateDirectoryAsync(destinationPath, cancellationToken);
        if (createDirResult.IsFailure)
        {
            return Result.Failure()
                .WithErrors(createDirResult.Errors)
                .WithMessages(createDirResult.Messages);
        }

        var existsResult = await provider.FileExistsAsync(path, cancellationToken: cancellationToken);
        if (existsResult.IsFailure)
        {
            return existsResult; // notfounderror
        }

        try
        {
            var readResult = await provider.ReadFileAsync(path, progress, cancellationToken);
            if (readResult.IsFailure)
            {
                return readResult;
            }

            await using var zipStream = readResult.Value;
            await using var zipInputStream = new ZipInputStream(zipStream)
            {
                Password = password // Sets password for decryption (PKZIP or AES)
            };

            long totalBytes = 0;
            long filesProcessed = 0;
            ZipEntry entry;
            while (/*zipInputStream.CanDecompressEntry &&*/ (entry = zipInputStream.GetNextEntry()) != null)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return Result.Failure()
                        .WithError(new OperationCancelledError("Operation cancelled during file uncompression"))
                        .WithMessage($"Cancelled uncompressing file at '{path}' to '{destinationPath}' after processing {filesProcessed} files");
                }

                var entryPath = Path.Combine(destinationPath, entry.Name).Replace("\\", "/");
                var directory = Path.GetDirectoryName(entryPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    var createDirResultInner = await provider.CreateDirectoryAsync(directory, cancellationToken);
                    if (createDirResultInner.IsFailure)
                    {
                        continue; // Skip files in directories that can't be created, but continue with others
                    }
                }

                await using var memoryStream = new MemoryStream();
                await zipInputStream.CopyToAsync(memoryStream, 8192, cancellationToken);
                memoryStream.Position = 0;

                var length = entry.Size;
                await provider.WriteFileAsync(entryPath, memoryStream, progress, cancellationToken);

                totalBytes += length;
                filesProcessed++;
                ReportProgress(progress, entryPath, totalBytes, filesProcessed); // Report progress for each file
            }

            return Result.Success()
                .WithMessage(!string.IsNullOrEmpty(password)
                    ? $"Password-protected uncompressed file at '{path}' to directory '{destinationPath}'"
                    : $"Uncompressed file at '{path}' to directory '{destinationPath}'");
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during file uncompression"))
                .WithMessage($"Cancelled uncompressing file at '{path}' to '{destinationPath}'");
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error uncompressing file at '{path}' to '{destinationPath}'");
        }
    }

    /// <summary>
    /// Reads a ZIP file from the storage provider and decompresses it, optionally handling password-protected compression using SharpZipLib.
    /// </summary>
    /// <remarks>
    /// This implementation uses SharpZipLib to read ZIP files with password protection compatible with popular ZIP tools (e.g., 7-Zip, WinZip).
    /// It supports PKZIP encryption (legacy) or AES-256 encryption, depending on the original file's configuration.
    /// </remarks>
    public static async Task<Result<Stream>> ReadCompressedFileAsync(this IFileStorageProvider provider, string path, string password = null, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        if (provider == null)
        {
            return Result<Stream>.Failure()
                .WithError(new ArgumentError("Provider cannot be null"))
                .WithMessage("Invalid provider provided for reading compressed file");
        }

        if (string.IsNullOrEmpty(path))
        {
            return Result<Stream>.Failure()
                .WithError(new FileSystemError("Path cannot be null or empty", path))
                .WithMessage("Invalid path provided for reading compressed file");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result<Stream>.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled reading compressed file at '{path}'");
        }

        var readResult = await provider.ReadFileAsync(path, progress, cancellationToken);
        if (readResult.IsFailure)
        {
            return readResult;
        }

        try
        {
            var zipStream = readResult.Value;
            await using var zipInputStream = new ZipInputStream(zipStream)
            {
                Password = password // Sets password for decryption (PKZIP or AES)
            };

            var entry = zipInputStream.GetNextEntry();
            if (entry == null)
            {
                return Result<Stream>.Failure()
                    .WithError(new FileSystemError("No entries found in compressed file", path))
                    .WithMessage($"Failed to read compressed file at '{path}'");
            }

            var decompressedStream = new MemoryStream();
            await zipInputStream.CopyToAsync(decompressedStream, 8192, cancellationToken);
            decompressedStream.Position = 0; // Reset position for reading
            var length = decompressedStream.Length;
            ReportProgress(progress, path, length, 1); // Report decompressed size

            return Result<Stream>.Success(decompressedStream)
                .WithMessage(!string.IsNullOrEmpty(password)
                    ? $"Password-protected decompressed and read file at '{path}'"
                    : $"Decompressed and read file at '{path}'");
        }
        catch (OperationCanceledException)
        {
            return Result<Stream>.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during decompression"))
                .WithMessage($"Cancelled decompressing file at '{path}'");
        }
        catch (Exception ex)
        {
            return Result<Stream>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error decompressing file at '{path}'");
        }
    }

    private static void ReportProgress(IProgress<FileProgress> progress, string path, long bytesProcessed, long filesProcessed, long totalFiles = 1)
    {
        progress?.Report(new FileProgress
        {
            BytesProcessed = bytesProcessed,
            FilesProcessed = filesProcessed,
            TotalFiles = totalFiles
        });
    }

    /// <summary>
    /// Gets the relative path of a file within a base directory, ensuring consistent path separators and proper trimming.
    /// </summary>
    private static string GetRelativePath(string fullPath, string baseDirectory)
    {
        // Ensure consistent path separators (use forward slashes for ZIP compatibility)
        fullPath = fullPath.Replace("\\", "/").Trim('/');
        baseDirectory = baseDirectory.Replace("\\", "/").Trim('/');

        // Ensure baseDirectory ends with a forward slash for proper substring removal
        if (!baseDirectory.EndsWith("/"))
        {
            baseDirectory += "/";
        }

        // Remove the base directory prefix to get the relative path
        if (fullPath.StartsWith(baseDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return fullPath[baseDirectory.Length..];
        }

        // Fallback: if the paths don't match as expected, return the full path (this should rarely happen)
        return fullPath;
    }
}