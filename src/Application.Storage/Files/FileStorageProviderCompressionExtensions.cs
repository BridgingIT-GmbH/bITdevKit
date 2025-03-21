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
/// Extension methods for <see cref="IFileStorageProvider"/> to add ZIP compression and decompression functionality.
/// </summary>
public static class FileStorageProviderCompressionExtensions
{
    /// <summary>
    /// Writes a file to the storage provider with ZIP compression, optionally password-protected using SharpZipLib (PKZIP or AES-256).
    /// </summary>
    /// <param name="provider">The file storage provider to use for writing the compressed file.</param>
    /// <param name="path">The path where the ZIP file will be written (e.g., "output.zip").</param>
    /// <param name="content">The stream containing the content to compress.</param>
    /// <param name="password">An optional password for encrypting the ZIP file. If null or empty, no encryption is applied.</param>
    /// <param name="progress">An optional progress reporter for tracking the compression process.</param>
    /// <param name="options">Optional configuration settings for ZIP compression. If null, default settings are used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="Result"/> indicating success or failure of the compression operation.</returns>
    /// <remarks>
    /// This method uses SharpZipLib to create ZIP files with password protection compatible with popular ZIP tools (e.g., 7-Zip, WinZip).
    /// It supports PKZIP encryption (legacy) or AES encryption, depending on the configuration.
    /// </remarks>
    public static async Task<Result> CompressAsync(
        this IFileStorageProvider provider,
        string path,
        Stream content,
        string password = null,
        IProgress<FileProgress> progress = null,
        FileCompressionOptions options = null,
        CancellationToken cancellationToken = default)
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

        options ??= FileCompressionOptions.Default;

        try
        {
            var zipStream = new MemoryStream();
            await using (var zipOutputStream = new ZipOutputStream(zipStream) { IsStreamOwner = false })
            {
                if (!string.IsNullOrEmpty(password))
                {
                    zipOutputStream.Password = password;
                }
                zipOutputStream.UseZip64 = options.UseZip64 ? UseZip64.On : UseZip64.Off;
                zipOutputStream.SetLevel(options.CompressionLevel);

                var entry = new ZipEntry("data");
                if (options.EntryDateTime.HasValue)
                {
                    entry.DateTime = options.EntryDateTime.Value;
                }
                if (options.AesEncryptionEnabled)
                {
                    entry.AESKeySize = options.AesEncryptionKeySize;
                }

                zipOutputStream.PutNextEntry(entry);

                await content.CopyToAsync(zipOutputStream, options.BufferSize, cancellationToken);
                await zipOutputStream.FlushAsync(cancellationToken);
                zipOutputStream.CloseEntry();

                await zipOutputStream.FinishAsync(cancellationToken);
                zipStream.Position = 0;
                var length = zipStream.Length;

                ReportProgress(progress, path, length, 1);
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
    /// Writes a ZIP file to the storage provider by compressing a file or all files and subdirectories in a directory, optionally password-protected using SharpZipLib (PKZIP or AES-256).
    /// </summary>
    /// <param name="provider">The file storage provider to use for reading the input and writing the ZIP file.</param>
    /// <param name="path">The path where the ZIP file will be written (e.g., "output.zip").</param>
    /// <param name="inputPath">The path of the file or directory to compress (e.g., "file.txt" or "directory").</param>
    /// <param name="password">An optional password for encrypting the ZIP file. If null or empty, no encryption is applied.</param>
    /// <param name="progress">An optional progress reporter for tracking the compression process.</param>
    /// <param name="options">Optional configuration settings for ZIP compression. If null, default settings are used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="Result"/> indicating success or failure of the compression operation.</returns>
    /// <remarks>
    /// This method uses SharpZipLib to create a ZIP file from a file or directory path in the storage provider, maintaining compatibility with popular ZIP tools (e.g., 7-Zip, WinZip).
    /// It supports PKZIP encryption (legacy) or AES encryption, depending on the configuration.
    /// </remarks>
    public static async Task<Result> CompressAsync(
        this IFileStorageProvider provider,
        string path,
        string inputPath,
        string password = null,
        IProgress<FileProgress> progress = null,
        FileCompressionOptions options = null,
        CancellationToken cancellationToken = default)
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

        options ??= FileCompressionOptions.Default;

        try
        {
            if (fileResult.IsSuccess) // zip single file
            {
                return await CompressFile(provider, path, inputPath, password, progress, options, cancellationToken);
            }
            else // zip full directory
            {
                return await CompressDirectory(provider, path, inputPath, password, progress, options, cancellationToken);
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

        static async Task<Result> CompressFile(
            IFileStorageProvider provider,
            string path,
            string inputPath,
            string password,
            IProgress<FileProgress> progress,
            FileCompressionOptions options,
            CancellationToken cancellationToken)
        {
            var zipStream = new MemoryStream();
            await using (var zipOutputStream = new ZipOutputStream(zipStream) { IsStreamOwner = false })
            {
                if (!string.IsNullOrEmpty(password))
                {
                    zipOutputStream.Password = password;
                }
                zipOutputStream.UseZip64 = options.UseZip64 ? UseZip64.On : UseZip64.Off;
                zipOutputStream.SetLevel(options.CompressionLevel);

                var fileInfoResult = await provider.GetFileMetadataAsync(inputPath, cancellationToken);
                if (fileInfoResult.IsFailure) return fileInfoResult;

                var fileName = Path.GetFileName(inputPath);
                var entry = new ZipEntry(fileName);
                if (options.EntryDateTime.HasValue)
                {
                    entry.DateTime = options.EntryDateTime.Value;
                }
                if (options.AesEncryptionEnabled)
                {
                    entry.AESKeySize = options.AesEncryptionKeySize;
                }

                zipOutputStream.PutNextEntry(entry);

                await using var fileStream = (await provider.ReadFileAsync(inputPath, progress, cancellationToken)).Value;
                fileStream.Position = 0;
                await fileStream.CopyToAsync(zipOutputStream, options.BufferSize, cancellationToken);

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

        static async Task<Result> CompressDirectory(
            IFileStorageProvider provider,
            string path,
            string inputPath,
            string password,
            IProgress<FileProgress> progress,
            FileCompressionOptions options,
            CancellationToken cancellationToken)
        {
            var zipStream = new MemoryStream();
            await using (var zipOutputStream = new ZipOutputStream(zipStream) { IsStreamOwner = false })
            {
                if (!string.IsNullOrEmpty(password))
                {
                    zipOutputStream.Password = password;
                }
                zipOutputStream.UseZip64 = options.UseZip64 ? UseZip64.On : UseZip64.Off;
                zipOutputStream.SetLevel(options.CompressionLevel);

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
                        continue;
                    }

                    var relativePath = GetRelativePath(file, inputPath);
                    var entry = new ZipEntry(relativePath);
                    if (options.EntryDateTime.HasValue)
                    {
                        entry.DateTime = options.EntryDateTime.Value;
                    }
                    if (options.AesEncryptionEnabled)
                    {
                        entry.AESKeySize = options.AesEncryptionKeySize;
                    }

                    zipOutputStream.PutNextEntry(entry);

                    await using var fileStream = (await provider.ReadFileAsync(file, progress, cancellationToken)).Value;
                    fileStream.Position = 0;
                    await fileStream.CopyToAsync(zipOutputStream, options.BufferSize, cancellationToken);

                    zipOutputStream.CloseEntry();
                    totalBytes += fileInfoResult.Value.Length;
                    filesProcessed++;

                    ReportProgress(progress, file, totalBytes, filesProcessed, totalFiles);
                }

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
    }

    /// <summary>
    /// Reads a ZIP file from the storage provider and uncompresses it to a directory in the storage provider, optionally handling password-protected compression using SharpZipLib.
    /// </summary>
    /// <param name="provider">The file storage provider to use for reading the ZIP file and writing the uncompressed files.</param>
    /// <param name="path">The path of the ZIP file to uncompress (e.g., "input.zip").</param>
    /// <param name="outputPath">The directory path where the uncompressed files will be written (e.g., "uncompressed").</param>
    /// <param name="password">An optional password for decrypting the ZIP file. If null or empty, no decryption is applied.</param>
    /// <param name="progress">An optional progress reporter for tracking the uncompression process.</param>
    /// <param name="options">Optional configuration settings for ZIP uncompression. If null, default settings are used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="Result"/> indicating success or failure of the uncompression operation.</returns>
    /// <remarks>
    /// This method uses SharpZipLib to read and extract ZIP files with password protection compatible with popular ZIP tools (e.g., 7-Zip, WinZip).
    /// It supports PKZIP encryption (legacy) or AES encryption, depending on the original file's configuration.
    /// </remarks>
    public static async Task<Result> UncompressAsync(
        this IFileStorageProvider provider,
        string path,
        string outputPath,
        string password = null,
        IProgress<FileProgress> progress = null,
        FileCompressionOptions options = null,
        CancellationToken cancellationToken = default)
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

        if (string.IsNullOrEmpty(outputPath))
        {
            return Result.Failure()
                .WithError(new FileSystemError("Destination path cannot be null or empty", outputPath))
                .WithMessage("Invalid destination path provided for uncompressing file");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled uncompressing file at '{path}' to '{outputPath}'");
        }

        var createDirResult = await provider.CreateDirectoryAsync(outputPath, cancellationToken);
        if (createDirResult.IsFailure)
        {
            return Result.Failure()
                .WithErrors(createDirResult.Errors)
                .WithMessages(createDirResult.Messages);
        }

        var existsResult = await provider.FileExistsAsync(path, cancellationToken: cancellationToken);
        if (existsResult.IsFailure)
        {
            return existsResult;
        }

        options ??= FileCompressionOptions.Default;

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
                Password = password
            };

            long totalBytes = 0;
            long filesProcessed = 0;
            ZipEntry entry;
            while ((entry = zipInputStream.GetNextEntry()) != null)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return Result.Failure()
                        .WithError(new OperationCancelledError("Operation cancelled during file uncompression"))
                        .WithMessage($"Cancelled uncompressing file at '{path}' to '{outputPath}' after processing {filesProcessed} files");
                }

                var entryPath = Path.Combine(outputPath, entry.Name).Replace("\\", "/");
                var directory = Path.GetDirectoryName(entryPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    var createDirResultInner = await provider.CreateDirectoryAsync(directory, cancellationToken);
                    if (createDirResultInner.IsFailure)
                    {
                        continue;
                    }
                }

                await using var memoryStream = new MemoryStream();
                await zipInputStream.CopyToAsync(memoryStream, options.BufferSize, cancellationToken);
                memoryStream.Position = 0;

                var length = entry.Size;
                await provider.WriteFileAsync(entryPath, memoryStream, progress, cancellationToken);

                totalBytes += length;
                filesProcessed++;
                ReportProgress(progress, entryPath, totalBytes, filesProcessed);
            }

            return Result.Success()
                .WithMessage(!string.IsNullOrEmpty(password)
                    ? $"Password-protected uncompressed file at '{path}' to directory '{outputPath}'"
                    : $"Uncompressed file at '{path}' to directory '{outputPath}'");
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during file uncompression"))
                .WithMessage($"Cancelled uncompressing file at '{path}' to '{outputPath}'");
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error uncompressing file at '{path}' to '{outputPath}'");
        }
    }

    /// <summary>
    /// Reads a ZIP file from the storage provider and decompresses it into a stream, optionally handling password-protected compression using SharpZipLib.
    /// </summary>
    /// <param name="provider">The file storage provider to use for reading the ZIP file.</param>
    /// <param name="path">The path of the ZIP file to read (e.g., "input.zip").</param>
    /// <param name="password">An optional password for decrypting the ZIP file. If null or empty, no decryption is applied.</param>
    /// <param name="progress">An optional progress reporter for tracking the decompression process.</param>
    /// <param name="options">Optional configuration settings for ZIP decompression. If null, default settings are used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="Result{Stream}"/> containing the decompressed stream on success, or an error on failure.</returns>
    /// <remarks>
    /// This method uses SharpZipLib to read ZIP files with password protection compatible with popular ZIP tools (e.g., 7-Zip, WinZip).
    /// It supports PKZIP encryption (legacy) or AES encryption, depending on the original file's configuration.
    /// </remarks>
    public static async Task<Result<Stream>> ReadCompressedFileAsync(
        this IFileStorageProvider provider,
        string path,
        string password = null,
        IProgress<FileProgress> progress = null,
        FileCompressionOptions options = null,
        CancellationToken cancellationToken = default)
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

        options ??= FileCompressionOptions.Default;

        try
        {
            var zipStream = readResult.Value;
            await using var zipInputStream = new ZipInputStream(zipStream)
            {
                Password = password
            };

            var entry = zipInputStream.GetNextEntry();
            if (entry == null)
            {
                return Result<Stream>.Failure()
                    .WithError(new FileSystemError("No entries found in compressed file", path))
                    .WithMessage($"Failed to read compressed file at '{path}'");
            }

            var decompressedStream = new MemoryStream();
            await zipInputStream.CopyToAsync(decompressedStream, options.BufferSize, cancellationToken);
            decompressedStream.Position = 0;
            var length = decompressedStream.Length;
            ReportProgress(progress, path, length, 1);

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

    private static string GetRelativePath(string fullPath, string baseDirectory)
    {
        fullPath = fullPath.Replace("\\", "/").Trim('/');
        baseDirectory = baseDirectory.Replace("\\", "/").Trim('/');

        if (!baseDirectory.EndsWith("/"))
        {
            baseDirectory += "/";
        }

        if (fullPath.StartsWith(baseDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return fullPath[baseDirectory.Length..];
        }

        return fullPath;
    }
}

/// <summary>
/// Configuration options for ZIP compression and decompression using SharpZipLib.
/// </summary>
public class FileCompressionOptions
{
    /// <summary>
    /// Gets or sets the buffer size to use for stream copying operations.
    /// Default: 8192 bytes (8 KB).
    /// </summary>
    public int BufferSize { get; set; } = 8192;

    /// <summary>
    /// Gets or sets the compression level to use (0-9, where 0 is no compression and 9 is maximum).
    /// Default: 9 (maximum compression).
    /// </summary>
    public int CompressionLevel { get; set; } = 9;

    /// <summary>
    /// Gets or sets whether to use ZIP64 extensions for large files or archives.
    /// Default: false (ZIP64 disabled).
    /// </summary>
    public bool UseZip64 { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to enable AES encryption if a password is provided.
    /// If false, PKZIP encryption is used instead.
    /// Default: false (uses PKZIP encryption).
    /// </summary>
    public bool AesEncryptionEnabled { get; set; } = false;

    public int AesEncryptionKeySize { get; set; } = 256; // SharpZipLib uses 256-bit AES by default when a password is set

    /// <summary>
    /// Gets or sets the DateTime to use for ZIP entries.
    /// If null, the current DateTime is used.
    /// Default: null.
    /// </summary>
    public DateTime? EntryDateTime { get; set; }

    /// <summary>
    /// Creates a default instance of <see cref="FileCompressionOptions"/>.
    /// </summary>
    public static FileCompressionOptions Default => new FileCompressionOptions();

    /// <summary>
    /// Creates a new fluent builder for configuring <see cref="FileCompressionOptions"/>.
    /// </summary>
    /// <returns>A new instance of <see cref="FileCompressionOptionsBuilder"/>.</returns>
    public static FileCompressionOptionsBuilder CreateBuilder()
    {
        return new FileCompressionOptionsBuilder();
    }
}

/// <summary>
/// A fluent builder for creating and configuring <see cref="FileCompressionOptions"/> instances.
/// </summary>
public class FileCompressionOptionsBuilder
{
    private readonly FileCompressionOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileCompressionOptionsBuilder"/> class.
    /// </summary>
    public FileCompressionOptionsBuilder()
    {
        this.options = new FileCompressionOptions();
    }

    /// <summary>
    /// Sets the buffer size for stream copying operations.
    /// </summary>
    /// <param name="bufferSize">The buffer size in bytes (e.g., 8192 for 8 KB).</param>
    /// <returns>The current <see cref="FileCompressionOptionsBuilder"/> instance for chaining.</returns>
    public FileCompressionOptionsBuilder WithBufferSize(int bufferSize)
    {
        this.options.BufferSize = bufferSize;
        return this;
    }

    /// <summary>
    /// Sets the compression level (0-9, where 0 is no compression and 9 is maximum).
    /// </summary>
    /// <param name="level">The compression level (0-9).</param>
    /// <returns>The current <see cref="FileCompressionOptionsBuilder"/> instance for chaining.</returns>
    public FileCompressionOptionsBuilder WithCompressionLevel(int level)
    {
        if (level < 0 || level > 9)
        {
            throw new ArgumentOutOfRangeException(nameof(level), "Compression level must be between 0 and 9.");
        }
        this.options.CompressionLevel = level;
        return this;
    }

    /// <summary>
    /// Enables or disables the use of ZIP64 extensions for large files or archives.
    /// </summary>
    /// <param name="useZip64">True to enable ZIP64; false to disable.</param>
    /// <returns>The current <see cref="FileCompressionOptionsBuilder"/> instance for chaining.</returns>
    public FileCompressionOptionsBuilder WithUseZip64(bool useZip64)
    {
        this.options.UseZip64 = useZip64;
        return this;
    }

    /// <summary>
    /// Enables AES encryption for password-protected ZIP files.
    /// </summary>
    /// <returns>The current <see cref="FileCompressionOptionsBuilder"/> instance for chaining.</returns>
    public FileCompressionOptionsBuilder WithAesEncryption(bool value = false, int keySize = 256)
    {
        this.options.AesEncryptionEnabled = value;

        if (value)
        {
            this.options.AesEncryptionKeySize = keySize;
        }

        return this;
    }

    /// <summary>
    /// Sets the DateTime to use for ZIP entries.
    /// </summary>
    /// <param name="dateTime">The DateTime to set for ZIP entries. If null, the current DateTime is used.</param>
    /// <returns>The current <see cref="FileCompressionOptionsBuilder"/> instance for chaining.</returns>
    public FileCompressionOptionsBuilder WithEntryDateTime(DateTime? dateTime)
    {
        this.options.EntryDateTime = dateTime;
        return this;
    }

    /// <summary>
    /// Builds and returns the configured <see cref="FileCompressionOptions"/> instance.
    /// </summary>
    /// <returns>The configured <see cref="FileCompressionOptions"/> instance.</returns>
    public FileCompressionOptions Build()
    {
        return this.options;
    }
}