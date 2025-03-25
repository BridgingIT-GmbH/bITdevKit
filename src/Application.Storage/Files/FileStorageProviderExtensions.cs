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

/// <summary>
/// Extension methods for IFileStorageProvider to add encryption, and additional functionality, with thread safety.
/// </summary>
public static class FileStorageProviderExtensions
{
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

    #region Text File Operations

    /// <summary>
    /// Writes a text file to the storage provider.
    /// </summary>
    public static async Task<Result> WriteTextFileAsync(this IFileStorageProvider provider, string path, string content, Encoding encoding = null, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        if (provider == null)
        {
            return Result.Failure()
                .WithError(new ArgumentError("Provider cannot be null"))
                .WithMessage("Invalid provider provided for writing text file");
        }

        if (string.IsNullOrEmpty(path))
        {
            return Result.Failure()
                .WithError(new FileSystemError("Path cannot be null or empty", path))
                .WithMessage("Invalid path provided for writing text file");
        }

        if (content == null)
        {
            return Result.Failure()
                .WithError(new ArgumentError("Content cannot be null"))
                .WithMessage("Invalid content provided for writing text file");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled writing text file at '{path}'");
        }

        encoding ??= Encoding.UTF8;

        try
        {
            await using var memoryStream = new MemoryStream(encoding.GetBytes(content));
            var length = memoryStream.Length;
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
                    return task.Result.WithMessage($"Wrote text file at '{path}'");
                }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during text file writing"))
                .WithMessage($"Cancelled writing text file at '{path}'");
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error writing text file at '{path}'");
        }
    }

    /// <summary>
    /// Reads a text file from the storage provider.
    /// </summary>
    public static async Task<Result<string>> ReadTextFileAsync(this IFileStorageProvider provider, string path, Encoding encoding = null, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        if (provider == null)
        {
            return Result<string>.Failure()
                .WithError(new ArgumentError("Provider cannot be null"))
                .WithMessage("Invalid provider provided for reading text file");
        }

        if (string.IsNullOrEmpty(path))
        {
            return Result<string>.Failure()
                .WithError(new FileSystemError("Path cannot be null or empty", path))
                .WithMessage("Invalid path provided for reading text file");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result<string>.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled reading text file at '{path}'");
        }

        encoding ??= Encoding.UTF8;

        var readResult = await provider.ReadFileAsync(path, progress, cancellationToken);
        if (readResult.IsFailure)
        {
            return Result<string>.Failure()
                .WithErrors(readResult.Errors)
                .WithMessages(readResult.Messages);
        }

        try
        {
            await using var stream = readResult.Value;
            using var reader = new StreamReader(stream, encoding);
            var content = await reader.ReadToEndAsync(cancellationToken);
            var length = content.Length;
            ReportProgress(progress, path, length, 1); // Report character size
            return Result<string>.Success(content)
                .WithMessage($"Read text file at '{path}'");
        }
        catch (OperationCanceledException)
        {
            return Result<string>.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during text file reading"))
                .WithMessage($"Cancelled reading text file at '{path}'");
        }
        catch (Exception ex)
        {
            return Result<string>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error reading text file at '{path}'");
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
            var isDirectoryResult = await provider.DirectoryExistsAsync(filePath, cancellationToken);
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

    /// <summary>
    /// Deep copies a file or directory structure from the source path to the destination path within the same storage location.
    /// </summary>
    /// <param name="provider">The file storage provider to use for copying the structure.</param>
    /// <param name="sourcePath">The source path of the file or directory to copy (e.g., "source/folder").</param>
    /// <param name="destinationPath">The destination path where the structure will be copied (e.g., "dest/folder").</param>
    /// <param name="skipFiles">If true, skips copying files and only copies the directory structure. Default is false.</param>
    /// <param name="searchPattern">An optional search pattern to filter files to copy (e.g., "*.txt"). If null, all files are copied unless skipFiles is true.</param>
    /// <param name="progress">An optional progress reporter for tracking the copying process.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="Result"/> indicating success or failure of the deep copy operation.</returns>
    /// <remarks>
    /// This method recursively copies the directory structure, including empty directories, and files that match the search pattern (unless skipped) from the source to the destination.
    /// It uses the provider's methods to list directories, list files, create directories, and copy files, ensuring thread safety and progress reporting.
    /// </remarks>
    public static async Task<Result> DeepCopyAsync(
        this IFileStorageProvider provider,
        string sourcePath,
        string destinationPath,
        bool skipFiles = false,
        string searchPattern = null,
        IProgress<FileProgress> progress = null,
        CancellationToken cancellationToken = default)
    {
        if (provider == null)
        {
            return Result.Failure()
                .WithError(new ArgumentError("Provider cannot be null"))
                .WithMessage("Invalid provider provided for deep copying");
        }

        if (string.IsNullOrEmpty(sourcePath))
        {
            return Result.Failure()
                .WithError(new FileSystemError("Source path cannot be null or empty", sourcePath))
                .WithMessage("Invalid source path provided for deep copying");
        }

        if (string.IsNullOrEmpty(destinationPath))
        {
            return Result.Failure()
                .WithError(new FileSystemError("Destination path cannot be null or empty", destinationPath))
                .WithMessage("Invalid destination path provided for deep copying");
        }

        if (sourcePath.Equals(destinationPath, StringComparison.OrdinalIgnoreCase))
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
            var fileExistsResult = await provider.FileExistsAsync(sourcePath, cancellationToken: cancellationToken);
            var directoryExistsResult = await provider.DirectoryExistsAsync(sourcePath, cancellationToken);

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
                var destFilePath = destinationPath;
                var copyResult = await provider.CopyFileAsync(sourcePath, destFilePath, progress, cancellationToken);
                if (copyResult.IsFailure)
                {
                    return copyResult;
                }

                var metadataResult = await provider.GetFileMetadataAsync(sourcePath, cancellationToken);
                if (metadataResult.IsSuccess)
                {
                    ReportProgress(progress, sourcePath, metadataResult.Value.Length, 1, 1);
                }

                return Result.Success()
                    .WithMessage($"Deep copied file from '{sourcePath}' to '{destinationPath}'");
            }

            // Handle directory copy
            // List all directories recursively under the source path, including empty ones
            var dirListResult = await provider.ListDirectoriesAsync(sourcePath, null, true, cancellationToken);
            if (dirListResult.IsFailure)
            {
                return Result.Failure()
                    .WithErrors(dirListResult.Errors)
                    .WithMessages(dirListResult.Messages);
            }

            var directories = dirListResult.Value?.ToList() ?? [];
            directories.Insert(0, sourcePath); // Include the root directory

            // List all files recursively under the source path, applying the search pattern
            var fileListResult = await provider.ListFilesAsync(sourcePath, searchPattern ?? "*.*", true, null, cancellationToken);
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
                        .WithError(new OperationCancelledError("Operation cancelled during deep copy"))
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
                var createDirResult = await provider.CreateDirectoryAsync(destDirPath, cancellationToken);
                if (createDirResult.IsFailure)
                {
                    failedPaths.Add(dir);
                    continue;
                }

                itemsProcessed++;
                ReportProgress(progress, dir, totalBytes, itemsProcessed, totalItems);
            }

            // Copy files if not skipping
            if (!skipFiles)
            {
                foreach (var file in files)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return Result.Failure()
                            .WithError(new OperationCancelledError("Operation cancelled during deep copy"))
                            .WithMessage($"Cancelled deep copying from '{sourcePath}' to '{destinationPath}' after processing {itemsProcessed}/{totalItems} items");
                    }

                    // Compute the relative path and destination file path
                    var relativePath = file.StartsWith(sourcePath, StringComparison.OrdinalIgnoreCase)
                        ? file[sourcePath.Length..].TrimStart('/')
                        : file;
                    var destFilePath = Path.Combine(destinationPath, relativePath).Replace("\\", "/");

                    // Copy the file
                    var copyResult = await provider.CopyFileAsync(file, destFilePath, progress, cancellationToken);
                    if (copyResult.IsFailure)
                    {
                        failedPaths.Add(file);
                        continue;
                    }

                    var metadataResult = await provider.GetFileMetadataAsync(file, cancellationToken);
                    if (metadataResult.IsSuccess)
                    {
                        totalBytes += metadataResult.Value.Length;
                        itemsProcessed++;
                        ReportProgress(progress, file, totalBytes, itemsProcessed, totalItems);
                    }
                }
            }

            if (failedPaths.Count > 0)
            {
                return Result.Failure()
                    .WithError(new PartialOperationError("Partial deep copy failure", failedPaths))
                    .WithMessage($"Deep copied {itemsProcessed}/{totalItems} items from '{sourcePath}' to '{destinationPath}', {failedPaths.Count} failed");
            }

            return Result.Success()
                .WithMessage($"Deep copied structure from '{sourcePath}' to '{destinationPath}'{(skipFiles ? " (files skipped)" : string.IsNullOrEmpty(searchPattern) ? "" : $" (filtered by '{searchPattern}')")}");
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during deep copy"))
                .WithMessage($"Cancelled deep copying from '{sourcePath}' to '{destinationPath}'");
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error deep copying from '{sourcePath}' to '{destinationPath}'");
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
}