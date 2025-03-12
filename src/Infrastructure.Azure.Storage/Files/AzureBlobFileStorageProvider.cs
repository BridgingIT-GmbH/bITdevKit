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

/// <summary>
/// File storage provider that uses Azure Blob Storage.
/// </summary>
public class AzureBlobStorageProvider : BaseFileStorageProvider
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
    public AzureBlobStorageProvider(
        string locationName,
        string connectionString,
        string containerName,
        bool ensureContainer = true) : base(locationName)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(connectionString, nameof(connectionString));
        ArgumentNullException.ThrowIfNullOrEmpty(containerName, nameof(containerName));

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
    public AzureBlobStorageProvider(
        string locationName,
        BlobServiceClient client,
        string containerName,
        bool ensureContainer = true) : base(locationName)
    {
        ArgumentNullException.ThrowIfNull(client, nameof(client));
        ArgumentNullException.ThrowIfNullOrEmpty(containerName, nameof(containerName));

        if (string.IsNullOrEmpty(containerName))
        {
            throw new ArgumentException("Container name cannot be null or empty", nameof(containerName));
        }

        this.containerName = containerName;
        this.ensureContainer = ensureContainer;

        // Initialize with provided BlobServiceClient
        this.lazyBlobServiceClient = new Lazy<BlobServiceClient>(() => client);
    }

    private BlobServiceClient Client => this.lazyBlobServiceClient.Value;

    /// <inheritdoc />
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

        try
        {
            var containerClient = await this.GetContainerClientAsync();
            var blobClient = containerClient.GetBlobClient(this.NormalizePath(path));

            var exists = await blobClient.ExistsAsync(cancellationToken);
            if (!exists.Value)
            {
                return Result.Failure()
                    .WithError(new NotFoundError("File not found"));
            }

            // Report progress if needed
            this.ReportProgress(progress, path, 0, 1);

            return Result.Success()
                .WithMessage($"Checked existence of file at '{path}'");
        }
        catch (Exception ex)
        {
            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Failed to check if blob exists at '{path}'");
        }
    }

    /// <inheritdoc />
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

        try
        {
            var containerClient = await this.GetContainerClientAsync();
            var blobClient = containerClient.GetBlobClient(this.NormalizePath(path));

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

                // Copy the content to memory stream
                await downloadInfo.Value.Content.CopyToAsync(memoryStream, 81920, cancellationToken);
                memoryStream.Position = 0;

                // Report progress if needed
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
                .WithMessage($"Failed to read file at '{path}'");
        }
    }

    /// <inheritdoc />
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

        try
        {
            var containerClient = await this.GetContainerClientAsync();
            var blobClient = containerClient.GetBlobClient(this.NormalizePath(path));

            // Setup progress reporting
            var totalBytes = content.Length;
            var options = new BlobUploadOptions
            {
                ProgressHandler = new Progress<long>(uploadedBytes =>
                    this.ReportProgress(progress, path, uploadedBytes, 1))
            };

            await blobClient.UploadAsync(content, options, cancellationToken);

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
                .WithMessage($"Failed to write file at '{path}'");
        }
    }

    /// <inheritdoc />
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

        try
        {
            var containerClient = await this.GetContainerClientAsync();
            var blobClient = containerClient.GetBlobClient(this.NormalizePath(path));

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
                .WithMessage($"Failed to delete file at '{path}'");
        }
    }

    /// <inheritdoc />
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

        try
        {
            var containerClient = await this.GetContainerClientAsync();
            var blobClient = containerClient.GetBlobClient(this.NormalizePath(path));

            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                return Result<string>.Failure()
                    .WithError(new FileSystemError("File not found", path))
                    .WithMessage($"Failed to compute checksum for file at '{path}'");
            }

            try
            {
                var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);

                if (properties.Value.ContentHash?.Length > 0)
                {
                    var hash = Convert.ToBase64String(properties.Value.ContentHash);
                    return Result<string>.Success(hash)
                        .WithMessage($"Computed checksum for file at '{path}'");
                }

                return Result<string>.Failure()
                    .WithError(new FileSystemError("MD5 hash not available for this blob", path))
                    .WithMessage($"Failed to compute checksum for file at '{path}'");
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
                .WithMessage($"Failed to compute checksum for file at '{path}'");
        }
    }

    /// <inheritdoc />
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

        try
        {
            var containerClient = await this.GetContainerClientAsync();
            var blobClient = containerClient.GetBlobClient(this.NormalizePath(path));

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
                    LastModified = properties.Value.LastModified.DateTime
                };

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
                .WithMessage($"Failed to retrieve metadata for file at '{path}'");
        }
    }

    /// <inheritdoc />
    public override async Task<Result> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if we can access the container
            var containerClient = await this.GetContainerClientAsync();
            await containerClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            return Result.Success()
                .WithMessage($"Azure Blob Storage at '{this.LocationName}' is healthy");
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
                .WithMessage($"Health check failed for Azure Blob Storage at '{this.LocationName}'");
        }
    }

    /// <inheritdoc />
    public override async Task<Result> SetFileMetadataAsync(string path, FileMetadata metadata, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = await this.GetContainerClientAsync();
            var blobClient = containerClient.GetBlobClient(this.NormalizePath(path));

            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                return Result.Failure($"File '{path}' not found");
            }

            var blobMetadata = new Dictionary<string, string>();

            //foreach (var prop in metadata.Properties)
            //{
            //    blobMetadata[prop.Key] = prop.Value?.ToString() ?? string.Empty;
            //}

            await blobClient.SetMetadataAsync(blobMetadata, cancellationToken: cancellationToken);

            // Content type can't be set with metadata, it's a property
            var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            await blobClient.SetHttpHeadersAsync(new BlobHttpHeaders
            {
                //ContentType = metadata.ContentType ?? properties.Value.ContentType,
                CacheControl = properties.Value.CacheControl,
                ContentDisposition = properties.Value.ContentDisposition,
                ContentEncoding = properties.Value.ContentEncoding,
                ContentLanguage = properties.Value.ContentLanguage
            }, cancellationToken: cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to set file metadata: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public override async Task<Result<FileMetadata>> UpdateFileMetadataAsync(string path, Func<FileMetadata, FileMetadata> metadataUpdate, CancellationToken cancellationToken = default)
    {
        try
        {
            var fileInfoResult = await this.GetFileInfoAsync(path, cancellationToken);
            if (!fileInfoResult.IsSuccess)
            {
                return fileInfoResult;
            }

            var updatedMetadata = metadataUpdate(fileInfoResult.Value);
            var result = await this.SetFileMetadataAsync(path, updatedMetadata, cancellationToken);

            return await this.GetFileInfoAsync(path, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result<FileMetadata>.Failure($"Failed to update file metadata: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public override async Task<Result<(IEnumerable<string> Files, string NextContinuationToken)>> ListFilesAsync(
        string path, string searchPattern = null, bool recursive = false,
        string continuationToken = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = await this.GetContainerClientAsync();
            path = this.NormalizePath(path);
            var prefix = string.IsNullOrEmpty(path) ? string.Empty : $"{path}/";

            var resultSegment = containerClient.GetBlobsAsync(traits: BlobTraits.None, states: BlobStates.None, prefix: prefix, cancellationToken: cancellationToken)
                .AsPages(continuationToken);
            var page = await resultSegment.FirstOrDefaultAsync(cancellationToken: cancellationToken);

            // If no results found, return empty collection
            if (page == null)
            {
                return Result<(IEnumerable<string> Files, string NextContinuationToken)>.Success(([], null));
            }

            var files = new List<string>();
            foreach (var blob in page.Values)
            {
                var blobPath = blob.Name;

                // If path is specified, remove the prefix from the blob path to get relative path
                if (!string.IsNullOrEmpty(prefix) && blobPath.StartsWith(prefix))
                {
                    blobPath = blobPath[prefix.Length..];
                }

                // Skip directories (virtual folders) if not recursive
                if (!recursive && blobPath.Contains('/'))
                {
                    var firstSegment = blobPath.Split('/')[0];
                    if (!files.Contains(firstSegment))
                    {
                        files.Add(firstSegment);
                    }
                    continue;
                }

                // Apply search pattern if specified
                if (string.IsNullOrEmpty(searchPattern) ||
                    Path.GetFileName(blobPath).Match(searchPattern))
                {
                    files.Add(blobPath);
                }
            }

            return Result<(IEnumerable<string> Files, string NextContinuationToken)>.Success(
                (files, page.ContinuationToken));
        }
        catch (Exception ex)
        {
            return Result<(IEnumerable<string> Files, string NextContinuationToken)>.Failure(
                $"Failed to list files: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public override async Task<Result> CopyFileAsync(string sourcePath, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = await this.GetContainerClientAsync();
            var sourceBlob = containerClient.GetBlobClient(this.NormalizePath(sourcePath));
            var destBlob = containerClient.GetBlobClient(this.NormalizePath(destinationPath));

            if (!await sourceBlob.ExistsAsync(cancellationToken))
            {
                return Result.Failure($"Source file '{sourcePath}' not found");
            }

            var operation = await destBlob.StartCopyFromUriAsync(sourceBlob.Uri, cancellationToken: cancellationToken);
            var copyResult = await operation.WaitForCompletionAsync(cancellationToken);

            this.ReportProgress(progress, sourcePath, 0, 1, 1);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to copy file: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public override async Task<Result> RenameFileAsync(string oldPath, string newPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        var copyResult = await this.CopyFileAsync(oldPath, newPath, progress, cancellationToken);
        if (!copyResult.IsSuccess)
        {
            return copyResult;
        }

        return await this.DeleteFileAsync(oldPath, progress, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<Result> MoveFileAsync(string sourcePath, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        return await this.RenameFileAsync(sourcePath, destinationPath, progress, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<Result> IsDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = await this.GetContainerClientAsync();
            var normalizedPath = this.NormalizePath(path);

            // In blob storage, directories are virtual
            if (string.IsNullOrEmpty(normalizedPath))
            {
                // Root is always treated as directory
                return Result.Success();
            }

            // Check if we have any blobs with this prefix
            var directoryPath = normalizedPath.EndsWith("/") ? normalizedPath : normalizedPath + "/";
            var blobItems = containerClient.GetBlobsAsync(prefix: directoryPath, cancellationToken: cancellationToken);

            // See if there's at least one blob with this prefix
            var hasAtLeastOne = await blobItems.AnyAsync(cancellationToken: cancellationToken);

            return Result.SuccessIf(hasAtLeastOne);
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to determine if path is a directory: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public override async Task<Result> CreateDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = await this.GetContainerClientAsync();

            // Directories in blob storage are virtual, they don't need explicit creation
            // However, it's common practice to create an empty blob as a directory marker
            var normalizedPath = this.NormalizePath(path);
            var directoryPath = normalizedPath.EndsWith("/") ? normalizedPath : normalizedPath + "/";
            var blobClient = containerClient.GetBlobClient(directoryPath + ".directory");

            // Upload an empty blob to mark this as a directory
            using var emptyContent = new MemoryStream();
            await blobClient.UploadAsync(emptyContent, overwrite: true, cancellationToken: cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to create directory: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public override async Task<Result> DeleteDirectoryAsync(string path, bool recursive, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = await this.GetContainerClientAsync();
            var normalizedPath = this.NormalizePath(path);
            var directoryPath = normalizedPath.EndsWith("/") ? normalizedPath : normalizedPath + "/";

            // List all blobs in this directory
            var blobItems = containerClient.GetBlobsAsync(prefix: directoryPath, cancellationToken: cancellationToken);

            // Check if there are any blobs
            var blobs = await blobItems.ToListAsync(cancellationToken);

            if (blobs.Count == 0)
            {
                // No blobs found with this prefix
                return Result.Failure($"Directory '{path}' not found or empty");
            }

            if (!recursive && blobs.Count > 1)
            {
                // If not recursive and there are multiple blobs (more than just the directory marker)
                return Result.Failure($"Directory '{path}' is not empty and recursive delete is not specified");
            }

            // Delete all blobs
            var errors = new List<string>();
            foreach (var blob in blobs)
            {
                try
                {
                    await containerClient.DeleteBlobAsync(blob.Name, cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    errors.Add($"Failed to delete blob '{blob.Name}': {ex.Message}");
                }
            }

            if (errors.Count != 0)
            {
                return Result.Failure($"Some blobs failed to delete: {string.Join("; ", errors)}");
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to delete directory: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public override async Task<Result<IEnumerable<string>>> ListDirectoriesAsync(string path, string searchPattern = null, bool recursive = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = await this.GetContainerClientAsync();
            var normalizedPath = this.NormalizePath(path);
            var prefix = string.IsNullOrEmpty(normalizedPath) ? string.Empty : normalizedPath.EndsWith("/") ? normalizedPath : normalizedPath + "/";

            // List all blobs with the prefix
            var blobItems = containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken);

            // Extract directories from blob paths
            var directories = new HashSet<string>();
            await foreach (var blob in blobItems)
            {
                // Skip the prefix itself
                var relativePath = blob.Name;
                if (prefix.Length > 0)
                {
                    relativePath = blob.Name[prefix.Length..];
                }

                // Extract directory parts
                var segments = relativePath.Split('/');

                if (recursive)
                {
                    // For recursive, add all directory levels
                    var currentPath = string.Empty;
                    for (var i = 0; i < segments.Length - 1; i++)
                    {
                        if (string.IsNullOrEmpty(currentPath))
                        {
                            currentPath = segments[i];
                        }
                        else
                        {
                            currentPath = $"{currentPath}/{segments[i]}";
                        }

                        if (!string.IsNullOrEmpty(currentPath))
                        {
                            var dirPath = prefix + currentPath;
                            directories.Add(dirPath);
                        }
                    }
                }
                else if (segments.Length > 1)
                {
                    // For non-recursive, just add the top-level directory
                    var dirPath = prefix + segments[0];
                    directories.Add(dirPath);
                }
            }

            // Apply search pattern if specified
            var result = string.IsNullOrEmpty(searchPattern)
                ? directories
                : directories.Where(dir =>  Path.GetFileName(dir.TrimEnd('/')).Match(searchPattern));

            return Result<IEnumerable<string>>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<string>>.Failure($"Failed to list directories: {ex.Message}");
        }
    }

    /// <summary>
    /// BlobContinuationToken class for parsing/storing Azure continuation tokens
    /// </summary>
    private class BlobContinuationToken(string continuationToken)
    {
        public string NextMarker { get; } = continuationToken;
    }

    private async Task<BlobContainerClient> GetContainerClientAsync()
    {
        var client = this.Client.GetBlobContainerClient(this.containerName);

        if (this.ensureContainer)
        {
            await client.CreateIfNotExistsAsync();
        }

        return client;
    }

    private string NormalizePath(string path)
    {
        return path?.Replace('\\', '/').TrimStart('/');
    }

    //private bool MatchesPattern(string fileName, string pattern)
    //{
    //    // Simple wildcard matching for search patterns
    //    if (pattern == "*")
    //    {
    //        return true;
    //    }

    //    // Handle *.ext pattern
    //    if (pattern.StartsWith("*") && pattern.Length > 1)
    //    {
    //        var ext = pattern[1..];
    //        return fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase);
    //    }

    //    // Handle exact match
    //    return string.Equals(fileName, pattern, StringComparison.OrdinalIgnoreCase);
    //}

    /// <inheritdoc />
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases resources used by the provider.
    /// </summary>
    /// <param name="disposing">Whether to release managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (this.disposed)
        {
            return;
        }

        if (disposing)
        {
            // If we created the BlobServiceClient ourselves, dispose any resources
            // Note: BlobServiceClient doesn't implement IDisposable, so no direct disposal needed
        }

        this.disposed = true;
    }
}