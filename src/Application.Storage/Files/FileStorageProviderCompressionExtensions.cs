// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System;
using System.IO;
using System.Text;
using System.Threading;
using BridgingIT.DevKit.Common;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;
using SharpCompress.Writers.GZip;
using SharpCompress.Writers.Zip;

/// <summary>
/// Extension methods for <see cref="IFileStorageProvider"/> to add compression and decompression functionality using SharpCompress.
/// </summary>
public static class FileStorageProviderCompressionExtensions
{
    /// <summary>
    /// Writes a file to the storage provider with compression using SharpCompress.
    /// </summary>
    /// <param name="provider">The file storage provider to use for writing the compressed file.</param>
    /// <param name="path">The path where the compressed file will be written (e.g., "output.zip").</param>
    /// <param name="content">The stream containing the content to compress.</param>
    /// <param name="progress">An optional progress reporter for tracking the compression process.</param>
    /// <param name="options">Optional configuration settings for compression. If null, default settings are used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="Result"/> indicating success or failure of the compression operation.</returns>
    /// <remarks>
    /// This method uses SharpCompress to create compressed files compatible with popular tools (e.g., 7-Zip, WinZip).
    /// Supported formats: Zip, Tar, GZip. Password protection is not supported during compression with SharpCompress.
    /// </remarks>
    public static async Task<Result> WriteCompressedFileAsync(
        this IFileStorageProvider provider,
        string path,
        Stream content,
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
            using (var writer = CreateWriter(zipStream, options))
            {
                writer.Write("data", content, options.EntryDateTime);
            }

            zipStream.Position = 0;
            var length = zipStream.Length;
            ReportProgress(progress, path, length, 1);

            return await provider.WriteFileAsync(path, zipStream, progress, cancellationToken)
                .ContinueWith(task =>
                {
                    if (task.IsFaulted || task.IsCanceled)
                    {
                        return Result.Failure()
                            .WithErrors(task.Result.Errors)
                            .WithMessages(task.Result.Messages);
                    }
                    return task.Result.WithMessage($"Compressed and wrote file at '{path}'");
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
    /// Reads a compressed file from the storage provider and decompresses it into a stream, optionally handling password-protected compression using SharpCompress.
    /// </summary>
    /// <param name="provider">The file storage provider to use for reading the compressed file.</param>
    /// <param name="path">The path of the compressed file to read (e.g., "input.zip").</param>
    /// <param name="password">An optional password for decrypting the compressed file. If null or empty, no decryption is applied.</param>
    /// <param name="progress">An optional progress reporter for tracking the decompression process.</param>
    /// <param name="options">Optional configuration settings for decompression. If null, default settings are used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="Result{Stream}"/> containing the decompressed stream on success, or an error on failure.</returns>
    /// <remarks>
    /// This method uses SharpCompress to read compressed files with password protection compatible with popular tools (e.g., 7-Zip, WinZip).
    /// Supported formats: Zip, Tar, GZip. It supports Deflate64 decompression and traditional PKZIP or AES encryption, depending on the original file's configuration.
    /// </remarks>
    public static async Task<Result<Stream>> ReadCompressedFile(
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
            using var archive = ArchiveFactory.Open(zipStream, new ReaderOptions { Password = password });

            // Validate the archive type matches the expected type
            if (!IsArchiveTypeMatch(archive.Type, options.ArchiveType))
            {
                return Result<Stream>.Failure()
                    .WithError(new FileSystemError($"Archive type mismatch: expected {options.ArchiveType}, but found {archive.Type}", path))
                    .WithMessage($"Failed to read compressed file at '{path}' due to archive type mismatch. Please specify the correct archive format with the options (zip/gzip/7zip)");
            }

            var entry = archive.Entries.FirstOrDefault(e => !e.IsDirectory);
            if (entry == null)
            {
                return Result<Stream>.Failure()
                    .WithError(new FileSystemError("No entries found in compressed file", path))
                    .WithMessage($"Failed to read compressed file at '{path}'");
            }

            var decompressedStream = new MemoryStream();
            await using (var entryStream = entry.OpenEntryStream())
            {
                await entryStream.CopyToAsync(decompressedStream, options.BufferSize, cancellationToken);
            }
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

    /// <summary>
    /// Writes a compressed file to the storage provider by compressing a file or all files and subdirectories in a directory using SharpCompress.
    /// </summary>
    /// <param name="provider">The file storage provider to use for reading the input and writing the compressed file.</param>
    /// <param name="path">The path where the compressed file will be written (e.g., "output.zip").</param>
    /// <param name="inputPath">The path of the file or directory to compress (e.g., "file.txt" or "directory").</param>
    /// <param name="progress">An optional progress reporter for tracking the compression process.</param>
    /// <param name="options">Optional configuration settings for compression. If null, default settings are used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="Result"/> indicating success or failure of the compression operation.</returns>
    /// <remarks>
    /// This method uses SharpCompress to create a compressed file from a file or directory path in the storage provider, maintaining compatibility with popular tools (e.g., 7-Zip, WinZip).
    /// Supported formats: Zip, Tar, GZip. Password protection is not supported during compression with SharpCompress.
    /// </remarks>
    public static async Task<Result> CompressAsync(
        this IFileStorageProvider provider,
        string path,
        string inputPath,
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
            if (fileResult.IsSuccess) // compress single file
            {
                return await CompressFile(provider, path, inputPath, progress, options, cancellationToken);
            }
            else // compress full directory
            {
                return await CompressDirectory(provider, path, inputPath, progress, options, cancellationToken);
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
            IProgress<FileProgress> progress,
            FileCompressionOptions options,
            CancellationToken cancellationToken)
        {
            var zipStream = new MemoryStream();
            using (var writer = CreateWriter(zipStream, options))
            {
                var fileInfoResult = await provider.GetFileMetadataAsync(inputPath, cancellationToken);
                if (fileInfoResult.IsFailure) return fileInfoResult;

                var fileName = Path.GetFileName(inputPath);
                await using var fileStream = (await provider.ReadFileAsync(inputPath, progress, cancellationToken)).Value;
                fileStream.Position = 0;
                writer.Write(fileName, fileStream, options.EntryDateTime);

                ReportProgress(progress, inputPath, fileInfoResult.Value.Length, 1, 1);
            }

            zipStream.Position = 0;
            return await provider.WriteFileAsync(path, zipStream, progress, cancellationToken)
                .ContinueWith(task =>
                {
                    if (task.IsFaulted || task.IsCanceled)
                    {
                        return Result.Failure()
                            .WithErrors(task.Result.Errors)
                            .WithMessages(task.Result.Messages);
                    }
                    return task.Result.WithMessage($"Compressed '{inputPath}' to '{path}'");
                }, cancellationToken);
        }

        static async Task<Result> CompressDirectory(
            IFileStorageProvider provider,
            string path,
            string inputPath,
            IProgress<FileProgress> progress,
            FileCompressionOptions options,
            CancellationToken cancellationToken)
        {
            var zipStream = new MemoryStream();
            using (var writer = CreateWriter(zipStream, options))
            {
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
                    await using var fileStream = (await provider.ReadFileAsync(file, progress, cancellationToken)).Value;
                    fileStream.Position = 0;
                    writer.Write(relativePath, fileStream, options.EntryDateTime);

                    totalBytes += fileInfoResult.Value.Length;
                    filesProcessed++;
                    ReportProgress(progress, file, totalBytes, filesProcessed, totalFiles);
                }
            }

            zipStream.Position = 0;
            return await provider.WriteFileAsync(path, zipStream, progress, cancellationToken)
                .ContinueWith(task =>
                {
                    if (task.IsFaulted || task.IsCanceled)
                    {
                        return Result.Failure()
                            .WithErrors(task.Result.Errors)
                            .WithMessages(task.Result.Messages);
                    }
                    return task.Result.WithMessage($"Compressed '{inputPath}' to '{path}'");
                }, cancellationToken);
        }
    }

    /// <summary>
    /// Reads a compressed file from the storage provider and uncompresses it to a directory in the storage provider, optionally handling password-protected compression using SharpCompress.
    /// </summary>
    /// <param name="provider">The file storage provider to use for reading the compressed file and writing the uncompressed files.</param>
    /// <param name="path">The path of the compressed file to uncompress (e.g., "input.zip").</param>
    /// <param name="outputPath">The directory path where the uncompressed files will be written (e.g., "uncompressed").</param>
    /// <param name="password">An optional password for decrypting the compressed file. If null or empty, no decryption is applied.</param>
    /// <param name="progress">An optional progress reporter for tracking the uncompression process.</param>
    /// <param name="options">Optional configuration settings for uncompression. If null, default settings are used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="Result"/> indicating success or failure of the uncompression operation.</returns>
    /// <remarks>
    /// This method uses SharpCompress to read and extract compressed files with password protection compatible with popular tools (e.g., 7-Zip, WinZip).
    /// Supported formats: Zip, Tar, GZip. It supports Deflate64 decompression and traditional PKZIP or AES encryption, depending on the original file's configuration.
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
            using var archive = ArchiveFactory.Open(zipStream, new ReaderOptions { Password = password });

            // Validate the archive type matches the expected type
            if (!IsArchiveTypeMatch(archive.Type, options.ArchiveType))
            {
                return Result.Failure()
                    .WithError(new FileSystemError($"Archive type mismatch: expected {options.ArchiveType}, but found {archive.Type}", path))
                    .WithMessage($"Failed to read compressed file at '{path}' due to archive type mismatch. Please specify the correct archive format with the options (zip/gzip/7zip)");
            }

            long totalBytes = 0;
            long filesProcessed = 0;

            foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return Result.Failure()
                        .WithError(new OperationCancelledError("Operation cancelled during file uncompression"))
                        .WithMessage($"Cancelled uncompressing file at '{path}' to '{outputPath}' after processing {filesProcessed} files");
                }

                var entryPath = Path.Combine(outputPath, entry.Key).Replace("\\", "/");
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
                await using (var entryStream = entry.OpenEntryStream())
                {
                    await entryStream.CopyToAsync(memoryStream, options.BufferSize, cancellationToken);
                }
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
    /// Lists all files within a compressed archive stored in the storage provider, optionally handling password-protected archives using SharpCompress.
    /// </summary>
    /// <param name="provider">The file storage provider to use for reading the compressed archive.</param>
    /// <param name="path">The path of the compressed archive to read (e.g., "archive.zip").</param>
    /// <param name="password">An optional password for decrypting the compressed archive. If null or empty, no decryption is applied.</param>
    /// <param name="progress">An optional progress reporter for tracking the listing process.</param>
    /// <param name="options">Optional configuration settings for archive handling. If null, default settings are used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="Result{IEnumerable{string}}"/> containing the list of file names on success, or an error on failure.</returns>
    /// <remarks>
    /// This method uses SharpCompress to read archive entries compatible with popular tools (e.g., 7-Zip, WinZip).
    /// Supported formats: Zip, Tar, GZip. Returns all file names at once without pagination.
    /// Only file entries are included (directories are excluded).
    /// </remarks>
    public static async Task<Result<IEnumerable<string>>> ListCompressedFilesAsync(
        this IFileStorageProvider provider,
        string path,
        string password = null,
        IProgress<FileProgress> progress = null,
        FileCompressionOptions options = null,
        CancellationToken cancellationToken = default)
    {
        if (provider == null)
        {
            return Result<IEnumerable<string>>.Failure()
                .WithError(new ArgumentError("Provider cannot be null"))
                .WithMessage("Invalid provider provided for listing archive files");
        }

        if (string.IsNullOrEmpty(path))
        {
            return Result<IEnumerable<string>>.Failure()
                .WithError(new FileSystemError("Path cannot be null or empty", path))
                .WithMessage("Invalid path provided for listing archive files");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result<IEnumerable<string>>.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled listing files in archive at '{path}'");
        }

        var existsResult = await provider.FileExistsAsync(path, cancellationToken: cancellationToken);
        if (existsResult.IsFailure)
        {
            return Result<IEnumerable<string>>.Failure()
                .WithErrors(existsResult.Errors)
                .WithMessages(existsResult.Messages);
        }

        options ??= FileCompressionOptions.Default;

        try
        {
            var readResult = await provider.ReadFileAsync(path, progress, cancellationToken);
            if (readResult.IsFailure)
            {
                return Result<IEnumerable<string>>.Failure()
                    .WithErrors(readResult.Errors)
                    .WithMessages(readResult.Messages);
            }

            await using var archiveStream = readResult.Value;
            using var archive = ArchiveFactory.Open(archiveStream, new ReaderOptions { Password = password });

            // Validate the archive type matches the expected type
            if (!IsArchiveTypeMatch(archive.Type, options.ArchiveType))
            {
                return Result<IEnumerable<string>>.Failure()
                    .WithError(new FileSystemError($"Archive type mismatch: expected {options.ArchiveType}, but found {archive.Type}", path))
                    .WithMessage($"Failed to read compressed file at '{path}' due to archive type mismatch. Please specify the correct archive format with the options (zip/gzip/7zip)");
            }

            var fileEntries = archive.Entries
                .Where(e => !e.IsDirectory)
                .Select(e => e.Key)
                .ToList();

            if (!fileEntries.Any())
            {
                return Result<IEnumerable<string>>.Success(Enumerable.Empty<string>())
                    .WithMessage($"No files found in archive at '{path}'");
            }

            ReportProgress(progress, path, archive.TotalSize, fileEntries.Count);

            return Result<IEnumerable<string>>.Success(fileEntries)
                .WithMessage(!string.IsNullOrEmpty(password)
                    ? $"Listed files in password-protected archive at '{path}'"
                    : $"Listed files in archive at '{path}'");
        }
        catch (OperationCanceledException)
        {
            return Result<IEnumerable<string>>.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during archive listing"))
                .WithMessage($"Cancelled listing files in archive at '{path}'");
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<string>>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error listing files in archive at '{path}'");
        }
    }

    private static IWriter CreateWriter(Stream stream, FileCompressionOptions options)
    {
        var archiveType = MapArchiveType(options.ArchiveType);
        // only allow write support for the following zip/tar/bzip2/gzip/lzip are implemented.
        if (archiveType != ArchiveType.Zip && archiveType != ArchiveType.Tar && archiveType != ArchiveType.GZip)
        {
            throw new ArgumentException($"Archive type '{archiveType}' is not supported for writing.");
        }

        var writerOptions = new WriterOptions(GetCompressionType(archiveType));
        if (archiveType == ArchiveType.Zip)
        {
            writerOptions = new ZipWriterOptions(GetCompressionType(archiveType))
            {
                DeflateCompressionLevel = (SharpCompress.Compressors.Deflate.CompressionLevel)options.CompressionLevel,
                UseZip64 = options.UseZip64,
                //ArchiveComment = "Compressed using SharpCompress",
                //Password = password,
                //Encryption = ZipEncryptionMethod.WinZipAes256
            };
        }
        else if (archiveType == ArchiveType.GZip)
        {
            writerOptions = new GZipWriterOptions()
            {
                CompressionLevel = (SharpCompress.Compressors.Deflate.CompressionLevel)options.CompressionLevel,
                //ArchiveComment = "Compressed using SharpCompress",
                //Password = password,
                //Encryption = ZipEncryptionMethod.WinZipAes256
            };
        }

        writerOptions.ArchiveEncoding = new SharpCompress.Common.ArchiveEncoding { Default = options.Encoding };
        writerOptions.LeaveStreamOpen = true;

        return WriterFactory.Open(stream, archiveType, writerOptions);
    }

    private static CompressionType GetCompressionType(ArchiveType archiveType)
    {
        return archiveType switch
        {
            ArchiveType.Zip => CompressionType.Deflate, // ZIP uses Deflate compression
            ArchiveType.GZip => CompressionType.GZip, // GZip uses GZip compression
            ArchiveType.Tar => CompressionType.None, // Tar does not use compression by default
            ArchiveType.SevenZip => CompressionType.LZMA, // 7-Zip uses LZMA compression
            ArchiveType.Rar => CompressionType.Rar, // Rar uses RAR compression
            _ => throw new ArgumentException($"Unsupported archive type for compression: {archiveType}", nameof(archiveType))
        };
    }

    private static bool IsArchiveTypeMatch(ArchiveType actualType, FileCompressionArchiveType expectedType)
    {
        var expectedArchiveType = MapArchiveType(expectedType);
        return actualType == expectedArchiveType;
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

    private static ArchiveType MapArchiveType(FileCompressionArchiveType archiveType)
    {
        return archiveType switch
        {
            FileCompressionArchiveType.Zip => ArchiveType.Zip,
            FileCompressionArchiveType.GZip => ArchiveType.GZip,
            FileCompressionArchiveType.SevenZip => ArchiveType.SevenZip,
            FileCompressionArchiveType.Rar => ArchiveType.Rar,
            FileCompressionArchiveType.Tar => ArchiveType.Tar,
            _ => throw new ArgumentException($"Unsupported archive type: {archiveType}", nameof(archiveType))
        };
    }
}

/// <summary>
/// Configuration options for compression and decompression using SharpCompress.
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
    /// Gets or sets the DateTime to use for compressed entries.
    /// If null, the current DateTime is used.
    /// Default: null.
    /// </summary>
    public DateTime? EntryDateTime { get; set; }

    /// <summary>
    /// Gets or sets the type of archive to create (e.g., Zip, Tar, GZip).
    /// Default: Zip.
    /// </summary>
    public FileCompressionArchiveType ArchiveType { get; set; } = FileCompressionArchiveType.Zip;

    /// <summary>
    /// Gets or sets the encoding to use for archive entries (e.g., file names, comments).
    /// Default: UTF8.
    /// </summary>
    public Encoding Encoding { get; set; } = Encoding.UTF8;

    /// <summary>
    /// Creates a default instance of <see cref="FileCompressionOptions"/>.
    /// </summary>
    public static FileCompressionOptions Default => new();

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
/// Specifies the type of archive to create or decompress.
/// Supported formats: Zip, Tar, GZip.
/// </summary>
public enum FileCompressionArchiveType
{
    /// <summary>
    /// ZIP archive format.
    /// </summary>
    Zip,

    /// <summary>
    /// GZip archive format.
    /// </summary>
    GZip,

    /// <summary>
    /// 7-Zip archive format.
    /// </summary>
    SevenZip,

    /// <summary>
    /// RAR archive format.
    /// </summary>
    Rar,

    /// <summary>
    /// TAR archive format.
    /// </summary>
    Tar
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
    /// Sets the DateTime to use for compressed entries.
    /// </summary>
    /// <param name="dateTime">The DateTime to set for compressed entries. If null, the current DateTime is used.</param>
    /// <returns>The current <see cref="FileCompressionOptionsBuilder"/> instance for chaining.</returns>
    public FileCompressionOptionsBuilder WithEntryDateTime(DateTime? dateTime)
    {
        this.options.EntryDateTime = dateTime;
        return this;
    }

    /// <summary>
    /// Sets the type of archive to create (e.g., Zip, Tar, GZip).
    /// </summary>
    /// <param name="archiveType">The type of archive to create. Default is Zip.</param>
    /// <returns>The current <see cref="FileCompressionOptionsBuilder"/> instance for chaining.</returns>
    public FileCompressionOptionsBuilder WithArchiveType(FileCompressionArchiveType archiveType)
    {
        this.options.ArchiveType = archiveType;
        return this;
    }

    /// <summary>
    /// Sets the encoding to use for archive entries (e.g., file names, comments).
    /// </summary>
    /// <param name="encoding">The encoding to use. Default is UTF8.</param>
    /// <returns>The current <see cref="FileCompressionOptionsBuilder"/> instance for chaining.</returns>
    public FileCompressionOptionsBuilder WithEncoding(Encoding encoding)
    {
        this.options.Encoding = encoding ?? Encoding.UTF8;
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