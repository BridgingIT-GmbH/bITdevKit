// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure.Storage;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using global::Azure;
using global::Azure.Storage.Files.Shares;
using global::Azure.Storage.Files.Shares.Models;
using System.Security.Cryptography;

/// <summary>
/// File storage provider that uses Azure Files via the REST API.
/// </summary>
public class AzureFilesFileStorageProvider : BaseFileStorageProvider, IDisposable
{
    private readonly string connectionString;
    private readonly Lazy<ShareServiceClient> lazyShareServiceClient;
    private readonly string shareName;
    private readonly bool ensureShare;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of AzureFilesFileStorageProvider with a connection string.
    /// </summary>
    /// <param name="connectionString">The Azure Storage connection string.</param>
    /// <param name="shareName">The name of the file share.</param>
    /// <param name="locationName">The logical name of this storage location.</param>
    /// <param name="ensureShare">Whether to create the share if it doesn't exist.</param>
    public AzureFilesFileStorageProvider(
        string locationName,
        string connectionString,
        string shareName,
        bool ensureShare = true) : base(locationName)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString, nameof(connectionString));
        ArgumentException.ThrowIfNullOrEmpty(shareName, nameof(shareName));

        this.connectionString = connectionString;
        this.shareName = shareName;
        this.ensureShare = ensureShare;

        // Initialize ShareServiceClient lazily
        this.lazyShareServiceClient = new Lazy<ShareServiceClient>(
            () => new ShareServiceClient(connectionString),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// Initializes a new instance of AzureFilesFileStorageProvider with a pre-configured ShareServiceClient.
    /// </summary>
    /// <param name="client">A pre-configured Azure Files Share Service client.</param>
    /// <param name="shareName">The name of the file share.</param>
    /// <param name="locationName">The logical name of this storage location.</param>
    /// <param name="ensureShare">Whether to create the share if it doesn't exist.</param>
    public AzureFilesFileStorageProvider(
        string locationName,
        ShareServiceClient client,
        string shareName,
        bool ensureShare = true) : base(locationName)
    {
        ArgumentNullException.ThrowIfNull(client, nameof(client));
        ArgumentException.ThrowIfNullOrEmpty(shareName, nameof(shareName));

        this.shareName = shareName;
        this.ensureShare = ensureShare;

        // Initialize with provided ShareServiceClient
        this.lazyShareServiceClient = new Lazy<ShareServiceClient>(() => client);
    }

    private ShareServiceClient Client => this.lazyShareServiceClient.Value;

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

        var normalizedPath = this.NormalizePath(path);
        var shareClient = await this.GetShareClientAsync();
        var directoryClient = shareClient.GetRootDirectoryClient();
        var fileClient = directoryClient.GetFileClient(normalizedPath);

        try
        {
            var exists = await fileClient.ExistsAsync(cancellationToken);
            if (!exists.Value)
            {
                return Result.Failure()
                    .WithError(new NotFoundError("File not found"));
            }

            var properties = await fileClient.GetPropertiesAsync(cancellationToken);
            this.ReportProgress(progress, path, properties.Value.ContentLength, 1);

            return Result.Success()
                .WithMessage($"Checked existence of file at '{path}'");
        }
        catch (RequestFailedException ex) when (ex.Status == 403)
        {
            return Result.Failure()
                .WithError(new PermissionError("Access denied", path, ex))
                .WithMessage($"Permission denied for file at '{path}'");
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error checking existence of file at '{path}'");
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

        var normalizedPath = this.NormalizePath(path);
        var shareClient = await this.GetShareClientAsync();
        var directoryClient = shareClient.GetRootDirectoryClient();
        var fileClient = directoryClient.GetFileClient(normalizedPath);

        try
        {
            if (!await fileClient.ExistsAsync(cancellationToken))
            {
                return Result<Stream>.Failure()
                    .WithError(new FileSystemError("File not found", path))
                    .WithMessage($"Failed to read file at '{path}'");
            }

            var downloadInfo = await fileClient.DownloadAsync(cancellationToken: cancellationToken);
            var memoryStream = new MemoryStream();
            await downloadInfo.Value.Content.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;
            this.ReportProgress(progress, path, memoryStream.Length, 1);

            return Result<Stream>.Success(memoryStream)
                .WithMessage($"Read file at '{path}'");
        }
        catch (RequestFailedException ex) when (ex.Status == 403)
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
        var shareClient = await this.GetShareClientAsync();
        var directoryClient = shareClient.GetRootDirectoryClient();
        var fileClient = directoryClient.GetFileClient(normalizedPath);

        try
        {
            // Ensure parent directories exist
            var directoryPath = Path.GetDirectoryName(normalizedPath);
            if (!string.IsNullOrEmpty(directoryPath))
            {
                var dirClient = shareClient.GetDirectoryClient(directoryPath);
                await dirClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            }

            var bytesWritten = 0L;
            var fileSize = content.Length > 0 ? content.Length : 1024; // Default size if unknown
            await fileClient.CreateAsync(fileSize, cancellationToken: cancellationToken);

            content.Position = 0;
            await fileClient.UploadAsync(content, new Progress<long>(bytes =>
            {
                bytesWritten = bytes;
                this.ReportProgress(progress, path, bytes, 1);
            }), cancellationToken);

            this.ReportProgress(progress, path, bytesWritten, 1);

            return Result.Success()
                .WithMessage($"Wrote file at '{path}'");
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during write"))
                .WithMessage($"Cancelled writing file at '{path}'");
        }
        catch (RequestFailedException ex) when (ex.Status == 403)
        {
            return Result.Failure()
                .WithError(new PermissionError("Access denied", path, ex))
                .WithMessage($"Permission denied for file at '{path}'");
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error writing file at '{path}'");
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

        var normalizedPath = this.NormalizePath(path);
        var shareClient = await this.GetShareClientAsync();
        var directoryClient = shareClient.GetRootDirectoryClient();
        var fileClient = directoryClient.GetFileClient(normalizedPath);

        try
        {
            if (!await fileClient.ExistsAsync(cancellationToken))
            {
                return Result.Failure()
                    .WithError(new FileSystemError("File not found", path))
                    .WithMessage($"Failed to delete file at '{path}'");
            }

            await fileClient.DeleteAsync(cancellationToken);
            this.ReportProgress(progress, path, 0, 1);

            return Result.Success()
                .WithMessage($"Deleted file at '{path}'");
        }
        catch (RequestFailedException ex) when (ex.Status == 403)
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

        var normalizedPath = this.NormalizePath(path);
        var shareClient = await this.GetShareClientAsync();
        var directoryClient = shareClient.GetRootDirectoryClient();
        var fileClient = directoryClient.GetFileClient(normalizedPath);

        try
        {
            if (!await fileClient.ExistsAsync(cancellationToken))
            {
                return Result<string>.Failure()
                    .WithError(new FileSystemError("File not found", path))
                    .WithMessage($"Failed to compute checksum for file at '{path}'");
            }

            var downloadInfo = await fileClient.DownloadAsync(cancellationToken: cancellationToken);
            using var sha256 = SHA256.Create();
            var hash = Convert.ToBase64String(await sha256.ComputeHashAsync(downloadInfo.Value.Content, cancellationToken));
            return Result<string>.Success(hash)
                .WithMessage($"Computed checksum for file at '{path}'");
        }
        catch (RequestFailedException ex) when (ex.Status == 403)
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

    public override async Task<Result<FileMetadata>> GetFileMetadataAsync(string path, CancellationToken cancellationToken = default)
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

        var normalizedPath = this.NormalizePath(path);
        var shareClient = await this.GetShareClientAsync();
        var directoryClient = shareClient.GetRootDirectoryClient();
        var fileClient = directoryClient.GetFileClient(normalizedPath);

        try
        {
            if (!await fileClient.ExistsAsync(cancellationToken))
            {
                return Result<FileMetadata>.Failure()
                    .WithError(new FileSystemError("File not found", path))
                    .WithMessage($"Failed to retrieve metadata for file at '{path}'");
            }

            var properties = await fileClient.GetPropertiesAsync(cancellationToken);
            var metadata = new FileMetadata
            {
                Path = path,
                Length = properties.Value.ContentLength,
                LastModified = properties.Value.LastModified.UtcDateTime
            };

            // Check custom metadata for LastModified override
            if (properties.Value.Metadata.TryGetValue("LastModified", out var lastModifiedStr) &&
                DateTime.TryParse(lastModifiedStr, out var lastModified))
            {
                metadata.LastModified = lastModified;
            }

            return Result<FileMetadata>.Success(metadata)
                .WithMessage($"Retrieved metadata for file at '{path}'");
        }
        catch (RequestFailedException ex) when (ex.Status == 403)
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

        var normalizedPath = this.NormalizePath(path);
        var shareClient = await this.GetShareClientAsync();
        var directoryClient = shareClient.GetRootDirectoryClient();
        var fileClient = directoryClient.GetFileClient(normalizedPath);

        try
        {
            if (!await fileClient.ExistsAsync(cancellationToken))
            {
                return Result.Failure()
                    .WithError(new FileSystemError("File not found", path))
                    .WithMessage($"Failed to set metadata for file at '{path}'");
            }

            var blobMetadata = new Dictionary<string, string>();
            if (metadata.LastModified.HasValue)
            {
                blobMetadata["LastModified"] = metadata.LastModified.Value.ToString("O");
            }

            await fileClient.SetMetadataAsync(blobMetadata, cancellationToken);
            return Result.Success()
                .WithMessage($"Set metadata for file at '{path}'");
        }
        catch (RequestFailedException ex) when (ex.Status == 403)
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

        var normalizedPath = this.NormalizePath(path);
        var shareClient = await this.GetShareClientAsync();
        var directoryClient = shareClient.GetRootDirectoryClient();
        var fileClient = directoryClient.GetFileClient(normalizedPath);

        try
        {
            if (!await fileClient.ExistsAsync(cancellationToken))
            {
                return Result<FileMetadata>.Failure()
                    .WithError(new FileSystemError("File not found", path))
                    .WithMessage($"Failed to update metadata for file at '{path}'");
            }

            var currentMetadataResult = await this.GetFileMetadataAsync(path, cancellationToken);
            if (currentMetadataResult.IsFailure)
            {
                return Result<FileMetadata>.Failure()
                    .WithErrors(currentMetadataResult.Errors)
                    .WithMessages(currentMetadataResult.Messages);
            }

            var updatedMetadata = metadataUpdate(currentMetadataResult.Value);
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
        catch (RequestFailedException ex) when (ex.Status == 403)
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
                .WithMessage($"Cancelled listing files in '{path}'");
        }

        var normalizedPath = this.NormalizePath(path);
        var shareClient = await this.GetShareClientAsync();
        var directoryClient = shareClient.GetDirectoryClient(normalizedPath);

        try
        {
            if (!await directoryClient.ExistsAsync(cancellationToken))
            {
                return Result<(IEnumerable<string> Files, string NextContinuationToken)>.Success(([], null))
                    .WithMessage($"Listed files in '{path}' with pattern '{searchPattern}'");
            }

            var files = new List<string>();
            var pageSize = 100; // Match Local's PageSize
            var items = directoryClient.GetFilesAndDirectoriesAsync(
                prefix: null,
                cancellationToken: cancellationToken)
                .AsPages(continuationToken, pageSize);

            var page = await items.FirstOrDefaultAsync(cancellationToken);
            if (page == null || page.Values.Count == 0)
            {
                return Result<(IEnumerable<string> Files, string NextContinuationToken)>.Success(([], null))
                    .WithMessage($"Listed files in '{path}' with pattern '{searchPattern}'");
            }

            foreach (var item in page.Values)
            {
                if (item.IsDirectory)
                {
                    if (recursive)
                    {
                        var subResult = await this.ListFilesAsync(
                            Path.Combine(normalizedPath, item.Name),
                            searchPattern,
                            true,
                            null,
                            cancellationToken);
                        if (subResult.IsSuccess)
                        {
                            files.AddRange(subResult.Value.Files);
                        }
                    }
                    continue;
                }

                var filePath = Path.Combine(normalizedPath, item.Name);
                if (string.IsNullOrEmpty(searchPattern) || Path.GetFileName(filePath).Match(searchPattern))
                {
                    files.Add(filePath);
                }
            }

            var nextToken = page.ContinuationToken != null && files.Count >= pageSize ? page.ContinuationToken : null;

            return Result<(IEnumerable<string> Files, string NextContinuationToken)>.Success((files.Order(), nextToken))
                .WithMessage($"Listed files in '{path}' with pattern '{searchPattern}'");
        }
        catch (RequestFailedException ex) when (ex.Status == 403)
        {
            return Result<(IEnumerable<string> Files, string NextContinuationToken)>.Failure()
                .WithError(new PermissionError("Access denied", path, ex))
                .WithMessage($"Failed to list files in '{path}' due to permissions");
        }
        catch (Exception ex)
        {
            return Result<(IEnumerable<string> Files, string NextContinuationToken)>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error listing files in '{path}'");
        }
    }

    public override async Task<Result> CopyFileAsync(string path, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(destinationPath))
        {
            return Result.Failure()
                .WithError(new FileSystemError("Source or destination path cannot be null or empty", $"{path ?? destinationPath}"))
                .WithMessage("Invalid paths provided");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled copying file from '{path}' to '{destinationPath}'");
        }

        var normalizedSource = this.NormalizePath(path);
        var normalizedDest = this.NormalizePath(destinationPath);
        var shareClient = await this.GetShareClientAsync();
        var sourceFileClient = shareClient.GetRootDirectoryClient().GetFileClient(normalizedSource);
        var destFileClient = shareClient.GetRootDirectoryClient().GetFileClient(normalizedDest);

        try
        {
            if (!await sourceFileClient.ExistsAsync(cancellationToken))
            {
                return Result.Failure()
                    .WithError(new FileSystemError("Source file not found", path))
                    .WithMessage($"Failed to copy file from '{path}' to '{destinationPath}'");
            }

            // Ensure destination directory exists
            var destDirectoryPath = Path.GetDirectoryName(normalizedDest);
            if (!string.IsNullOrEmpty(destDirectoryPath))
            {
                var destDirClient = shareClient.GetDirectoryClient(destDirectoryPath);
                await destDirClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            }

            var properties = await sourceFileClient.GetPropertiesAsync(cancellationToken);
            await destFileClient.CreateAsync(properties.Value.ContentLength, cancellationToken: cancellationToken);
            var copyInfo = await destFileClient.StartCopyAsync(sourceFileClient.Uri, cancellationToken: cancellationToken);

            // Poll for copy completion
            ShareFileProperties destProperties;
            do
            {
                await Task.Delay(500, cancellationToken); // Poll every 500ms
                destProperties = await destFileClient.GetPropertiesAsync(cancellationToken);
                if (destProperties.CopyStatus == CopyStatus.Failed)
                {
                    throw new RequestFailedException($"Copy operation failed with status: {destProperties.CopyStatusDescription}");
                }
            } while (destProperties.CopyStatus == CopyStatus.Pending);

            this.ReportProgress(progress, path, properties.Value.ContentLength, 1);

            return Result.Success()
                .WithMessage($"Copied file from '{path}' to '{destinationPath}'");
        }
        catch (RequestFailedException ex) when (ex.Status == 403)
        {
            return Result.Failure()
                .WithError(new PermissionError("Access denied", path, ex))
                .WithMessage($"Permission denied for file at '{path}' or '{destinationPath}'");
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error copying file from '{path}' to '{destinationPath}'");
        }
    }

    public override async Task<Result> RenameFileAsync(string path, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(destinationPath))
        {
            return Result.Failure()
                .WithError(new FileSystemError("Old or new path cannot be null or empty", $"{path ?? destinationPath}"))
                .WithMessage("Invalid paths provided");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled renaming file from '{path}' to '{destinationPath}'");
        }

        var normalizedOld = this.NormalizePath(path);
        var normalizedNew = this.NormalizePath(destinationPath);
        var shareClient = await this.GetShareClientAsync();
        var oldFileClient = shareClient.GetRootDirectoryClient().GetFileClient(normalizedOld);
        var newFileClient = shareClient.GetRootDirectoryClient().GetFileClient(normalizedNew);

        try
        {
            if (!await oldFileClient.ExistsAsync(cancellationToken))
            {
                return Result.Failure()
                    .WithError(new FileSystemError("File not found", path))
                    .WithMessage($"Failed to rename file from '{path}' to '{destinationPath}'");
            }

            // Ensure new directory exists
            var newDirectoryPath = Path.GetDirectoryName(normalizedNew);
            if (!string.IsNullOrEmpty(newDirectoryPath))
            {
                var newDirClient = shareClient.GetDirectoryClient(newDirectoryPath);
                await newDirClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            }

            var properties = await oldFileClient.GetPropertiesAsync(cancellationToken);
            await newFileClient.CreateAsync(properties.Value.ContentLength, cancellationToken: cancellationToken);
            var copyInfo = await newFileClient.StartCopyAsync(oldFileClient.Uri, cancellationToken: cancellationToken);

            // Poll for copy completion
            ShareFileProperties newProperties;
            do
            {
                await Task.Delay(500, cancellationToken); // Poll every 500ms
                newProperties = await newFileClient.GetPropertiesAsync(cancellationToken);
                if (newProperties.CopyStatus == CopyStatus.Failed)
                {
                    throw new RequestFailedException($"Copy operation failed with status: {newProperties.CopyStatusDescription}");
                }
            } while (newProperties.CopyStatus == CopyStatus.Pending);

            await oldFileClient.DeleteAsync(cancellationToken);
            this.ReportProgress(progress, path, properties.Value.ContentLength, 1);

            return Result.Success()
                .WithMessage($"Renamed file from '{path}' to '{destinationPath}'");
        }
        catch (RequestFailedException ex) when (ex.Status == 403)
        {
            return Result.Failure()
                .WithError(new PermissionError("Access denied", path, ex))
                .WithMessage($"Permission denied for file at '{path}' or '{destinationPath}'");
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error renaming file from '{path}' to '{destinationPath}'");
        }
    }

    public override async Task<Result> MoveFileAsync(string path, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(destinationPath))
        {
            return Result.Failure()
                .WithError(new FileSystemError("Source or destination path cannot be null or empty", $"{path ?? destinationPath}"))
                .WithMessage("Invalid paths provided");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled moving file from '{path}' to '{destinationPath}'");
        }

        var normalizedSource = this.NormalizePath(path);
        var normalizedDest = this.NormalizePath(destinationPath);
        var shareClient = await this.GetShareClientAsync();
        var sourceFileClient = shareClient.GetRootDirectoryClient().GetFileClient(normalizedSource);
        var destFileClient = shareClient.GetRootDirectoryClient().GetFileClient(normalizedDest);

        try
        {
            if (!await sourceFileClient.ExistsAsync(cancellationToken))
            {
                return Result.Failure()
                    .WithError(new FileSystemError("File not found", path))
                    .WithMessage($"Failed to move file from '{path}' to '{destinationPath}'");
            }

            // Ensure destination directory exists
            var destDirectoryPath = Path.GetDirectoryName(normalizedDest);
            if (!string.IsNullOrEmpty(destDirectoryPath))
            {
                var destDirClient = shareClient.GetDirectoryClient(destDirectoryPath);
                await destDirClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            }

            var properties = await sourceFileClient.GetPropertiesAsync(cancellationToken);
            await destFileClient.CreateAsync(properties.Value.ContentLength, cancellationToken: cancellationToken);
            var copyInfo = await destFileClient.StartCopyAsync(sourceFileClient.Uri, cancellationToken: cancellationToken);

            // Poll for copy completion
            ShareFileProperties destProperties;
            do
            {
                await Task.Delay(500, cancellationToken); // Poll every 500ms
                destProperties = await destFileClient.GetPropertiesAsync(cancellationToken);
                if (destProperties.CopyStatus == CopyStatus.Failed)
                {
                    throw new RequestFailedException($"Copy operation failed with status: {destProperties.CopyStatusDescription}");
                }
            } while (destProperties.CopyStatus == CopyStatus.Pending);

            await sourceFileClient.DeleteAsync(cancellationToken);
            this.ReportProgress(progress, path, properties.Value.ContentLength, 1);

            return Result.Success()
                .WithMessage($"Moved file from '{path}' to '{destinationPath}'");
        }
        catch (RequestFailedException ex) when (ex.Status == 403)
        {
            return Result.Failure()
                .WithError(new PermissionError("Access denied", path, ex))
                .WithMessage($"Permission denied for file at '{path}' or '{destinationPath}'");
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error moving file from '{path}' to '{destinationPath}'");
        }
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

        var normalizedPath = this.NormalizePath(path);
        var shareClient = await this.GetShareClientAsync();
        var directoryClient = shareClient.GetDirectoryClient(normalizedPath);

        try
        {
            var exists = await directoryClient.ExistsAsync(cancellationToken);
            if (!exists.Value)
            {
                return Result.Failure()
                    .WithError(new NotFoundError("Directory not found"));
            }

            return Result.Success()
                .WithMessage($"Checked if '{path}' is a directory");
        }
        catch (RequestFailedException ex) when (ex.Status == 403)
        {
            return Result.Failure()
                .WithError(new PermissionError("Access denied", path, ex))
                .WithMessage($"Permission denied for path '{path}'");
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error checking if '{path}' is a directory");
        }
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

        var normalizedPath = this.NormalizePath(path);
        var shareClient = await this.GetShareClientAsync();
        var directoryClient = shareClient.GetDirectoryClient(normalizedPath);

        try
        {
            await directoryClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            return Result.Success()
                .WithMessage($"Created directory at '{path}'");
        }
        catch (RequestFailedException ex) when (ex.Status == 403)
        {
            return Result.Failure()
                .WithError(new PermissionError("Access denied", path, ex))
                .WithMessage($"Permission denied for path '{path}'");
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error creating directory at '{path}'");
        }
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

        var normalizedPath = this.NormalizePath(path);
        var shareClient = await this.GetShareClientAsync();
        var directoryClient = shareClient.GetDirectoryClient(normalizedPath);

        try
        {
            if (!await directoryClient.ExistsAsync(cancellationToken))
            {
                return Result.Failure()
                    .WithError(new FileSystemError("Directory not found", path))
                    .WithMessage($"Failed to delete directory at '{path}'");
            }

            if (!recursive)
            {
                var items = directoryClient.GetFilesAndDirectoriesAsync(cancellationToken: cancellationToken);
                if (await items.AnyAsync(cancellationToken))
                {
                    return Result.Failure()
                        .WithError(new FileSystemError("Directory not empty", path))
                        .WithMessage($"Failed to delete directory at '{path}'");
                }
            }

            await directoryClient.DeleteAsync(cancellationToken);
            return Result.Success()
                .WithMessage($"Deleted directory at '{path}'");
        }
        catch (RequestFailedException ex) when (ex.Status == 403)
        {
            return Result.Failure()
                .WithError(new PermissionError("Access denied", path, ex))
                .WithMessage($"Permission denied for path '{path}'");
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error deleting directory at '{path}'");
        }
    }

    public override async Task<Result<IEnumerable<string>>> ListDirectoriesAsync(string path, string searchPattern = null, bool recursive = false, CancellationToken cancellationToken = default)
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
                .WithMessage($"Cancelled listing directories in '{path}'");
        }

        var normalizedPath = this.NormalizePath(path);
        var shareClient = await this.GetShareClientAsync();
        var directoryClient = shareClient.GetDirectoryClient(normalizedPath);

        try
        {
            if (!await directoryClient.ExistsAsync(cancellationToken))
            {
                return Result<IEnumerable<string>>.Failure()
                    .WithError(new FileSystemError("Directory not found", path))
                    .WithMessage($"Failed to list directories in '{path}'");
            }

            var directories = new HashSet<string>();
            await foreach (var item in directoryClient.GetFilesAndDirectoriesAsync(cancellationToken: cancellationToken))
            {
                if (!item.IsDirectory)
                {
                    continue;
                }

                var dirPath = Path.Combine(normalizedPath, item.Name);
                if (string.IsNullOrEmpty(searchPattern) || Path.GetFileName(dirPath).Match(searchPattern))
                {
                    directories.Add(dirPath);
                }

                if (recursive)
                {
                    var subDirs = await this.ListDirectoriesAsync(dirPath, searchPattern, true, cancellationToken);
                    if (subDirs.IsSuccess)
                    {
                        directories.UnionWith(subDirs.Value);
                    }
                }
            }

            return Result<IEnumerable<string>>.Success(directories.Order())
                .WithMessage($"Listed directories in '{path}' with pattern '{searchPattern}'");
        }
        catch (RequestFailedException ex) when (ex.Status == 403)
        {
            return Result<IEnumerable<string>>.Failure()
                .WithError(new PermissionError("Access denied", path, ex))
                .WithMessage($"Failed to list directories in '{path}' due to permissions");
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<string>>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error listing directories in '{path}'");
        }
    }

    public override async Task<Result> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled checking health of Azure Files storage at '{this.LocationName}'");
        }

        var shareClient = await this.GetShareClientAsync();

        try
        {
            await shareClient.GetPropertiesAsync(cancellationToken);

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
                .WithMessage($"Azure Files storage at '{this.LocationName}' is healthy");
        }
        catch (RequestFailedException ex) when (ex.Status == 403)
        {
            return Result.Failure()
                .WithError(new PermissionError("Access denied", this.shareName, ex))
                .WithMessage($"Permission denied for Azure Files storage at '{this.LocationName}'");
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error checking health of Azure Files storage at '{this.LocationName}'");
        }
    }

    private async Task<ShareClient> GetShareClientAsync()
    {
        var shareClient = this.Client.GetShareClient(this.shareName);
        if (this.ensureShare)
        {
            await shareClient.CreateIfNotExistsAsync(cancellationToken: CancellationToken.None);
        }
        return shareClient;
    }

    private string NormalizePath(string path)
    {
        return path?.Replace('\\', '/').TrimStart('/');
    }

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
            // No additional resources to dispose since ShareServiceClient doesn't implement IDisposable
        }

        this.disposed = true;
    }
}