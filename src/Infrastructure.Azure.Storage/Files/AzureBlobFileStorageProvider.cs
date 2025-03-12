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
using global::Azure.Storage;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using global::Azure.Storage.Blobs.Models;

/// <summary>
/// File storage provider that uses Azure Blob Storage.
/// </summary>
public class AzureBlobStorageProvider : BaseFileStorageProvider
{
    private readonly string connectionString;
    private readonly Lazy<BlobServiceClient> lazyBlobServiceClient;
    private readonly string containerName;
    private readonly bool createContainerIfNotExists;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of AzureBlobStorageProvider with a connection string.
    /// </summary>
    /// <param name="connectionString">The Azure Storage connection string.</param>
    /// <param name="containerName">The name of the blob container.</param>
    /// <param name="locationName">The logical name of this storage location.</param>
    /// <param name="createContainerIfNotExists">Whether to create the container if it doesn't exist.</param>
    public AzureBlobStorageProvider(
        string locationName,
        string connectionString,
        string containerName,
        bool createContainerIfNotExists = true) : base(locationName)
    {
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
        }

        if (string.IsNullOrEmpty(containerName))
        {
            throw new ArgumentException("Container name cannot be null or empty", nameof(containerName));
        }

        this.connectionString = connectionString;
        this.containerName = containerName;
        this.createContainerIfNotExists = createContainerIfNotExists;

        // Initialize BlobServiceClient lazily
        this.lazyBlobServiceClient = new Lazy<BlobServiceClient>(
            () => new BlobServiceClient(connectionString),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// Initializes a new instance of AzureBlobStorageProvider with a pre-configured BlobServiceClient.
    /// </summary>
    /// <param name="blobServiceClient">A pre-configured Azure Blob Service client.</param>
    /// <param name="containerName">The name of the blob container.</param>
    /// <param name="locationName">The logical name of this storage location.</param>
    /// <param name="createContainerIfNotExists">Whether to create the container if it doesn't exist.</param>
    public AzureBlobStorageProvider(
        string locationName,
        BlobServiceClient blobServiceClient,
        string containerName,
        bool createContainerIfNotExists = true) : base(locationName)
    {
        ArgumentNullException.ThrowIfNull(blobServiceClient);

        if (string.IsNullOrEmpty(containerName))
        {
            throw new ArgumentException("Container name cannot be null or empty", nameof(containerName));
        }

        this.containerName = containerName;
        this.createContainerIfNotExists = createContainerIfNotExists;

        // Initialize with provided BlobServiceClient
        this.lazyBlobServiceClient = new Lazy<BlobServiceClient>(() => blobServiceClient);
    }

    private BlobServiceClient BlobServiceClient => this.lazyBlobServiceClient.Value;

    private async Task<BlobContainerClient> GetContainerClientAsync()
    {
        var containerClient = this.BlobServiceClient.GetBlobContainerClient(this.containerName);

        if (this.createContainerIfNotExists)
        {
            await containerClient.CreateIfNotExistsAsync();
        }

        return containerClient;
    }

    private string NormalizePath(string path)
    {
        return path?.Replace('\\', '/').TrimStart('/');
    }

    private bool MatchesPattern(string fileName, string pattern)
    {
        // Simple wildcard matching for search patterns
        if (pattern == "*")
        {
            return true;
        }

        // Handle *.ext pattern
        if (pattern.StartsWith("*") && pattern.Length > 1)
        {
            var ext = pattern[1..];
            return fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase);
        }

        // Handle exact match
        return string.Equals(fileName, pattern, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public override async Task<Result> ExistsAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = await this.GetContainerClientAsync();
            var blobClient = containerClient.GetBlobClient(this.NormalizePath(path));

            var exists = await blobClient.ExistsAsync(cancellationToken);
            return Result.SuccessIf(exists.Value);
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to check if blob exists: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public override async Task<Result<Stream>> ReadFileAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = await this.GetContainerClientAsync();
            var blobClient = containerClient.GetBlobClient(this.NormalizePath(path));

            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                return Result<Stream>.Failure($"File '{path}' not found");
            }

            var downloadInfo = await blobClient.DownloadAsync(cancellationToken);
            var memoryStream = new MemoryStream();

            // Setup progress reporting
            //var totalBytes = downloadInfo.Value.ContentLength;
            //var downloadedBytes = 0L;

            await downloadInfo.Value.Content.CopyToAsync(memoryStream, 81920, cancellationToken);
            //bytesRead => ReportProgress(progress, path, downloadedBytes += bytesRead, totalBytes)); // CopyToAsync has no read action

            memoryStream.Position = 0;
            return Result<Stream>.Success(memoryStream);
        }
        catch (Exception ex)
        {
            return Result<Stream>.Failure($"Failed to read file: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public override async Task<Result> WriteFileAsync(string path, Stream content, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = await this.GetContainerClientAsync();
            var blobClient = containerClient.GetBlobClient(this.NormalizePath(path));

            // Setup progress reporting
            var totalBytes = content.Length;
            var options = new BlobUploadOptions
            {
                ProgressHandler = new Progress<long>(uploadedBytes =>
                    this.ReportProgress(progress, path, uploadedBytes, totalBytes))
            };

            await blobClient.UploadAsync(content, options, cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to write file: {ex.Message}");
        }
    }

    // Additional method implementations for AzureBlobStorageProvider

    /// <inheritdoc />
    public override async Task<Result> DeleteFileAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = await this.GetContainerClientAsync();
            var blobClient = containerClient.GetBlobClient(this.NormalizePath(path));

            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                return Result.Failure($"File '{path}' not found");
            }

            await blobClient.DeleteAsync(cancellationToken: cancellationToken);
            this.ReportProgress(progress, path, 0, 1, 1);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to delete file: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public override async Task<Result<string>> GetChecksumAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = await this.GetContainerClientAsync();
            var blobClient = containerClient.GetBlobClient(this.NormalizePath(path));

            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                return Result<string>.Failure($"File '{path}' not found");
            }

            var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);

            // Azure stores MD5 hash for blobs
            if (properties.Value.ContentHash?.Length > 0)
            {
                return Result<string>.Success(Convert.ToBase64String(properties.Value.ContentHash));
            }

            return Result<string>.Failure("MD5 hash not available for this blob");
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"Failed to get checksum: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public override async Task<Result<FileMetadata>> GetFileInfoAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = await this.GetContainerClientAsync();
            var blobClient = containerClient.GetBlobClient(this.NormalizePath(path));

            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                return Result<FileMetadata>.Failure($"File '{path}' not found");
            }

            var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);

            var metadata = new FileMetadata
            {
                Path = path,
                Length = properties.Value.ContentLength,
                LastModified = properties.Value.LastModified.DateTime,
            };

            return Result<FileMetadata>.Success(metadata);
        }
        catch (Exception ex)
        {
            return Result<FileMetadata>.Failure($"Failed to get file info: {ex.Message}");
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
                    this.MatchesPattern(Path.GetFileName(blobPath), searchPattern))
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
    public override async Task<Result> CopyFilesAsync(IEnumerable<(string SourcePath, string DestinationPath)> filePairs, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var errors = new List<string>();
            var totalFiles = filePairs.Count();
            var processedFiles = 0;

            foreach (var (sourcePath, destinationPath) in filePairs)
            {
                var result = await this.CopyFileAsync(sourcePath, destinationPath, null, cancellationToken);
                if (!result.IsSuccess)
                {
                    errors.Add($"Failed to copy '{sourcePath}' to '{destinationPath}': {result}");
                }

                processedFiles++;
                this.ReportProgress(progress, "Batch copy operation", 0, processedFiles, totalFiles);
            }

            if (errors.Count != 0)
            {
                return Result.Failure($"Some files failed to copy: {string.Join("; ", errors.ToString(", "))}");
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to copy files: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public override async Task<Result> MoveFilesAsync(IEnumerable<(string SourcePath, string DestinationPath)> filePairs, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var copyResult = await this.CopyFilesAsync(filePairs, progress, cancellationToken);
            if (!copyResult.IsSuccess)
            {
                return copyResult;
            }

            var errors = new List<string>();
            var totalFiles = filePairs.Count();
            var processedFiles = 0;

            foreach (var (sourcePath, _) in filePairs)
            {
                var result = await this.DeleteFileAsync(sourcePath, null, cancellationToken);
                if (!result.IsSuccess)
                {
                    errors.Add($"Failed to delete source file '{sourcePath}' after copying: {result}");
                }

                processedFiles++;
                this.ReportProgress(progress, "Batch move operation", 0, processedFiles, totalFiles);
            }

            if (errors.Count != 0)
            {
                return Result.Failure($"Some source files failed to delete after copying: {string.Join("; ", errors.ToString(", "))}");
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to move files: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public override async Task<Result> DeleteFilesAsync(IEnumerable<string> paths, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var errors = new List<string>();
            var pathsList = paths.ToList();
            var totalFiles = pathsList.Count;
            var processedFiles = 0;

            foreach (var path in pathsList)
            {
                var result = await this.DeleteFileAsync(path, null, cancellationToken);
                if (!result.IsSuccess)
                {
                    errors.Add($"Failed to delete '{path}': {result}");
                }

                processedFiles++;
                this.ReportProgress(progress, "Batch delete operation", 0, processedFiles, totalFiles);
            }

            if (errors.Count != 0)
            {
                return Result.Failure($"Some files failed to delete: {string.Join("; ", errors.ToString(", "))}");
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to delete files: {ex.Message}");
        }
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
                : directories.Where(dir => this.MatchesPattern(Path.GetFileName(dir.TrimEnd('/')), searchPattern));

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

    // Update the ReportProgress method to match the signature in your code
    private void ReportProgress(IProgress<FileProgress> progress, string path, long bytesProcessed, long filesProcessed, long totalFiles = 1)
    {
        progress?.Report(new FileProgress
        {
            BytesProcessed = bytesProcessed,
            FilesProcessed = filesProcessed,
            TotalFiles = totalFiles
        });
    }

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

    /// <inheritdoc />
    public override async async Task<Result> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if we can access the container
            var containerClient = await this.GetContainerClientAsync();
            await containerClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Health check failed: {ex.Message}");
        }
    }
}