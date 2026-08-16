// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;

/// <summary>
/// A behavior that retries failed file storage operations with exponential backoff, wrapping an IFileStorageProvider.
/// Uses structured logging with TypedLogger for consistent formatting, similar to RepositoryLoggingBehavior.
/// </summary>
public partial class RetryFileStorageBehavior(IFileStorageProvider innerProvider, ILoggerFactory loggerFactory, RetryOptions options)
    : IFileStorageBehavior
{
    private readonly string type = "FileStorage";
    private readonly IFileStorageProvider innerProvider = innerProvider ?? throw new ArgumentNullException(nameof(innerProvider));
    private readonly ILogger<RetryFileStorageBehavior> logger = loggerFactory?.CreateLogger<RetryFileStorageBehavior>() ?? NullLoggerFactory.Instance.CreateLogger<RetryFileStorageBehavior>();
    private readonly RetryOptions options = options ?? new RetryOptions();

    /// <summary>
    /// Gets the inner provider.
    /// </summary>
    public IFileStorageProvider InnerProvider => this.innerProvider;

    /// <summary>
    /// Gets the location name.
    /// </summary>
    public string LocationName => this.InnerProvider.LocationName;

    /// <summary>
    /// Gets the description.
    /// </summary>
    public string Description => this.InnerProvider.Description;

    /// <summary>
    /// Gets the supports notifications.
    /// </summary>
    public bool SupportsNotifications => this.InnerProvider.SupportsNotifications;

    private async Task<Result<T>> ExecuteWithRetryAsync<T>(Func<Task<Result<T>>> operation, string operationName, string path, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogRetryStart(this.logger, Constants.LogKey, this.type, operationName, path, this.options.MaxRetries);
        var retryPolicy = Policy
            .HandleResult<Result<T>>(r => r.IsFailure && this.IsRetryableError(r.Errors))
            .WaitAndRetryAsync(this.options.MaxRetries, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (del, timeSpan, retryCount, context) =>
                {
                    if (del.Result.IsFailure)
                    {
                        this.logger.LogError("[{LogKey}] file storage: retry {RetryCount}/{MaxRetries} for {Operation} on '{Path}' due to: {Error}", Constants.LogKey, retryCount, this.options.MaxRetries, operationName, path, del.Result.Errors.LastOrDefault()?.Message);
                        context["LastError"] = del;
                    }
                });

        try
        {
            var result = await retryPolicy.ExecuteAsync(async () =>
            {
                var opResult = await operation();
                if (opResult.IsFailure && !this.IsRetryableError(opResult.Errors))
                {
                    throw new InvalidOperationException($"Non-retryable error occurred: {opResult.Errors.LastOrDefault()?.Message}");
                }

                return opResult.Value;
            });

            this.logger.LogInformation("[{LogKey}] file storage: successfully completed {Operation} on '{Path}'", Constants.LogKey, operationName, path);
            return result;
        }
        catch (InvalidOperationException ex)
        {
            this.logger.LogError("[{LogKey}] file storage: failed {Operation} on '{Path}': {Error}", Constants.LogKey, operationName, path, ex.Message);
            throw;
        }
    }

    private async Task<Result> ExecuteWithRetryAsync(Func<Task<Result>> operation, string operationName, string path, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogRetryStart(this.logger, Constants.LogKey, this.type, operationName, path, this.options.MaxRetries);
        var retryPolicy = Policy
            .HandleResult<Result>(r => r.IsFailure && this.IsRetryableError(r.Errors))
            .WaitAndRetryAsync(this.options.MaxRetries, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (del, timeSpan, retryCount, context) =>
                {
                    if (del.Result.IsFailure)
                    {
                        this.logger.LogError("[{LogKey}] file storage: retry {RetryCount}/{MaxRetries} for {Operation} on '{Path}' due to: {Error}",
                            Constants.LogKey, retryCount, this.options.MaxRetries, operationName, path, del.Result.Errors.LastOrDefault()?.Message);
                        context["LastError"] = del;
                    }
                });

        try
        {
            var result = await retryPolicy.ExecuteAsync(async () =>
            {
                var opResult = await operation();
                if (opResult.IsFailure && !this.IsRetryableError(opResult.Errors))
                {
                    throw new InvalidOperationException($"Non-retryable error occurred: {opResult.Errors.LastOrDefault()?.Message}");
                }

                return opResult;
            });

            this.logger.LogInformation("[{LogKey}] file storage: successfully completed {Operation} on '{Path}' after {Retries} retries", Constants.LogKey, operationName, path, 0/*retryPolicy.State.Value.RetryCount*/);
            return result;
        }
        catch (InvalidOperationException ex)
        {
            this.logger.LogError("[{LogKey}] file storage: failed {Operation} on '{Path}' after {Retries} retries: {Error}", Constants.LogKey, operationName, path, 0/*retryPolicy.State.Value.RetryCount*/, ex.Message);
            throw;
        }
    }

    private bool IsRetryableError(IEnumerable<IResultError> errors)
    {
        return errors.Any(e => e is FileSystemError || e is FileSystemPermissionError || e is ExceptionError);
    }

    /// <summary>
    /// Executes the file exists operation.
    /// </summary>
    /// <param name="path">The path used by the operation.</param>
    /// <param name="progress">The progress used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<Result> FileExistsAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            return await this.ExecuteWithRetryAsync(() =>
                this.innerProvider.FileExistsAsync(path, progress, cancellationToken), "exists", path, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure()
                .WithError(ex.InnerException as IResultError ?? new ExceptionError(ex))
                .WithMessage($"Failed to check existence of file at '{path}' after retries");
        }
    }

    /// <summary>
    /// Executes the read file operation.
    /// </summary>
    /// <param name="path">The path used by the operation.</param>
    /// <param name="progress">The progress used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<Result<Stream>> ReadFileAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            return await this.ExecuteWithRetryAsync(() => this.innerProvider.ReadFileAsync(path, progress, cancellationToken), "read", path, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Result<Stream>.Failure()
                .WithError(ex.InnerException as IResultError ?? new ExceptionError(ex))
                .WithMessage($"Failed to read file at '{path}' after retries");
        }
    }

    /// <summary>
    /// Executes the write file operation.
    /// </summary>
    /// <param name="path">The path used by the operation.</param>
    /// <param name="content">The content used by the operation.</param>
    /// <param name="progress">The progress used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<Result> WriteFileAsync(string path, Stream content, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            return await this.ExecuteWithRetryAsync(() => this.innerProvider.WriteFileAsync(path, content, progress, cancellationToken), "write", path, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure()
                .WithError(ex.InnerException as IResultError ?? new ExceptionError(ex))
                .WithMessage($"Failed to write file at '{path}' after retries");
        }
    }

    /// <summary>
    /// Executes the open write file operation.
    /// </summary>
    /// <param name="path">The path used by the operation.</param>
    /// <param name="useTemporaryWrite">The use temporary write used by the operation.</param>
    /// <param name="progress">The progress used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<Result<Stream>> OpenWriteFileAsync(string path, bool useTemporaryWrite = false, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            return await this.ExecuteWithRetryAsync(() => this.innerProvider.OpenWriteFileAsync(path, useTemporaryWrite, progress, cancellationToken), "open write", path, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Result<Stream>.Failure()
                .WithError(ex.InnerException as IResultError ?? new ExceptionError(ex))
                .WithMessage($"Failed to open file for writing at '{path}' after retries");
        }
    }

    /// <summary>
    /// Deletes file.
    /// </summary>
    /// <param name="path">The path used by the operation.</param>
    /// <param name="progress">The progress used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<Result> DeleteFileAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            return await this.ExecuteWithRetryAsync(() => this.innerProvider.DeleteFileAsync(path, progress, cancellationToken), "delete", path, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure()
                .WithError(ex.InnerException as IResultError ?? new ExceptionError(ex))
                .WithMessage($"Failed to delete file at '{path}' after retries");
        }
    }

    /// <summary>
    /// Gets checksum.
    /// </summary>
    /// <param name="path">The path used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<Result<string>> GetChecksumAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            return await this.ExecuteWithRetryAsync(() => this.innerProvider.GetChecksumAsync(path, cancellationToken), "checksum", path, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Result<string>.Failure()
                .WithError(ex.InnerException as IResultError ?? new ExceptionError(ex))
                .WithMessage($"Failed to compute checksum for file at '{path}' after retries");
        }
    }

    /// <summary>
    /// Gets file metadata.
    /// </summary>
    /// <param name="path">The path used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<Result<FileMetadata>> GetFileMetadataAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            return await this.ExecuteWithRetryAsync(() => this.innerProvider.GetFileMetadataAsync(path, cancellationToken), "info", path, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Result<FileMetadata>.Failure()
                .WithError(ex.InnerException as IResultError ?? new ExceptionError(ex))
                .WithMessage($"Failed to retrieve metadata for file at '{path}' after retries");
        }
    }

    /// <summary>
    /// Executes the set file metadata operation.
    /// </summary>
    /// <param name="path">The path used by the operation.</param>
    /// <param name="metadata">The metadata used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<Result> SetFileMetadataAsync(string path, FileMetadata metadata, CancellationToken cancellationToken = default)
    {
        try
        {
            return await this.ExecuteWithRetryAsync(() => this.innerProvider.SetFileMetadataAsync(path, metadata, cancellationToken), "set metadata", path, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure()
                .WithError(ex.InnerException as IResultError ?? new ExceptionError(ex))
                .WithMessage($"Failed to set metadata for file at '{path}' after retries");
        }
    }

    /// <summary>
    /// Executes the update file metadata operation.
    /// </summary>
    /// <param name="path">The path used by the operation.</param>
    /// <param name="metadataUpdate">The metadata update used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<Result<FileMetadata>> UpdateFileMetadataAsync(string path, Func<FileMetadata, FileMetadata> metadataUpdate, CancellationToken cancellationToken = default)
    {
        try
        {
            return await this.ExecuteWithRetryAsync(() => this.innerProvider.UpdateFileMetadataAsync(path, metadataUpdate, cancellationToken), "update metadata", path, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Result<FileMetadata>.Failure()
                .WithError(ex.InnerException as IResultError ?? new ExceptionError(ex))
                .WithMessage($"Failed to update metadata for file at '{path}' after retries");
        }
    }

    /// <summary>
    /// Executes the list files operation.
    /// </summary>
    /// <param name="path">The path used by the operation.</param>
    /// <param name="searchPattern">The search pattern used by the operation.</param>
    /// <param name="recursive">The recursive used by the operation.</param>
    /// <param name="continuationToken">The continuation token used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<Result<(IEnumerable<string> Files, string NextContinuationToken)>> ListFilesAsync(
        string path, string searchPattern = null, bool recursive = false, string continuationToken = null, CancellationToken cancellationToken = default)
    {
        try
        {
            return await this.ExecuteWithRetryAsync(() => this.innerProvider.ListFilesAsync(path, searchPattern, recursive, continuationToken, cancellationToken), "list files", path, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Result<(IEnumerable<string> Files, string NextContinuationToken)>.Failure()
                .WithError(ex.InnerException as IResultError ?? new ExceptionError(ex))
                .WithMessage($"Failed to list files in '{path}' after retries");
        }
    }

    /// <summary>
    /// Executes the copy file operation.
    /// </summary>
    /// <param name="sourcePath">The source path used by the operation.</param>
    /// <param name="destinationPath">The destination path used by the operation.</param>
    /// <param name="progress">The progress used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<Result> CopyFileAsync(string sourcePath, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            return await this.ExecuteWithRetryAsync(() => this.innerProvider.CopyFileAsync(sourcePath, destinationPath, progress, cancellationToken), "copy", sourcePath, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure()
                .WithError(ex.InnerException as IResultError ?? new ExceptionError(ex))
                .WithMessage($"Failed to copy file from '{sourcePath}' to '{destinationPath}' after retries");
        }
    }

    /// <summary>
    /// Executes the rename file operation.
    /// </summary>
    /// <param name="oldPath">The old path used by the operation.</param>
    /// <param name="newPath">The new path used by the operation.</param>
    /// <param name="progress">The progress used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<Result> RenameFileAsync(string oldPath, string newPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            return await this.ExecuteWithRetryAsync(() => this.innerProvider.RenameFileAsync(oldPath, newPath, progress, cancellationToken), "rename", oldPath, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure()
                .WithError(ex.InnerException as IResultError ?? new ExceptionError(ex))
                .WithMessage($"Failed to rename file from '{oldPath}' to '{newPath}' after retries");
        }
    }

    /// <summary>
    /// Executes the rename directory operation.
    /// </summary>
    /// <param name="oldPath">The old path used by the operation.</param>
    /// <param name="newPath">The new path used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<Result> RenameDirectoryAsync(string oldPath, string newPath, CancellationToken cancellationToken = default)
    {
        try
        {
            return await this.ExecuteWithRetryAsync(() => this.innerProvider.RenameDirectoryAsync(oldPath, newPath, cancellationToken), "rename", oldPath, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure()
                .WithError(ex.InnerException as IResultError ?? new ExceptionError(ex))
                .WithMessage($"Failed to rename directory from '{oldPath}' to '{newPath}' after retries");
        }
    }

    /// <summary>
    /// Executes the move file operation.
    /// </summary>
    /// <param name="sourcePath">The source path used by the operation.</param>
    /// <param name="destinationPath">The destination path used by the operation.</param>
    /// <param name="progress">The progress used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<Result> MoveFileAsync(string sourcePath, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            return await this.ExecuteWithRetryAsync(() => this.innerProvider.MoveFileAsync(sourcePath, destinationPath, progress, cancellationToken), "move", sourcePath, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure()
                .WithError(ex.InnerException as IResultError ?? new ExceptionError(ex))
                .WithMessage($"Failed to move file from '{sourcePath}' to '{destinationPath}' after retries");
        }
    }

    /// <summary>
    /// Executes the copy files operation.
    /// </summary>
    /// <param name="filePairs">The file pairs used by the operation.</param>
    /// <param name="progress">The progress used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<Result> CopyFilesAsync(IEnumerable<(string SourcePath, string DestinationPath)> filePairs, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var pairDescriptions = string.Join(", ", filePairs.Select(p => $"({p.SourcePath} -> {p.DestinationPath})"));
            return await this.ExecuteWithRetryAsync(() => this.innerProvider.CopyFilesAsync(filePairs, progress, cancellationToken), "copy multiple", pairDescriptions, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure()
                .WithError(ex.InnerException as IResultError ?? new ExceptionError(ex))
                .WithMessage("Failed to copy multiple files after retries");
        }
    }

    /// <summary>
    /// Executes the move files operation.
    /// </summary>
    /// <param name="filePairs">The file pairs used by the operation.</param>
    /// <param name="progress">The progress used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<Result> MoveFilesAsync(IEnumerable<(string SourcePath, string DestinationPath)> filePairs, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var pairDescriptions = string.Join(", ", filePairs.Select(p => $"({p.SourcePath} -> {p.DestinationPath})"));
            return await this.ExecuteWithRetryAsync(() => this.innerProvider.MoveFilesAsync(filePairs, progress, cancellationToken), "move multiple", pairDescriptions, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure()
                .WithError(ex.InnerException as IResultError ?? new ExceptionError(ex))
                .WithMessage("Failed to move multiple files after retries");
        }
    }

    /// <summary>
    /// Deletes files.
    /// </summary>
    /// <param name="paths">The paths used by the operation.</param>
    /// <param name="progress">The progress used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<Result> DeleteFilesAsync(IEnumerable<string> paths, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var pathList = string.Join(", ", paths);
            return await this.ExecuteWithRetryAsync(() => this.innerProvider.DeleteFilesAsync(paths, progress, cancellationToken), "delete multiple", pathList, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure()
                .WithError(ex.InnerException as IResultError ?? new ExceptionError(ex))
                .WithMessage("Failed to delete multiple files after retries");
        }
    }

    /// <summary>
    /// Executes the directory exists operation.
    /// </summary>
    /// <param name="path">The path used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<Result> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            return await this.ExecuteWithRetryAsync(() => this.innerProvider.DirectoryExistsAsync(path, cancellationToken), "is directory", path, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure()
                .WithError(ex.InnerException as IResultError ?? new ExceptionError(ex))
                .WithMessage($"Failed to check if '{path}' is a directory after retries");
        }
    }

    /// <summary>
    /// Creates directory.
    /// </summary>
    /// <param name="path">The path used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<Result> CreateDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            return await this.ExecuteWithRetryAsync(() => this.innerProvider.CreateDirectoryAsync(path, cancellationToken), "create directory", path, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure()
                .WithError(ex.InnerException as IResultError ?? new ExceptionError(ex))
                .WithMessage($"Failed to create directory at '{path}' after retries");
        }
    }

    /// <summary>
    /// Deletes directory.
    /// </summary>
    /// <param name="path">The path used by the operation.</param>
    /// <param name="recursive">The recursive used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<Result> DeleteDirectoryAsync(string path, bool recursive, CancellationToken cancellationToken = default)
    {
        try
        {
            return await this.ExecuteWithRetryAsync(() => this.innerProvider.DeleteDirectoryAsync(path, recursive, cancellationToken), "delete directory", path, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure()
                .WithError(ex.InnerException as IResultError ?? new ExceptionError(ex))
                .WithMessage($"Failed to delete directory at '{path}' after retries");
        }
    }

    /// <summary>
    /// Executes the list directories operation.
    /// </summary>
    /// <param name="path">The path used by the operation.</param>
    /// <param name="searchPattern">The search pattern used by the operation.</param>
    /// <param name="recursive">The recursive used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<Result<IEnumerable<string>>> ListDirectoriesAsync(
        string path, string searchPattern = null, bool recursive = false, CancellationToken cancellationToken = default)
    {
        try
        {
            return await this.ExecuteWithRetryAsync(() => this.innerProvider.ListDirectoriesAsync(path, searchPattern, recursive, cancellationToken), "list directories", path, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Result<IEnumerable<string>>.Failure()
                .WithError(ex.InnerException as IResultError ?? new ExceptionError(ex))
                .WithMessage($"Failed to list directories in '{path}' after retries");
        }
    }

    /// <summary>
    /// Executes the check health operation.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task<Result> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await this.ExecuteWithRetryAsync(() => this.innerProvider.CheckHealthAsync(cancellationToken), "check health", this.innerProvider.LocationName, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure()
                .WithError(ex.InnerException as IResultError ?? new ExceptionError(ex))
                .WithMessage($"Failed to check health of storage at '{this.innerProvider.LocationName}' after retries");
        }
    }

    /// <summary>
    /// Represents typed logger.
    /// </summary>
    public static partial class TypedLogger
    {
        /// <summary>
        /// Writes a log entry for the retry start operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="locationName">The location name used by the operation.</param>
        /// <param name="operation">The operation used by the operation.</param>
        /// <param name="path">The path used by the operation.</param>
        /// <param name="maxRetries">The max retries used by the operation.</param>
        [LoggerMessage(0, LogLevel.Information, "[{LogKey}] file storage: retry start (type={LocationName}, operation={Operation}, path={Path}, maxRetries={MaxRetries})")]
        public static partial void LogRetryStart(ILogger logger, string logKey, string locationName, string operation, string path, int maxRetries);

        /// <summary>
        /// Writes a log entry for the retry attempt operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="locationName">The location name used by the operation.</param>
        /// <param name="retryCount">The retry count used by the operation.</param>
        /// <param name="maxRetries">The max retries used by the operation.</param>
        /// <param name="operation">The operation used by the operation.</param>
        /// <param name="path">The path used by the operation.</param>
        /// <param name="errors">The errors used by the operation.</param>
        [LoggerMessage(1, LogLevel.Warning, "[{LogKey}] file storage: retry {RetryCount}/{MaxRetries} for {Operation} on '{Path}' due to: {Errors} (type={LocationName})")]
        public static partial void LogRetryAttempt(ILogger logger, string logKey, string locationName, int retryCount, int maxRetries, string operation, string path, IEnumerable<IResultError> errors);

        /// <summary>
        /// Writes a log entry for the retry success operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="locationName">The location name used by the operation.</param>
        /// <param name="operation">The operation used by the operation.</param>
        /// <param name="path">The path used by the operation.</param>
        /// <param name="retries">The retries used by the operation.</param>
        [LoggerMessage(2, LogLevel.Information, "[{LogKey}] file storage: successfully completed {Operation} on '{Path}' after {Retries} retries  (type={LocationName})")]
        public static partial void LogRetrySuccess(ILogger logger, string logKey, string locationName, string operation, string path, int retries);

        /// <summary>
        /// Writes a log entry for the retry failure operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="locationName">The location name used by the operation.</param>
        /// <param name="operation">The operation used by the operation.</param>
        /// <param name="path">The path used by the operation.</param>
        /// <param name="retries">The retries used by the operation.</param>
        /// <param name="error">The error used by the operation.</param>
        [LoggerMessage(3, LogLevel.Error, "[{LogKey}] file storage: failed {Operation} on '{Path}' after {Retries} retries: {Error}  (type={LocationName})")]
        public static partial void LogRetryFailure(ILogger logger, string logKey, string locationName, string operation, string path, int retries, string error);
    }
}

/// <summary>
/// Configuration options for retry behavior.
/// </summary>
public class RetryOptions
{
    /// <summary>
    /// Gets or sets the max retries.
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}
