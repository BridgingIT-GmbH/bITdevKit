// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// A behavior that logs file storage operations, wrapping an IFileStorageProvider for logging before and after operations.
/// Uses structured logging with TypedLogger for consistent formatting, similar to RepositoryLoggingBehavior.
/// </summary>
public partial class LoggingFileStorageBehavior(IFileStorageProvider innerProvider, ILoggerFactory loggerFactory, LoggingOptions options = null)
    : IFileStorageBehavior
{
    private readonly IFileStorageProvider innerProvider = innerProvider ?? throw new ArgumentNullException(nameof(innerProvider));
    private readonly ILogger<LoggingFileStorageBehavior> logger = loggerFactory?.CreateLogger<LoggingFileStorageBehavior>() ?? NullLoggerFactory.Instance.CreateLogger<LoggingFileStorageBehavior>();
    private readonly LoggingOptions options = options ?? new LoggingOptions();

    public IFileStorageProvider InnerProvider => this.innerProvider;

    public string LocationName => this.InnerProvider.LocationName;

    public string Description => this.InnerProvider.Description;

    public bool SupportsNotifications => this.InnerProvider.SupportsNotifications;

    public async Task<Result> FileExistsAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogExists(this.logger, Constants.LogKey, this.innerProvider.LocationName, path);
        var result = await this.innerProvider.FileExistsAsync(path, progress, cancellationToken);
        if (result.IsSuccess)
        {
            this.logger.LogInformation("{LogKey} file storage: successfully checked existence of file at '{Path}'", Constants.LogKey, path);
        }
        else
        {
            this.logger.LogWarning("{LogKey} file storage: failed to check existence of file at '{Path}': {Errors}", Constants.LogKey, path, result.Errors);
        }
        return result;
    }

    public async Task<Result<Stream>> ReadFileAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogRead(this.logger, Constants.LogKey, this.innerProvider.LocationName, path);
        var result = await this.innerProvider.ReadFileAsync(path, progress, cancellationToken);
        if (result.IsSuccess)
        {
            this.logger.LogInformation("{LogKey} file storage: successfully read file at '{Path}'", Constants.LogKey, path);
        }
        else
        {
            this.logger.LogWarning("{LogKey} file storage: failed to read file at '{Path}': {Errors}", Constants.LogKey, path, result.Errors);
        }
        return result;
    }

    public async Task<Result> WriteFileAsync(string path, Stream content, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogWrite(this.logger, Constants.LogKey, this.innerProvider.LocationName, path);
        var result = await this.innerProvider.WriteFileAsync(path, content, progress, cancellationToken);
        if (result.IsSuccess)
        {
            this.logger.LogInformation("{LogKey} file storage: successfully wrote file at '{Path}'", Constants.LogKey, path);
        }
        else
        {
            this.logger.LogWarning("{LogKey} file storage: failed to write file at '{Path}': {Errors}", Constants.LogKey, path, result.Errors);
        }
        return result;
    }

    public async Task<Result> DeleteFileAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogDelete(this.logger, Constants.LogKey, this.innerProvider.LocationName, path);
        var result = await this.innerProvider.DeleteFileAsync(path, progress, cancellationToken);
        if (result.IsSuccess)
        {
            this.logger.LogInformation("{LogKey} file storage: successfully deleted file at '{Path}'", Constants.LogKey, path);
        }
        else
        {
            this.logger.LogWarning("{LogKey} file storage: failed to delete file at '{Path}': {Errors}", Constants.LogKey, path, result.Errors);
        }
        return result;
    }

    public async Task<Result<string>> GetChecksumAsync(string path, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogChecksum(this.logger, Constants.LogKey, this.innerProvider.LocationName, path);
        var result = await this.innerProvider.GetChecksumAsync(path, cancellationToken);
        if (result.IsSuccess)
        {
            this.logger.LogInformation("{LogKey} file storage: successfully computed checksum for file at '{Path}'", Constants.LogKey, path);
        }
        else
        {
            this.logger.LogWarning("{LogKey} file storage: failed to compute checksum for file at '{Path}': {Errors}", Constants.LogKey, path, result.Errors);
        }
        return result;
    }

    public async Task<Result<FileMetadata>> GetFileMetadataAsync(string path, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogInfo(this.logger, Constants.LogKey, this.innerProvider.LocationName, path);
        var result = await this.innerProvider.GetFileMetadataAsync(path, cancellationToken);
        if (result.IsSuccess)
        {
            this.logger.LogInformation("{LogKey} file storage: successfully retrieved metadata for file at '{Path}'", Constants.LogKey, path);
        }
        else
        {
            this.logger.LogWarning("{LogKey} file storage: failed to retrieve metadata for file at '{Path}': {Errors}", Constants.LogKey, path, result.Errors);
        }
        return result;
    }

    public async Task<Result> SetFileMetadataAsync(string path, FileMetadata metadata, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogSetMetadata(this.logger, Constants.LogKey, this.innerProvider.LocationName, path);
        var result = await this.innerProvider.SetFileMetadataAsync(path, metadata, cancellationToken);
        if (result.IsSuccess)
        {
            this.logger.LogInformation("{LogKey} file storage: successfully set metadata for file at '{Path}'", Constants.LogKey, path);
        }
        else
        {
            this.logger.LogWarning("{LogKey} file storage: failed to set metadata for file at '{Path}': {Errors}", Constants.LogKey, path, result.Errors);
        }
        return result;
    }

    public async Task<Result<FileMetadata>> UpdateFileMetadataAsync(string path, Func<FileMetadata, FileMetadata> metadataUpdate, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogUpdateMetadata(this.logger, Constants.LogKey, this.innerProvider.LocationName, path);
        var result = await this.innerProvider.UpdateFileMetadataAsync(path, metadataUpdate, cancellationToken);
        if (result.IsSuccess)
        {
            this.logger.LogInformation("{LogKey} file storage: successfully updated metadata for file at '{Path}'", Constants.LogKey, path);
        }
        else
        {
            this.logger.LogWarning("{LogKey} file storage: failed to update metadata for file at '{Path}': {Errors}", Constants.LogKey, path, result.Errors);
        }
        return result;
    }

    public async Task<Result<(IEnumerable<string> Files, string NextContinuationToken)>> ListFilesAsync(
        string path, string searchPattern = null, bool recursive = false, string continuationToken = null, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogListFiles(this.logger, Constants.LogKey, this.innerProvider.LocationName, path, searchPattern);
        var result = await this.innerProvider.ListFilesAsync(path, searchPattern, recursive, continuationToken, cancellationToken);
        if (result.IsSuccess)
        {
            this.logger.LogInformation("{LogKey} file storage: successfully listed files in '{Path}' with pattern '{Pattern}'", Constants.LogKey, path, searchPattern);
        }
        else
        {
            this.logger.LogWarning("{LogKey} file storage: failed to list files in '{Path}' with pattern '{Pattern}': {Errors}", Constants.LogKey, path, searchPattern, result.Errors);
        }
        return result;
    }

    public async Task<Result> CopyFileAsync(string sourcePath, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogCopy(this.logger, Constants.LogKey, this.innerProvider.LocationName, sourcePath, destinationPath);
        var result = await this.innerProvider.CopyFileAsync(sourcePath, destinationPath, progress, cancellationToken);
        if (result.IsSuccess)
        {
            this.logger.LogInformation("{LogKey} file storage: successfully copied file from '{Source}' to '{Destination}'", Constants.LogKey, sourcePath, destinationPath);
        }
        else
        {
            this.logger.LogWarning("{LogKey} file storage: failed to copy file from '{Source}' to '{Destination}': {Errors}", Constants.LogKey, sourcePath, destinationPath, result.Errors);
        }
        return result;
    }

    public async Task<Result> RenameFileAsync(string oldPath, string newPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogRename(this.logger, Constants.LogKey, this.innerProvider.LocationName, oldPath, newPath);
        var result = await this.innerProvider.RenameFileAsync(oldPath, newPath, progress, cancellationToken);
        if (result.IsSuccess)
        {
            this.logger.LogInformation("{LogKey} file storage: successfully renamed file from '{OldPath}' to '{NewPath}'", Constants.LogKey, oldPath, newPath);
        }
        else
        {
            this.logger.LogWarning("{LogKey} file storage: failed to rename file from '{OldPath}' to '{NewPath}': {Errors}", Constants.LogKey, oldPath, newPath, result.Errors);
        }
        return result;
    }

    public async Task<Result> MoveFileAsync(string sourcePath, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogMove(this.logger, Constants.LogKey, this.innerProvider.LocationName, sourcePath, destinationPath);
        var result = await this.innerProvider.MoveFileAsync(sourcePath, destinationPath, progress, cancellationToken);
        if (result.IsSuccess)
        {
            this.logger.LogInformation("{LogKey} file storage: successfully moved file from '{Source}' to '{Destination}'", Constants.LogKey, sourcePath, destinationPath);
        }
        else
        {
            this.logger.LogWarning("{LogKey} file storage: failed to move file from '{Source}' to '{Destination}': {Errors}", Constants.LogKey, sourcePath, destinationPath, result.Errors);
        }
        return result;
    }

    public async Task<Result> CopyFilesAsync(IEnumerable<(string SourcePath, string DestinationPath)> filePairs, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        var pairDescriptions = string.Join(", ", filePairs.Select(p => $"({p.SourcePath} -> {p.DestinationPath})"));
        TypedLogger.LogCopyMultiple(this.logger, Constants.LogKey, this.innerProvider.LocationName, pairDescriptions);
        var result = await this.innerProvider.CopyFilesAsync(filePairs, progress, cancellationToken);
        if (result.IsSuccess)
        {
            this.logger.LogInformation("{LogKey} file storage: successfully copied all files", Constants.LogKey);
        }
        else
        {
            this.logger.LogWarning("{LogKey} file storage: failed to copy multiple files: {Errors}", Constants.LogKey, result.Errors);
        }
        return result;
    }

    public async Task<Result> MoveFilesAsync(IEnumerable<(string SourcePath, string DestinationPath)> filePairs, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        var pairDescriptions = string.Join(", ", filePairs.Select(p => $"({p.SourcePath} -> {p.DestinationPath})"));
        TypedLogger.LogMoveMultiple(this.logger, Constants.LogKey, this.innerProvider.LocationName, pairDescriptions);
        var result = await this.innerProvider.MoveFilesAsync(filePairs, progress, cancellationToken);
        if (result.IsSuccess)
        {
            this.logger.LogInformation("{LogKey} file storage: successfully moved all files", Constants.LogKey);
        }
        else
        {
            this.logger.LogWarning("{LogKey} file storage: failed to move multiple files: {Errors}", Constants.LogKey, result.Errors);
        }
        return result;
    }

    public async Task<Result> DeleteFilesAsync(IEnumerable<string> paths, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        var pathList = string.Join(", ", paths);
        TypedLogger.LogDeleteMultiple(this.logger, Constants.LogKey, this.innerProvider.LocationName, pathList);
        var result = await this.innerProvider.DeleteFilesAsync(paths, progress, cancellationToken);
        if (result.IsSuccess)
        {
            this.logger.LogInformation("{LogKey} file storage: successfully deleted all files", Constants.LogKey);
        }
        else
        {
            this.logger.LogWarning("{LogKey} file storage: failed to delete multiple files: {Errors}", Constants.LogKey, result.Errors);
        }
        return result;
    }

    public async Task<Result> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogIsDirectory(this.logger, Constants.LogKey, this.innerProvider.LocationName, path);
        var result = await this.innerProvider.DirectoryExistsAsync(path, cancellationToken);
        if (result.IsSuccess)
        {
            this.logger.LogInformation("{LogKey} file storage: successfully checked if '{Path}' is a directory", Constants.LogKey, path);
        }
        else
        {
            this.logger.LogWarning("{LogKey} file storage: failed to check if '{Path}' is a directory: {Errors}", Constants.LogKey, path, result.Errors);
        }
        return result;
    }

    public async Task<Result> CreateDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogCreateDirectory(this.logger, Constants.LogKey, this.innerProvider.LocationName, path);
        var result = await this.innerProvider.CreateDirectoryAsync(path, cancellationToken);
        if (result.IsSuccess)
        {
            this.logger.LogInformation("{LogKey} file storage: successfully created directory at '{Path}'", Constants.LogKey, path);
        }
        else
        {
            this.logger.LogWarning("{LogKey} file storage: failed to create directory at '{Path}': {Errors}", Constants.LogKey, path, result.Errors);
        }
        return result;
    }

    public async Task<Result> DeleteDirectoryAsync(string path, bool recursive, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogDeleteDirectory(this.logger, Constants.LogKey, this.innerProvider.LocationName, path, recursive);
        var result = await this.innerProvider.DeleteDirectoryAsync(path, recursive, cancellationToken);
        if (result.IsSuccess)
        {
            this.logger.LogInformation("{LogKey} file storage: successfully deleted directory at '{Path}'", Constants.LogKey, path);
        }
        else
        {
            this.logger.LogWarning("{LogKey} file storage: failed to delete directory at '{Path}': {Errors}", Constants.LogKey, path, result.Errors);
        }
        return result;
    }

    public async Task<Result<IEnumerable<string>>> ListDirectoriesAsync(
        string path, string searchPattern = null, bool recursive = false, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogListDirectories(this.logger, Constants.LogKey, this.innerProvider.LocationName, path, searchPattern);
        var result = await this.innerProvider.ListDirectoriesAsync(path, searchPattern, recursive, cancellationToken);
        if (result.IsSuccess)
        {
            this.logger.LogInformation("{LogKey} file storage: successfully listed directories in '{Path}' with pattern '{Pattern}'", Constants.LogKey, path, searchPattern);
        }
        else
        {
            this.logger.LogWarning("{LogKey} file storage: failed to list directories in '{Path}' with pattern '{Pattern}': {Errors}", Constants.LogKey, path, searchPattern, result.Errors);
        }
        return result;
    }

    public async Task<Result> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        TypedLogger.LogCheckHealth(this.logger, Constants.LogKey, this.innerProvider.LocationName, this.innerProvider.LocationName);
        var result = await this.innerProvider.CheckHealthAsync(cancellationToken);
        if (result.IsSuccess)
        {
            this.logger.LogInformation("{LogKey} file storage: successfully checked health of storage at '{Location}'", Constants.LogKey, this.innerProvider.LocationName);
        }
        else
        {
            this.logger.LogWarning("{LogKey} file storage: failed to check health of storage at '{Location}': {Errors}", Constants.LogKey, this.innerProvider.LocationName, result.Errors);
        }
        return result;
    }

    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Information, "{LogKey} file storage: exists (type={LocationName}, path={Path})")]
        public static partial void LogExists(ILogger logger, string logKey, string locationName, string path);

        [LoggerMessage(1, LogLevel.Information, "{LogKey} file storage: read (type={LocationName}, path={Path})")]
        public static partial void LogRead(ILogger logger, string logKey, string locationName, string path);

        [LoggerMessage(2, LogLevel.Information, "{LogKey} file storage: write (type={LocationName}, path={Path})")]
        public static partial void LogWrite(ILogger logger, string logKey, string locationName, string path);

        [LoggerMessage(3, LogLevel.Information, "{LogKey} file storage: delete (type={LocationName}, path={Path})")]
        public static partial void LogDelete(ILogger logger, string logKey, string locationName, string path);

        [LoggerMessage(4, LogLevel.Information, "{LogKey} file storage: checksum (type={LocationName}, path={Path})")]
        public static partial void LogChecksum(ILogger logger, string logKey, string locationName, string path);

        [LoggerMessage(5, LogLevel.Information, "{LogKey} file storage: info (type={LocationName}, path={Path})")]
        public static partial void LogInfo(ILogger logger, string logKey, string locationName, string path);

        [LoggerMessage(6, LogLevel.Information, "{LogKey} file storage: set metadata (type={LocationName}, path={Path})")]
        public static partial void LogSetMetadata(ILogger logger, string logKey, string locationName, string path);

        [LoggerMessage(7, LogLevel.Information, "{LogKey} file storage: update metadata (type={LocationName}, path={Path})")]
        public static partial void LogUpdateMetadata(ILogger logger, string logKey, string locationName, string path);

        [LoggerMessage(8, LogLevel.Information, "{LogKey} file storage: list files (type={LocationName}, path={Path}, pattern={Pattern})")]
        public static partial void LogListFiles(ILogger logger, string logKey, string locationName, string path, string pattern);

        [LoggerMessage(9, LogLevel.Information, "{LogKey} file storage: copy (type={LocationName}, source={Source}, destination={Destination})")]
        public static partial void LogCopy(ILogger logger, string logKey, string locationName, string source, string destination);

        [LoggerMessage(10, LogLevel.Information, "{LogKey} file storage: rename (type={LocationName}, oldPath={OldPath}, newPath={NewPath})")]
        public static partial void LogRename(ILogger logger, string logKey, string locationName, string oldPath, string newPath);

        [LoggerMessage(11, LogLevel.Information, "{LogKey} file storage: move (type={LocationName}, source={Source}, destination={Destination})")]
        public static partial void LogMove(ILogger logger, string logKey, string locationName, string source, string destination);

        [LoggerMessage(12, LogLevel.Information, "{LogKey} file storage: copy multiple (type={LocationName}, pairs={Pairs})")]
        public static partial void LogCopyMultiple(ILogger logger, string logKey, string locationName, string pairs);

        [LoggerMessage(13, LogLevel.Information, "{LogKey} file storage: move multiple (type={LocationName}, pairs={Pairs})")]
        public static partial void LogMoveMultiple(ILogger logger, string logKey, string locationName, string pairs);

        [LoggerMessage(14, LogLevel.Information, "{LogKey} file storage: delete multiple (type={LocationName}, paths={Paths})")]
        public static partial void LogDeleteMultiple(ILogger logger, string logKey, string locationName, string paths);

        [LoggerMessage(15, LogLevel.Information, "{LogKey} file storage: is directory (type={LocationName}, path={Path})")]
        public static partial void LogIsDirectory(ILogger logger, string logKey, string locationName, string path);

        [LoggerMessage(16, LogLevel.Information, "{LogKey} file storage: create directory (type={LocationName}, path={Path})")]
        public static partial void LogCreateDirectory(ILogger logger, string logKey, string locationName, string path);

        [LoggerMessage(17, LogLevel.Information, "{LogKey} file storage: delete directory (type={LocationName}, path={Path}, recursive={Recursive})")]
        public static partial void LogDeleteDirectory(ILogger logger, string logKey, string locationName, string path, bool recursive);

        [LoggerMessage(18, LogLevel.Information, "{LogKey} file storage: list directories (type={LocationName}, path={Path}, pattern={Pattern})")]
        public static partial void LogListDirectories(ILogger logger, string logKey, string locationName, string path, string pattern);

        [LoggerMessage(19, LogLevel.Information, "{LogKey} file storage: check health (type={LocationName}, location={Location})")]
        public static partial void LogCheckHealth(ILogger logger, string logKey, string locationName, string location);
    }
}

/// <summary>
/// Configuration options for logging behavior.
/// </summary>
public class LoggingOptions
{
    public LogLevel MinLevel { get; set; } = LogLevel.Information;
}