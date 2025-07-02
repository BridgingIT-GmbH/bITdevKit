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
using global::Azure.Storage.Blobs;
using global::Azure.Storage.Blobs.Models;
using System.Security.Cryptography;

/// <summary>
/// File storage provider that uses Azure Blob Storage.
/// </summary>
public class AzureBlobFileStorageProvider : BaseFileStorageProvider, IDisposable
{
    private readonly string connectionString;
    private readonly Lazy<BlobServiceClient> lazyBlobServiceClient;
    private readonly string containerName;
    private readonly bool ensureContainer;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of AzureBlobStorageProvider with a connection string.
    /// </summary>
    /// <param name="connectionString">The Azure Storage connection string.</param>
    /// <param name="containerName">The name of the blob container.</param>
    /// <param name="locationName">The logical name of this storage location.</param>
    /// <param name="ensureContainer">Whether to create the container if it doesn't exist.</param>
    public AzureBlobFileStorageProvider(
        string locationName,
        string connectionString,
        string containerName,
        bool ensureContainer = true) : base(locationName)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString, nameof(connectionString));
        ArgumentException.ThrowIfNullOrEmpty(containerName, nameof(containerName));

        this.connectionString = connectionString;
        this.containerName = containerName;
        this.ensureContainer = ensureContainer;

        // Initialize BlobServiceClient lazily
        this.lazyBlobServiceClient = new Lazy<BlobServiceClient>(
            () => new BlobServiceClient(connectionString),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// Initializes a new instance of AzureBlobStorageProvider with a pre-configured BlobServiceClient.
    /// </summary>
    /// <param name="client">A pre-configured Azure Blob Service client.</param>
    /// <param name="containerName">The name of the blob container.</param>
    /// <param name="locationName">The logical name of this storage location.</param>
    /// <param name="ensureContainer">Whether to create the container if it doesn't exist.</param>
    public AzureBlobFileStorageProvider(
        string locationName,
        BlobServiceClient client,
        string containerName,
        bool ensureContainer = true) : base(locationName)
    {
        ArgumentNullException.ThrowIfNull(client, nameof(client));
        ArgumentException.ThrowIfNullOrEmpty(containerName, nameof(containerName));

        this.containerName = containerName;
        this.ensureContainer = ensureContainer;

        // Initialize with provided BlobServiceClient
        this.lazyBlobServiceClient = new Lazy<BlobServiceClient>(() => client);
    }

    private BlobServiceClient Client => this.lazyBlobServiceClient.Value;

    public override async Task<Result> FileExistsAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
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
        var containerClient = await this.GetContainerClientAsync();

        try
        {
            var blobClient = containerClient.GetBlobClient(normalizedPath);
            var exists = await blobClient.ExistsAsync(cancellationToken);
            if (!exists.Value)
            {
                return Result.Failure()
                    .WithError(new NotFoundError("File not found"));
            }

            var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            this.ReportProgress(progress, path, properties.Value.ContentLength, 1);

            return Result.Success()
                .WithMessage($"Checked existence of file at '{path}'");
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
        var containerClient = await this.GetContainerClientAsync();

        try
        {
            var blobClient = containerClient.GetBlobClient(normalizedPath);
            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                return Result<Stream>.Failure()
                    .WithError(new FileSystemError("File not found", path))
                    .WithMessage($"Failed to read file at '{path}'");
            }

            try
            {
                var downloadInfo = await blobClient.DownloadAsync(cancellationToken);
                var memoryStream = new MemoryStream();
                await downloadInfo.Value.Content.CopyToAsync(memoryStream, 81920, cancellationToken);
                memoryStream.Position = 0;
                this.ReportProgress(progress, path, memoryStream.Length, 1);

                return Result<Stream>.Success(memoryStream)
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
        var containerClient = await this.GetContainerClientAsync();

        try
        {
            var blobClient = containerClient.GetBlobClient(normalizedPath);
            var bytesWritten = 0L;
            var options = new BlobUploadOptions
            {
                ProgressHandler = new Progress<long>(bytes =>
                {
                    bytesWritten = bytes;
                    this.ReportProgress(progress, path, bytes, 1);
                })
            };

            await blobClient.UploadAsync(content, options, cancellationToken);
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
        var containerClient = await this.GetContainerClientAsync();

        try
        {
            var blobClient = containerClient.GetBlobClient(normalizedPath);
            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                return Result.Failure()
                    .WithError(new FileSystemError("File not found", path))
                    .WithMessage($"Failed to delete file at '{path}'");
            }

            try
            {
                await blobClient.DeleteAsync(cancellationToken: cancellationToken);
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
        var containerClient = await this.GetContainerClientAsync();

        try
        {
            var blobClient = containerClient.GetBlobClient(normalizedPath);
            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                return Result<string>.Failure()
                    .WithError(new FileSystemError("File not found", path))
                    .WithMessage($"Failed to compute checksum for file at '{path}'");
            }

            try
            {
                // Compute SHA256 manually to match Local and test expectations
                var downloadInfo = await blobClient.DownloadAsync(cancellationToken);
                using var sha256 = SHA256.Create();
                var hash = Convert.ToBase64String(await sha256.ComputeHashAsync(downloadInfo.Value.Content, cancellationToken));
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
        var containerClient = await this.GetContainerClientAsync();

        try
        {
            var blobClient = containerClient.GetBlobClient(normalizedPath);
            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                return Result<FileMetadata>.Failure()
                    .WithError(new FileSystemError("File not found", path))
                    .WithMessage($"Failed to retrieve metadata for file at '{path}'");
            }

            try
            {
                var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
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
        var containerClient = await this.GetContainerClientAsync();

        try
        {
            var blobClient = containerClient.GetBlobClient(normalizedPath);
            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                return Result.Failure()
                    .WithError(new FileSystemError("File not found", path))
                    .WithMessage($"Failed to set metadata for file at '{path}'");
            }

            try
            {
                // Azure doesn't support setting LastModified directly; store in metadata
                var blobMetadata = new Dictionary<string, string>();
                if (metadata.LastModified.HasValue)
                {
                    blobMetadata["LastModified"] = metadata.LastModified.Value.ToString("O");
                }

                await blobClient.SetMetadataAsync(blobMetadata, cancellationToken: cancellationToken);
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
        var containerClient = await this.GetContainerClientAsync();

        try
        {
            var blobClient = containerClient.GetBlobClient(normalizedPath);
            if (!await blobClient.ExistsAsync(cancellationToken))
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
        var prefix = string.IsNullOrEmpty(normalizedPath) ? string.Empty : $"{normalizedPath}"; // omit last / here
        var containerClient = await this.GetContainerClientAsync();

        try
        {
            var resultSegment = containerClient.GetBlobsAsync(
                traits: BlobTraits.None, states: BlobStates.None, prefix: prefix, cancellationToken: cancellationToken)
                .AsPages(continuationToken, 100); // Match Local's PageSize

            var page = await resultSegment.FirstOrDefaultAsync(cancellationToken: cancellationToken);
            if (page == null || page.Values?.Count == 0)
            {
                return Result<(IEnumerable<string> Files, string NextContinuationToken)>.Success(([], null))
                    .WithMessage($"Listed files in '{path}' with pattern '{searchPattern}'");
            }

            var files = page.Values
                .Select(blob => blob.Name)
                //.Select(name => name.StartsWith(prefix) ? name[prefix.Length..] : name)
                .Where(name => recursive || !name.Contains('/'))
                .Where(name => string.IsNullOrEmpty(searchPattern) || Path.GetFileName(name).Match(searchPattern))
                .Order()
                .ToList();

            var nextToken = page.ContinuationToken != null && files.Count == 100 ? page.ContinuationToken : null;

            return Result<(IEnumerable<string> Files, string NextContinuationToken)>.Success((files, nextToken))
                .WithMessage($"Listed files in '{path}' with pattern '{searchPattern}'");
        }
        catch (UnauthorizedAccessException ex)
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
        var containerClient = await this.GetContainerClientAsync();

        try
        {
            var sourceBlob = containerClient.GetBlobClient(normalizedSource);
            var destBlob = containerClient.GetBlobClient(normalizedDest);

            if (!await sourceBlob.ExistsAsync(cancellationToken))
            {
                return Result.Failure()
                    .WithError(new FileSystemError("Source file not found", path))
                    .WithMessage($"Failed to copy file from '{path}' to '{destinationPath}'");
            }

            try
            {
                var operation = await destBlob.StartCopyFromUriAsync(sourceBlob.Uri, cancellationToken: cancellationToken);
                await operation.WaitForCompletionAsync(cancellationToken);
                var properties = await destBlob.GetPropertiesAsync(new BlobRequestConditions(), cancellationToken);
                this.ReportProgress(progress, path, properties.Value.ContentLength, 1);

                return Result.Success()
                    .WithMessage($"Copied file from '{path}' to '{destinationPath}'");
            }
            catch (UnauthorizedAccessException ex)
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
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error copying file from '{path}' to '{destinationPath}'");
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

        var normalizedOld = this.NormalizePath(oldPath);
        var normalizedNew = this.NormalizePath(newPath);
        var containerClient = await this.GetContainerClientAsync();

        try
        {
            var oldBlob = containerClient.GetBlobClient(normalizedOld);
            var newBlob = containerClient.GetBlobClient(normalizedNew);

            if (!await oldBlob.ExistsAsync(cancellationToken))
            {
                return Result.Failure()
                    .WithError(new FileSystemError("File not found", oldPath))
                    .WithMessage($"Failed to rename file from '{oldPath}' to '{newPath}'");
            }

            try
            {
                var operation = await newBlob.StartCopyFromUriAsync(oldBlob.Uri, cancellationToken: cancellationToken);
                await operation.WaitForCompletionAsync(cancellationToken);
                await oldBlob.DeleteAsync(cancellationToken: cancellationToken);
                var properties = await newBlob.GetPropertiesAsync(new BlobRequestConditions(), cancellationToken);
                this.ReportProgress(progress, oldPath, properties.Value.ContentLength, 1);

                return Result.Success()
                    .WithMessage($"Renamed file from '{oldPath}' to '{newPath}'");
            }
            catch (UnauthorizedAccessException ex)
            {
                return Result.Failure()
                    .WithError(new PermissionError("Access denied", oldPath, ex))
                    .WithMessage($"Permission denied for file at '{oldPath}' or '{newPath}'");
            }
            catch (Exception ex)
            {
                return Result.Failure()
                    .WithError(new ExceptionError(ex))
                    .WithMessage($"Unexpected error renaming file from '{oldPath}' to '{newPath}'");
            }
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error renaming file from '{oldPath}' to '{newPath}'");
        }
    }

    public override async Task<Result> MoveFileAsync(string path, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
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
                .WithMessage($"Cancelled moving file from '{path}' to '{destinationPath}'");
        }

        var normalizedOld = this.NormalizePath(path);
        var normalizedNew = this.NormalizePath(destinationPath);
        var containerClient = await this.GetContainerClientAsync();

        try
        {
            var oldBlob = containerClient.GetBlobClient(normalizedOld);
            var newBlob = containerClient.GetBlobClient(normalizedNew);

            if (!await oldBlob.ExistsAsync(cancellationToken))
            {
                return Result.Failure()
                    .WithError(new FileSystemError("File not found", path))
                    .WithMessage($"Failed to move file from '{path}' to '{destinationPath}'");
            }

            try
            {
                var operation = await newBlob.StartCopyFromUriAsync(oldBlob.Uri, cancellationToken: cancellationToken);
                await operation.WaitForCompletionAsync(cancellationToken);
                await oldBlob.DeleteAsync(cancellationToken: cancellationToken);
                var properties = await newBlob.GetPropertiesAsync(new BlobRequestConditions(), cancellationToken);
                this.ReportProgress(progress, path, properties.Value.ContentLength, 1);

                return Result.Success()
                    .WithMessage($"Moved file from '{path}' to '{destinationPath}'");
            }
            catch (UnauthorizedAccessException ex)
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
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error moving file from '{path}' to '{destinationPath}'");
        }
    }

    public override async Task<Result> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default)
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
        var containerClient = await this.GetContainerClientAsync();

        try
        {
            var prefix = normalizedPath.EndsWith("/") ? normalizedPath : normalizedPath + "/";
            var blobItems = containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken);
            var exists = await blobItems.AnyAsync(cancellationToken: cancellationToken);
            if (!exists)
            {
                return Result.Failure()
                    .WithError(new NotFoundError("Directory not found"));
            }

            return Result.Success()
                .WithMessage($"Checked if '{path}' is a directory");
        }
        catch (UnauthorizedAccessException ex)
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
        var containerClient = await this.GetContainerClientAsync();

        try
        {
            var directoryPath = normalizedPath.EndsWith("/") ? normalizedPath : normalizedPath + "/";
            var blobClient = containerClient.GetBlobClient(directoryPath + ".directory");
            using var emptyContent = new MemoryStream();
            await blobClient.UploadAsync(emptyContent, true, cancellationToken);

            return Result.Success()
                .WithMessage($"Created directory at '{path}'");
        }
        catch (UnauthorizedAccessException ex)
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
        var directoryPath = normalizedPath.EndsWith("/") ? normalizedPath : normalizedPath + "/";
        var containerClient = await this.GetContainerClientAsync();

        try
        {
            var blobItems = containerClient.GetBlobsAsync(prefix: directoryPath, cancellationToken: cancellationToken);
            var blobs = await blobItems.ToListAsync(cancellationToken);
            if (!blobs.Any())
            {
                return Result.Failure()
                    .WithError(new FileSystemError("Directory not found", path))
                    .WithMessage($"Failed to delete directory at '{path}'");
            }

            if (!recursive && blobs.Count > 1) // More than just a marker blob
            {
                return Result.Failure()
                    .WithError(new FileSystemError("Directory not empty", path))
                    .WithMessage($"Failed to delete directory at '{path}'");
            }

            foreach (var blob in blobs)
            {
                await containerClient.DeleteBlobAsync(blob.Name, cancellationToken: cancellationToken);
            }

            return Result.Success()
                .WithMessage($"Deleted directory at '{path}'");
        }
        catch (UnauthorizedAccessException ex)
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
            path = string.Empty;
            //return Result<IEnumerable<string>>.Failure()
            //    .WithError(new FileSystemError("Path cannot be null or empty", path))
            //    .WithMessage("Invalid path provided");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result<IEnumerable<string>>.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled listing directories in '{path}'");
        }

        var normalizedPath = this.NormalizePath(path);
        var prefix = string.IsNullOrEmpty(normalizedPath) ? string.Empty : normalizedPath.EndsWith("/") ? normalizedPath : normalizedPath + "/";
        var containerClient = await this.GetContainerClientAsync();

        try
        {
            var blobItems = containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken);
            var directories = new HashSet<string>();

            await foreach (var blob in blobItems)
            {
                var relativePath = blob.Name.StartsWith(prefix) ? blob.Name[prefix.Length..] : blob.Name;
                var segments = relativePath.Split('/');

                if (segments.Length > 1)
                {
                    var currentPath = string.Empty;
                    for (var i = 0; i < segments.Length - 1 && (recursive || i == 0); i++)
                    {
                        currentPath = string.IsNullOrEmpty(currentPath) ? segments[i] : $"{currentPath}/{segments[i]}";
                        directories.Add(prefix + currentPath);
                    }
                }
            }

            var result = string.IsNullOrEmpty(searchPattern)
                ? directories
                : directories.Where(dir => Path.GetFileName(dir.TrimEnd('/')).Match(searchPattern));

            return Result<IEnumerable<string>>.Success(result.Order())
                .WithMessage($"Listed directories in '{path}' with pattern '{searchPattern}'");
        }
        catch (UnauthorizedAccessException ex)
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
                .WithMessage($"Cancelled checking health of Azure Blob Storage at '{this.LocationName}'");
        }

        var containerClient = await this.GetContainerClientAsync();

        try
        {
            await containerClient.GetPropertiesAsync(cancellationToken: cancellationToken);

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
                .WithMessage($"Azure Blob storage at '{this.LocationName}' is healthy");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Result.Failure()
                .WithError(new PermissionError("Access denied", this.containerName, ex))
                .WithMessage($"Permission denied for Azure Blob Storage at '{this.LocationName}'");
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error checking health of Azure Blob Storage at '{this.LocationName}'");
        }
    }

    private async Task<BlobContainerClient> GetContainerClientAsync()
    {
        var containerClient = this.Client.GetBlobContainerClient(this.containerName);
        if (this.ensureContainer)
        {
            await containerClient.CreateIfNotExistsAsync(cancellationToken: CancellationToken.None);
        }
        return containerClient;
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
            // No additional resources to dispose since BlobServiceClient doesn't implement IDisposable
        }

        this.disposed = true;
    }
}