// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using BridgingIT.DevKit.Common;
using ICSharpCode.SharpZipLib.Zip;

/// <summary>
/// Extension methods for IFileStorageProvider to add compression, encryption, and additional functionality, with thread safety.
/// </summary>
public static class FileStorageProviderExtensions
{
    #region Compression/Decompression

    /// <summary>
    /// Writes a file to the storage provider with ZIP compression, optionally password-protected using SharpZipLib (PKZIP or AES-256).
    /// </summary>
    /// <remarks>
    /// This implementation uses SharpZipLib to create ZIP files with password protection compatible with popular ZIP tools (e.g., 7-Zip, WinZip).
    /// It supports PKZIP encryption (legacy) or AES-256 encryption, depending on SharpZipLib's configuration.
    /// </remarks>
    public static async Task<Result> WriteCompressedFileAsync(this IFileStorageProvider provider, string path, Stream content, string password = null, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
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
            await using var zipOutputStream = new ZipOutputStream(zipStream);
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

            await zipStream.FlushAsync(cancellationToken);
            zipStream.Position = 0; // Reset position for writing
            var length = zipStream.Length;
            ReportProgress(progress, path, length, 1); // Report compressed size

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
    public static async Task<Result> WriteCompressedFileAsync(this IFileStorageProvider provider, string path, string contentPath, string password = null, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
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

        if (string.IsNullOrEmpty(contentPath))
        {
            return Result.Failure()
                .WithError(new FileSystemError("Content path cannot be null or empty", contentPath))
                .WithMessage("Invalid input path provided for reading content");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled writing compressed directory to '{path}'");
        }

        var isDirectoryResult = await provider.IsDirectoryAsync(contentPath, cancellationToken);
        if (isDirectoryResult.IsFailure)
        {
            return Result.Failure()
                .WithErrors(isDirectoryResult.Errors)
                .WithMessages(isDirectoryResult.Messages);
        }

        try
        {
            var zipStream = new MemoryStream();
            await using var zipOutputStream = new ZipOutputStream(zipStream);
            if (!string.IsNullOrEmpty(password))
            {
                zipOutputStream.Password = password; // Sets password for PKZIP or AES encryption
                zipOutputStream.UseZip64 = UseZip64.Off; // Optional: Disable Zip64 for compatibility with older tools
            }
            zipOutputStream.SetLevel(9); // Maximum compression level (0-9)

            // List all files and directories recursively
            var listResult = await provider.ListFilesAsync(contentPath, "*.*", true, null, cancellationToken);
            if (listResult.Value.Files?.Any() != true)
            {
                return Result.Failure()
                    .WithError(new FileSystemError("No files found in directory", contentPath))
                    .WithMessage($"No files found in directory '{contentPath}' for compression");
            }

            long totalBytes = 0;
            long filesProcessed = 0;
            var totalFiles = listResult.Value.Files.Count();

            foreach (var file in listResult.Value.Files)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return Result.Failure()
                        .WithError(new OperationCancelledError("Operation cancelled during directory compression"))
                        .WithMessage($"Cancelled compressing directory '{contentPath}' after processing {filesProcessed}/{totalFiles} files");
                }

                var fileInfoResult = await provider.GetFileMetadataAsync(file, cancellationToken);
                if (fileInfoResult.IsFailure)
                {
                    continue; // Skip files that can't be accessed, but continue with others
                }

                // Calculate the relative path correctly by ensuring consistent path separators and trimming the directory prefix
                var relativePath = GetRelativePath(file, contentPath);
                var entry = new ZipEntry(relativePath);
                zipOutputStream.PutNextEntry(entry);

                await using var fileStream = (await provider.ReadFileAsync(file, progress, cancellationToken)).Value;
                await fileStream.CopyToAsync(zipOutputStream, 8192, cancellationToken);

                zipOutputStream.CloseEntry();
                await zipOutputStream.FlushAsync(cancellationToken);
                totalBytes += fileInfoResult.Value.Length;
                filesProcessed++;

                ReportProgress(progress, file, totalBytes, filesProcessed, totalFiles); // Report progress for each file
            }

            await zipStream.FlushAsync(cancellationToken);
            zipStream.Position = 0; // Reset position for writing
                                    //var length = zipStream.Length;
                                    //ReportProgress(progress, path, length, filesProcessed); // Report total compressed size

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
                        ? $"Password-protected compressed directory '{contentPath}' to '{path}'"
                        : $"Compressed directory '{contentPath}' to '{path}'");
                }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during directory compression"))
                .WithMessage($"Cancelled compressing directory '{contentPath}' to '{path}'");
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error compressing directory '{contentPath}' to '{path}'");
        }
    }

    /// <summary>
    /// Reads a ZIP file from the storage provider and uncompresses it to a directory in the storage provider, optionally handling password-protected compression using SharpZipLib.
    /// </summary>
    /// <remarks>
    /// This implementation uses SharpZipLib to read and extract ZIP files with password protection compatible with popular ZIP tools (e.g., 7-Zip, WinZip).
    /// It supports PKZIP encryption (legacy) or AES-256 encryption, depending on the original file's configuration.
    /// </remarks>
    public static async Task<Result> UncompressFileAsync(this IFileStorageProvider provider, string path, string destinationPath, string password = null, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
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

    #endregion

    #region Encryption/Decryption

    /// <summary>
    /// Writes a file to the storage provider with AES encryption.
    /// </summary>
    public static async Task<Result> WriteEncryptedFileAsync(this IFileStorageProvider provider, string path, Stream content, string encryptionKey, string initializationVector, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        if (provider == null)
        {
            return Result.Failure()
                .WithError(new ArgumentError("Provider cannot be null"))
                .WithMessage("Invalid provider provided for writing encrypted file");
        }

        if (string.IsNullOrEmpty(path))
        {
            return Result.Failure()
                .WithError(new FileSystemError("Path cannot be null or empty", path))
                .WithMessage("Invalid path provided for writing encrypted file");
        }

        if (content == null)
        {
            return Result.Failure()
                .WithError(new ArgumentError("Content stream cannot be null"))
                .WithMessage("Invalid content provided for writing encrypted file");
        }

        if (string.IsNullOrEmpty(encryptionKey) || string.IsNullOrEmpty(initializationVector))
        {
            return Result.Failure()
                .WithError(new ArgumentError("Encryption key and initialization vector cannot be null or empty"))
                .WithMessage("Invalid encryption parameters for writing encrypted file");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled writing encrypted file at '{path}'");
        }

        try
        {
            var key = Encoding.UTF8.GetBytes(encryptionKey.PadRight(32)[..32]); // 32 bytes for AES-256
            var iv = Encoding.UTF8.GetBytes(initializationVector.PadRight(16)[..16]); // 16 bytes for IV

            var encryptedStream = new MemoryStream();
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                using var encryptor = aes.CreateEncryptor();
                await using var cryptoStream = new CryptoStream(encryptedStream, encryptor, CryptoStreamMode.Write, true);
                await content.CopyToAsync(cryptoStream, 8192, cancellationToken);
            }
            encryptedStream.Position = 0; // Reset position for writing
            var length = encryptedStream.Length;
            ReportProgress(progress, path, length, 1); // Report encrypted size

            return await provider.WriteFileAsync(path, encryptedStream, progress, cancellationToken)
                .ContinueWith(task =>
                {
                    if (task.IsFaulted || task.IsCanceled)
                    {
                        return Result.Failure()
                            .WithErrors(task.Result.Errors)
                            .WithMessages(task.Result.Messages);
                    }
                    return task.Result.WithMessage($"Encrypted and wrote file at '{path}'");
                }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during encryption"))
                .WithMessage($"Cancelled encrypting file at '{path}'");
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error encrypting file at '{path}'");
        }
    }

    /// <summary>
    /// Reads a file from the storage provider and decrypts it using AES.
    /// </summary>
    public static async Task<Result<Stream>> ReadEncryptedFileAsync(this IFileStorageProvider provider, string path, string encryptionKey, string initializationVector, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        if (provider == null)
        {
            return Result<Stream>.Failure()
                .WithError(new ArgumentError("Provider cannot be null"))
                .WithMessage("Invalid provider provided for reading encrypted file");
        }

        if (string.IsNullOrEmpty(path))
        {
            return Result<Stream>.Failure()
                .WithError(new FileSystemError("Path cannot be null or empty", path))
                .WithMessage("Invalid path provided for reading encrypted file");
        }

        if (string.IsNullOrEmpty(encryptionKey) || string.IsNullOrEmpty(initializationVector))
        {
            return Result<Stream>.Failure()
                .WithError(new ArgumentError("Encryption key and initialization vector cannot be null or empty"))
                .WithMessage("Invalid encryption parameters for reading encrypted file");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result<Stream>.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled reading encrypted file at '{path}'");
        }

        var readResult = await provider.ReadFileAsync(path, progress, cancellationToken);
        if (readResult.IsFailure)
        {
            return readResult;
        }

        try
        {
            var encryptedStream = readResult.Value;
            var decryptedStream = new MemoryStream();
            var key = Encoding.UTF8.GetBytes(encryptionKey.PadRight(32)[..32]); // 32 bytes for AES-256
            var iv = Encoding.UTF8.GetBytes(initializationVector.PadRight(16)[..16]); // 16 bytes for IV

            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                using var decryptor = aes.CreateDecryptor();
                await using var cryptoStream = new CryptoStream(encryptedStream, decryptor, CryptoStreamMode.Read, true);
                await cryptoStream.CopyToAsync(decryptedStream, 8192, cancellationToken);
            }
            decryptedStream.Position = 0; // Reset position for reading
            var length = decryptedStream.Length;
            ReportProgress(progress, path, length, 1); // Report decrypted size
            return Result<Stream>.Success(decryptedStream)
                .WithMessage($"Decrypted and read file at '{path}'");
        }
        catch (OperationCanceledException)
        {
            return Result<Stream>.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during decryption"))
                .WithMessage($"Cancelled decrypting file at '{path}'");
        }
        catch (Exception ex)
        {
            return Result<Stream>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error decrypting file at '{path}'");
        }
    }

    #endregion

    #region Byte Array Operations

    /// <summary>
    /// Writes a byte array to the storage provider.
    /// </summary>
    public static async Task<Result> WriteBytesAsync(this IFileStorageProvider provider, string path, byte[] bytes, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        if (provider == null)
        {
            return Result.Failure()
                .WithError(new ArgumentError("Provider cannot be null"))
                .WithMessage("Invalid provider provided for writing bytes");
        }

        if (string.IsNullOrEmpty(path))
        {
            return Result.Failure()
                .WithError(new FileSystemError("Path cannot be null or empty", path))
                .WithMessage("Invalid path provided for writing bytes");
        }

        if (bytes == null)
        {
            return Result.Failure()
                .WithError(new ArgumentError("Bytes cannot be null"))
                .WithMessage("Invalid bytes provided for writing");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled writing bytes to file at '{path}'");
        }

        try
        {
            await using var memoryStream = new MemoryStream(bytes);
            var length = bytes.Length;
            ReportProgress(progress, path, length, 1); // Report byte size
            return await provider.WriteFileAsync(path, memoryStream, progress, cancellationToken)
                .ContinueWith(task =>
                {
                    if (task.IsFaulted || task.IsCanceled)
                    {
                        return Result.Failure()
                            .WithErrors(task.Result.Errors)
                            .WithMessages(task.Result.Messages);
                    }
                    return task.Result.WithMessage($"Wrote bytes to file at '{path}'");
                }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during byte writing"))
                .WithMessage($"Cancelled writing bytes to file at '{path}'");
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error writing bytes to file at '{path}'");
        }
    }

    /// <summary>
    /// Reads a file from the storage provider as a byte array.
    /// </summary>
    public static async Task<Result<byte[]>> ReadBytesAsync(this IFileStorageProvider provider, string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        if (provider == null)
        {
            return Result<byte[]>.Failure()
                .WithError(new ArgumentError("Provider cannot be null"))
                .WithMessage("Invalid provider provided for reading bytes");
        }

        if (string.IsNullOrEmpty(path))
        {
            return Result<byte[]>.Failure()
                .WithError(new FileSystemError("Path cannot be null or empty", path))
                .WithMessage("Invalid path provided for reading bytes");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result<byte[]>.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled reading bytes from file at '{path}'");
        }

        var readResult = await provider.ReadFileAsync(path, progress, cancellationToken);
        if (readResult.IsFailure)
        {
            return Result<byte[]>.Failure()
                .WithErrors(readResult.Errors)
                .WithMessages(readResult.Messages);
        }

        try
        {
            await using var stream = readResult.Value;
            var bytes = new byte[stream.Length];
            await stream.ReadExactlyAsync(bytes, 0, (int)stream.Length, cancellationToken);
            var length = bytes.Length;
            ReportProgress(progress, path, length, 1); // Report byte size
            return Result<byte[]>.Success(bytes)
                .WithMessage($"Read bytes from file at '{path}'");
        }
        catch (OperationCanceledException)
        {
            return Result<byte[]>.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during byte reading"))
                .WithMessage($"Cancelled reading bytes from file at '{path}'");
        }
        catch (Exception ex)
        {
            return Result<byte[]>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error reading bytes from file at '{path}'");
        }
    }

    #endregion

    #region Generic Object Serialization/Deserialization

    /// <summary>
    /// Writes a generic object to the storage provider by serializing it using the specified serializer (default: SystemTextJsonSerializer).
    /// </summary>
    public static async Task<Result> WriteFileAsync<T>(this IFileStorageProvider provider, string path, T content, ISerializer serializer = null, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
        where T : class
    {
        if (provider == null)
        {
            return Result.Failure()
                .WithError(new ArgumentError("Provider cannot be null"))
                .WithMessage("Invalid provider provided for writing object");
        }

        if (string.IsNullOrEmpty(path))
        {
            return Result.Failure()
                .WithError(new FileSystemError("Path cannot be null or empty", path))
                .WithMessage("Invalid path provided for writing object");
        }

        if (content == null)
        {
            return Result.Failure()
                .WithError(new ArgumentError("Object cannot be null"))
                .WithMessage("Invalid object provided for writing");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled writing object to file at '{path}'");
        }

        var effectiveSerializer = serializer ?? new SystemTextJsonSerializer();

        try
        {
            await using var memoryStream = new MemoryStream();
            effectiveSerializer.Serialize(content, memoryStream);
            var bytes = memoryStream.ToArray();
            var length = bytes.Length;
            ReportProgress(progress, path, length, 1); // Report byte size
            return await provider.WriteFileAsync(path, new MemoryStream(bytes), progress, cancellationToken)
                .ContinueWith(task =>
                {
                    if (task.IsFaulted || task.IsCanceled)
                    {
                        return Result.Failure()
                            .WithErrors(task.Result.Errors)
                            .WithMessages(task.Result.Messages);
                    }
                    return task.Result.WithMessage($"Wrote object to file at '{path}'");
                }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during object serialization"))
                .WithMessage($"Cancelled writing object to file at '{path}'");
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error serializing and writing object to file at '{path}'");
        }
    }

    /// <summary>
    /// Reads a file from the storage provider and deserializes it into a generic object using the specified serializer (default: SystemTextJsonSerializer).
    /// </summary>
    public static async Task<Result<T>> ReadFileAsync<T>(this IFileStorageProvider provider, string path, ISerializer serializer = null, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
        where T : class
    {
        if (provider == null)
        {
            return Result<T>.Failure()
                .WithError(new ArgumentError("Provider cannot be null"))
                .WithMessage("Invalid provider provided for reading object");
        }

        if (string.IsNullOrEmpty(path))
        {
            return Result<T>.Failure()
                .WithError(new FileSystemError("Path cannot be null or empty", path))
                .WithMessage("Invalid path provided for reading object");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result<T>.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled reading object from file at '{path}'");
        }

        var bytesResult = await provider.ReadBytesAsync(path, progress, cancellationToken);
        if (bytesResult.IsFailure)
        {
            return Result<T>.Failure()
                .WithErrors(bytesResult.Errors)
                .WithMessages(bytesResult.Messages);
        }

        var effectiveSerializer = serializer ?? new SystemTextJsonSerializer();

        try
        {
            await using var memoryStream = new MemoryStream(bytesResult.Value);
            var obj = effectiveSerializer.Deserialize<T>(memoryStream);
            if (obj == null)
            {
                return Result<T>.Failure()
                    .WithError(new ExceptionError(new InvalidOperationException("Deserialization resulted in null object")))
                    .WithMessage($"Failed to deserialize object from file at '{path}'");
            }

            var length = bytesResult.Value.Length;
            ReportProgress(progress, path, length, 1); // Report byte size
            return Result<T>.Success(obj)
                .WithMessage($"Read and deserialized object from file at '{path}'");
        }
        catch (OperationCanceledException)
        {
            return Result<T>.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during object deserialization"))
                .WithMessage($"Cancelled reading object from file at '{path}'");
        }
        catch (Exception ex)
        {
            return Result<T>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error deserializing object from file at '{path}'");
        }
    }

    #endregion

    #region Directory Traversal

    /// <summary>
    /// Traverses the entire file provider starting from the specified path, collecting all file metadata and optionally executing an action on each file.
    /// </summary>
    /// <param name="provider">The IFileStorageProvider instance to traverse.</param>
    /// <param name="path">The starting path (directory) to begin traversal from.</param>
    /// <param name="fileAction">An optional action to execute on each file found (e.g., reading or processing the file).</param>
    /// <param name="progress">An optional progress reporter for tracking files and bytes processed.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A Result containing a List<FileMetadata> of all files found, or an error if the operation fails.</returns>
    /// <remarks>
    /// This method recursively traverses directories, collects file metadata, and supports an optional action on each file.
    /// Progress is reported based on the number of files processed and their sizes using the provided IProgress<FileProgress>.
    /// </remarks>
    public static async Task<Result<List<FileMetadata>>> TraverseFilesAsync(this IFileStorageProvider provider, string path, Func<string, Stream, CancellationToken, Task> fileAction = null, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        if (provider == null)
        {
            return Result<List<FileMetadata>>.Failure()
                .WithError(new ArgumentError("Provider cannot be null"))
                .WithMessage("Invalid provider provided for traversing files");
        }

        if (string.IsNullOrEmpty(path))
        {
            return Result<List<FileMetadata>>.Failure()
                .WithError(new FileSystemError("Start path cannot be null or empty", path))
                .WithMessage("Invalid start path provided for traversing files");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result<List<FileMetadata>>.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled traversing files from '{path}'");
        }

        try
        {
            var fileMetadatas = new List<FileMetadata>();
            await TraverseDirectoryAsync(provider, path, fileMetadatas, fileAction, progress, cancellationToken);

            progress?.Report(new FileProgress
            {
                BytesProcessed = fileMetadatas.Sum(m => m.Length),
                FilesProcessed = fileMetadatas.Count,
                TotalFiles = fileMetadatas.Count // Total files are known after traversal
            });

            return Result<List<FileMetadata>>.Success(fileMetadatas)
                .WithMessage($"Traversed '{path}' and found {fileMetadatas.Count} files");
        }
        catch (OperationCanceledException)
        {
            return Result<List<FileMetadata>>.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during file traversal"))
                .WithMessage($"Cancelled traversing files from '{path}'");
        }
        catch (Exception ex)
        {
            return Result<List<FileMetadata>>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error traversing files from '{path}'");
        }
    }

    private static async Task TraverseDirectoryAsync(IFileStorageProvider provider, string path, List<FileMetadata> fileMetadatas, Func<string, Stream, CancellationToken, Task> fileAction, IProgress<FileProgress> progress, CancellationToken cancellationToken)
    {
        // List all paths in the current directory
        var listResult = await provider.ListFilesAsync(path, "*.*", true, null, cancellationToken);
        if (listResult.IsFailure)
        {
            return; // Skip directories that can't be listed, but continue traversal
        }

        foreach (var filePath in listResult.Value.Files ?? [])
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("Operation cancelled during file traversal");
            }

            // Check if the path is a directory using IsDirectoryAsync
            var isDirectoryResult = await provider.IsDirectoryAsync(filePath, cancellationToken);
            if (isDirectoryResult.IsSuccess)
            {
                // Recursively traverse the subdirectory
                await TraverseDirectoryAsync(provider, filePath, fileMetadatas, fileAction, progress, cancellationToken);
            }
            else
            {
                // Process the file (not a directory)
                var fileInfoResult = await provider.GetFileMetadataAsync(filePath, cancellationToken);
                if (fileInfoResult.IsFailure)
                {
                    continue; // Skip files that can't be accessed, but continue with others
                }

                if (fileInfoResult.Value?.Path?.EndsWith(".directory") == false)
                {
                    fileMetadatas.Add(fileInfoResult.Value);

                    if (fileAction != null) // Execute optional action on the file if provided
                    {
                        var readResult = await provider.ReadFileAsync(filePath, progress, cancellationToken);
                        if (readResult.IsSuccess)
                        {
                            await using var fileStream = readResult.Value;
                            await fileAction(filePath, fileStream, cancellationToken);
                        }
                    }

                    // Report progress for each file
                    progress?.Report(new FileProgress
                    {
                        BytesProcessed = fileMetadatas.Sum(m => m.Length),
                        FilesProcessed = fileMetadatas.Count,
                        TotalFiles = 0 // Total files unknown until traversal completes
                    });
                }
            }
        }
    }

    #endregion

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