// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using BridgingIT.DevKit.Common;

/// <summary>
/// Defines a contract for abstracting file system interactions across various storage types,
/// including traditional file shares and cloud storage systems like Azure Blob Storage.
/// Hierarchical structure is mimicked via path naming (e.g., blob names like "folder/subfolder/file.txt").
/// Containers or shares are configured during provider initialization.
/// All operations return a Result to handle success/failure with messages and typed errors.
/// </summary>
public interface IFileStorageProvider
{
    /// <summary>
    /// Gets the name of the storage location (e.g., root path or container name).
    /// </summary>
    string LocationName { get; }

    /// <summary>
    /// Gets a brief description of the storage provider (e.g., "Azure Blob Storage") and possible location details
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Indicates whether the provider supports notifications for changes (e.g., via FileSystemWatcher or cloud event subscriptions).
    /// Used in monitoring for real-time change detection; for broader use, this may indicate notification capabilities.
    /// </summary>
    bool SupportsNotifications { get; }

    /// <summary>
    /// Checks if a file or blob exists at the specified path, returning a Result with success or failure and errors.
    /// Example: `var result = await provider.ExistsAsync("folder/file.txt", null, CancellationToken.None); if (result.IsSuccess) Console.WriteLine("File exists");`
    /// </summary>
    /// <param name="path">The path to check (e.g., "folder/file.txt").</param>
    /// <param name="progress">Optional progress reporter for tracking bytes and files processed.</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation.</param>
    /// <returns>A Result indicating success or failure with typed errors (e.g., FileSystemError, PermissionError).</returns>
    Task<Result> FileExistsAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a readable stream for the file or blob at the specified path, returning a Result with the stream or errors.
    /// Example: `var result = await provider.ReadFileAsync("folder/file.txt", null, CancellationToken.None); if (result.IsSuccess) using (var stream = result.Value) { /* Read stream */ }`
    /// </summary>
    /// <param name="path">The path to the file (e.g., "folder/file.txt").</param>
    /// <param name="progress">Optional progress reporter for tracking bytes and files processed.</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation.</param>
    /// <returns>A Result containing the stream on success or failure with typed errors (e.g., FileNotFoundError, PermissionError).</returns>
    Task<Result<Stream>> ReadFileAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the provided stream to the file or blob at the specified path, returning a Result with success or errors.
    /// Example: `using (var stream = new MemoryStream()) await provider.WriteFileAsync("folder/file.txt", stream, null, CancellationToken.None);`
    /// </summary>
    /// <param name="path">The path to write to (e.g., "folder/file.txt").</param>
    /// <param name="content">The stream containing the data to write.</param>
    /// <param name="progress">Optional progress reporter for tracking bytes and files processed.</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation.</param>
    /// <returns>A Result indicating success or failure with typed errors (e.g., DiskFullError, PermissionError).</returns>
    Task<Result> WriteFileAsync(string path, Stream content, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the file or blob at the specified path, returning a Result with success or errors.
    /// Example: `var result = await provider.DeleteFileAsync("folder/file.txt", null, CancellationToken.None); if (result.IsSuccess) Console.WriteLine("File deleted");`
    /// </summary>
    /// <param name="path">The path to delete (e.g., "folder/file.txt").</param>
    /// <param name="progress">Optional progress reporter for tracking bytes and files processed.</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation.</param>
    /// <returns>A Result indicating success or failure with typed errors (e.g., FileNotFoundError, PermissionError).</returns>
    Task<Result> DeleteFileAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes and returns a checksum (e.g., hash) for the file or blob at the specified path, returning a Result with the checksum or errors.
    /// Example: `var result = await provider.GetChecksumAsync("folder/file.txt", CancellationToken.None); if (result.IsSuccess) Console.WriteLine($"Checksum: {result.Value}");`
    /// </summary>
    /// <param name="path">The path to the file (e.g., "folder/file.txt").</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation.</param>
    /// <returns>A Result containing the checksum on success or failure with typed errors (e.g., FileSystemError, PermissionError).</returns>
    Task<Result<string>> GetChecksumAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves metadata for the file or blob at the specified path, returning a Result with the metadata or errors.
    /// Example: `var result = await provider.GetFileInfoAsync("folder/file.txt", CancellationToken.None); if (result.IsSuccess) Console.WriteLine($"Size: {result.Value.Length}");`
    /// </summary>
    /// <param name="path">The path to the file (e.g., "folder/file.txt").</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation.</param>
    /// <returns>A Result containing the metadata on success or failure with typed errors (e.g., FileNotFoundError, PermissionError).</returns>
    Task<Result<FileMetadata>> GetFileMetadataAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets metadata for the file or blob at the specified path, returning a Result with success or errors.
    /// Example: `var metadata = new FileMetadata(); await provider.SetFileMetadataAsync("folder/file.txt", metadata, CancellationToken.None);`
    /// </summary>
    /// <param name="path">The path to the file (e.g., "folder/file.txt").</param>
    /// <param name="metadata">The metadata to set.</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation.</param>
    /// <returns>A Result indicating success or failure with typed errors (e.g., InvalidMetadataError, PermissionError).</returns>
    Task<Result> SetFileMetadataAsync(string path, FileMetadata metadata, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates specific fields in the metadata for the file or blob at the specified path, returning a Result with the updated metadata or errors.
    /// Example: `var result = await provider.UpdateFileMetadataAsync("folder/file.txt", m => m.WithLength(1024), CancellationToken.None); if (result.IsSuccess) Console.WriteLine($"Updated metadata: {result.Value.Length}");`
    /// </summary>
    /// <param name="path">The path to the file (e.g., "folder/file.txt").</param>
    /// <param name="metadataUpdate">A function that updates the existing metadata.</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation.</param>
    /// <returns>A Result containing the updated metadata on success or failure with typed errors (e.g., InvalidMetadataError, PermissionError).</returns>
    Task<Result<FileMetadata>> UpdateFileMetadataAsync(string path, Func<FileMetadata, FileMetadata> metadataUpdate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists files matching the search pattern under the specified path, returning a Result with the file list and continuation token or errors.
    /// For cloud storage, supports continuation tokens for scalability. If cancelled mid-scan, returns partial results with errors.
    /// Example: `var result = await provider.ListFilesAsync("folder/", "*.txt", true, null, CancellationToken.None); if (result.IsSuccess) foreach (var file in result.Value.Files) Console.WriteLine(file);`
    /// </summary>
    /// <param name="path">The base path (e.g., directory or container prefix).</param>
    /// <param name="searchPattern">Pattern to filter files (e.g., "*.txt").</param>
    /// <param name="recursive">Whether to include subdirectories (inferred from path hierarchy).</param>
    /// <param name="continuationToken">Token for pagination; null for first page, returned token for subsequent pages.</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation.</param>
    /// <returns>A Result containing (IEnumerable<string> Files, string NextContinuationToken) on success or failure with typed errors (e.g., AccessDeniedError, FileSystemError).</returns>
    Task<Result<(IEnumerable<string> Files, string NextContinuationToken)>> ListFilesAsync(
        string path, string searchPattern = null, bool recursive = false, string continuationToken = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies a file or blob from the source path to the destination path, returning a Result with success or errors.
    /// Example: `var result = await provider.CopyFileAsync("folder/source.txt", "folder/dest.txt", null, CancellationToken.None); if (result.IsSuccess) Console.WriteLine("File copied");`
    /// </summary>
    /// <param name="path">The source path to copy from (e.g., "folder/source.txt").</param>
    /// <param name="destinationPath">The destination path to copy to (e.g., "folder/destination.txt").</param>
    /// <param name="progress">Optional progress reporter for tracking bytes and files processed.</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation.</param>
    /// <returns>A Result indicating success or failure with typed errors (e.g., FileNotFoundError, PermissionError).</returns>
    Task<Result> CopyFileAsync(string path, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames a file or blob from the old path to the new path, returning a Result with success or errors.
    /// Example: `var result = await provider.RenameFileAsync("folder/old.txt", "folder/new.txt", null, CancellationToken.None); if (result.IsSuccess) Console.WriteLine("File renamed");`
    /// </summary>
    /// <param name="path">The current path of the file (e.g., "folder/old.txt").</param>
    /// <param name="destinationPath">The new path for the file (e.g., "folder/new.txt").</param>
    /// <param name="progress">Optional progress reporter for tracking bytes and files processed.</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation.</param>
    /// <returns>A Result indicating success or failure with typed errors (e.g., FileNotFoundError, PermissionError).</returns>
    Task<Result> RenameFileAsync(string path, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a file or blob from the source path to the destination path, returning a Result with success or errors.
    /// Example: `var result = await provider.MoveFileAsync("folder/source.txt", "folder/dest.txt", null, CancellationToken.None); if (result.IsSuccess) Console.WriteLine("File moved");`
    /// </summary>
    /// <param name="path">The source path to move from (e.g., "folder/source.txt").</param>
    /// <param name="destinationPath">The destination path to move to (e.g., "folder/destination.txt").</param>
    /// <param name="progress">Optional progress reporter for tracking bytes and files processed.</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation.</param>
    /// <returns>A Result indicating success or failure with typed errors (e.g., FileNotFoundError, PermissionError).</returns>
    Task<Result> MoveFileAsync(string path, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies multiple files or blobs from source paths to destination paths, returning a Result with success or errors.
    /// Example: `var result = await provider.CopyFilesAsync(new[] { ("folder/source.txt", "folder/dest.txt") }, null, CancellationToken.None); if (result.IsSuccess) Console.WriteLine("Files copied");`
    /// </summary>
    /// <param name="filePairs">A collection of source-destination path pairs to copy.</param>
    /// <param name="progress">Optional progress reporter for tracking bytes and files processed across all operations.</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation.</param>
    /// <returns>A Result indicating success or failure with typed errors (e.g., PartialCopyError, PermissionError).</returns>
    Task<Result> CopyFilesAsync(IEnumerable<(string SourcePath, string DestinationPath)> filePairs, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves multiple files or blobs from source paths to destination paths, returning a Result with success or errors.
    /// Example: `var result = await provider.MoveFilesAsync(new[] { ("folder/source.txt", "folder/dest.txt") }, null, CancellationToken.None); if (result.IsSuccess) Console.WriteLine("Files moved");`
    /// </summary>
    /// <param name="filePairs">A collection of source-destination path pairs to move.</param>
    /// <param name="progress">Optional progress reporter for tracking bytes and files processed across all operations.</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation.</param>
    /// <returns>A Result indicating success or failure with typed errors (e.g., PartialMoveError, PermissionError).</returns>
    Task<Result> MoveFilesAsync(IEnumerable<(string SourcePath, string DestinationPath)> filePairs, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes multiple files or blobs at the specified paths, returning a Result with success or errors.
    /// Example: `var result = await provider.DeleteFilesAsync(new[] { "folder/file.txt" }, null, CancellationToken.None); if (result.IsSuccess) Console.WriteLine("Files deleted");`
    /// </summary>
    /// <param name="paths">A collection of paths to delete.</param>
    /// <param name="progress">Optional progress reporter for tracking bytes and files processed across all operations.</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation.</param>
    /// <returns>A Result indicating success or failure with typed errors (e.g., PartialDeleteError, PermissionError).</returns>
    Task<Result> DeleteFilesAsync(IEnumerable<string> paths, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines if the specified path represents a directory (virtual or actual), returning a Result with success or errors.
    /// Example: `var result = await provider.IsDirectoryAsync("folder/subfolder", CancellationToken.None); if (result.IsSuccess) Console.WriteLine("Is a directory");`
    /// </summary>
    /// <param name="path">The path to check (e.g., "folder/subfolder").</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation.</param>
    /// <returns>A Result indicating success or failure with typed errors (e.g., PathNotFoundError, PermissionError).</returns>
    Task<Result> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a directory (or virtual folder) at the specified path, returning a Result with success or errors.
    /// Example: `var result = await provider.CreateDirectoryAsync("folder/subfolder", CancellationToken.None); if (result.IsSuccess) Console.WriteLine("Directory created");`
    /// </summary>
    /// <param name="path">The path to create (e.g., "folder/subfolder").</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation.</param>
    /// <returns>A Result indicating success or failure with typed errors (e.g., DirectoryExistsError, PermissionError).</returns>
    Task<Result> CreateDirectoryAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the directory (or virtual folder) at the specified path, returning a Result with success or errors.
    /// Example: `var result = await provider.DeleteDirectoryAsync("folder/subfolder", true, CancellationToken.None); if (result.IsSuccess) Console.WriteLine("Directory deleted");`
    /// </summary>
    /// <param name="path">The path to delete (e.g., "folder/subfolder").</param>
    /// <param name="recursive">Whether to delete contents recursively.</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation.</param>
    /// <returns>A Result indicating success or failure with typed errors (e.g., DirectoryNotEmptyError, PermissionError).</returns>
    Task<Result> DeleteDirectoryAsync(string path, bool recursive, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists directories matching the search pattern under the specified path, returning a Result with the directory list or errors.
    /// Example: `var result = await provider.ListDirectoriesAsync("folder/", "*sub*", true, CancellationToken.None); if (result.IsSuccess) foreach (var dir in result.Value) Console.WriteLine(dir);`
    /// </summary>
    /// <param name="path">The base path (e.g., directory or container prefix).</param>
    /// <param name="searchPattern">Pattern to filter directories (e.g., "*sub*").</param>
    /// <param name="recursive">Whether to include subdirectories.</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation.</param>
    /// <returns>A Result containing the directory list on success or failure with typed errors (e.g., AccessDeniedError, FileSystemError).</returns>
    Task<Result<IEnumerable<string>>> ListDirectoriesAsync(string path, string searchPattern = null, bool recursive = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks connectivity and basic access to the storage location, returning a Result with success or errors.
    /// Returns false if the location is unreachable or permissions are insufficient, triggering a health alert.
    /// Example: `var result = await provider.CheckHealthAsync(CancellationToken.None); if (result.IsSuccess) Console.WriteLine("Storage is healthy");`
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to abort the operation.</param>
    /// <returns>A Result indicating success or failure with typed errors (e.g., ConnectionError, PermissionError).</returns>
    Task<Result> CheckHealthAsync(CancellationToken cancellationToken = default);
}