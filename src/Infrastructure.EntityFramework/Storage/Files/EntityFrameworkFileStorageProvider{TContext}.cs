// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BridgingIT.DevKit.Application.Storage;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Provides the foundation surface for an Entity Framework backed <see cref="IFileStorageProvider" />.
/// </summary>
/// <typeparam name="TContext">The database context type that implements <see cref="IFileStorageContext" />.</typeparam>
/// <example>
/// <code>
/// services.AddDbContext&lt;AppDbContext&gt;(options => options.UseSqlServer(connectionString));
///
/// services.AddFileStorage(factory => factory.RegisterProvider("db", builder => builder
///     .UseEntityFramework&lt;AppDbContext&gt;(
///         "DatabaseFiles",
///         configure: options =>
///         {
///             options.LeaseDuration(TimeSpan.FromSeconds(30))
///                 .RetryCount(3)
///                 .PageSize(100);
///         })));
/// </code>
/// </example>
public class EntityFrameworkFileStorageProvider<TContext> : BaseFileStorageProvider
    where TContext : DbContext, IFileStorageContext
{
    private readonly IServiceProvider serviceProvider;
    private readonly EntityFrameworkFileStorageOptions options;
    private readonly string leaseOwner = $"{Environment.MachineName}:{Guid.NewGuid():N}";
    private readonly ILogger logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityFrameworkFileStorageProvider{TContext}" /> class.
    /// </summary>
    /// <param name="serviceProvider">The root service provider used to create scoped database contexts.</param>
    /// <param name="loggerFactory">The logger factory used to initialize runtime options.</param>
    /// <param name="locationName">The logical storage location name.</param>
    /// <param name="description">An optional human-readable provider description.</param>
    /// <param name="options">Optional runtime options for the provider.</param>
    public EntityFrameworkFileStorageProvider(
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory,
        string locationName,
        string description = null,
        EntityFrameworkFileStorageOptions options = null)
        : base(locationName)
    {
        this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        this.options = options ?? new EntityFrameworkFileStorageOptions();
        this.options.LoggerFactory ??= loggerFactory ?? NullLoggerFactory.Instance;
        this.logger = this.options.CreateLogger<EntityFrameworkFileStorageProvider<TContext>>();
        this.Description = description ?? locationName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityFrameworkFileStorageProvider{TContext}" /> class
    /// from configuration values.
    /// </summary>
    /// <param name="serviceProvider">The root service provider used to create scoped database contexts.</param>
    /// <param name="loggerFactory">The logger factory used to initialize runtime options.</param>
    /// <param name="locationName">The logical storage location name.</param>
    /// <param name="description">An optional human-readable provider description.</param>
    /// <param name="configuration">Optional configuration values that are projected into runtime options.</param>
    public EntityFrameworkFileStorageProvider(
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory,
        string locationName,
        string description,
        EntityFrameworkFileStorageConfiguration configuration)
        : this(
            serviceProvider,
            loggerFactory,
            locationName,
            description,
            CreateOptions(loggerFactory, configuration))
    {
    }

    /// <summary>
    /// Gets a value indicating whether this provider supports change notifications.
    /// </summary>
    public override bool SupportsNotifications { get; } = false;

    /// <summary>
    /// Gets the runtime options currently used by the provider.
    /// </summary>
    protected EntityFrameworkFileStorageOptions Options => this.options;

    /// <inheritdoc />
    public override async Task<Result> FileExistsAsync(
        string path,
        IProgress<FileProgress> progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
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
            using var scope = this.CreateScope();
            var context = this.ResolveContext(scope);
            var readyResult = await this.EnsureStorageReadyAsync(context, cancellationToken);
            if (readyResult.IsFailure)
            {
                return Result.Failure()
                    .WithErrors(readyResult.Errors)
                    .WithMessages(readyResult.Messages);
            }

            var normalizedPath = this.NormalizePath(path);
            var file = await this.TryGetFileAsync(context, normalizedPath, cancellationToken);
            if (file is null)
            {
                return Result.Failure()
                    .WithError(new NotFoundError("File not found"))
                    .WithMessage($"Failed to check existence of file at '{path}'");
            }

            progress?.Report(new FileProgress
            {
                BytesProcessed = file.Length,
                FilesProcessed = 1,
                TotalFiles = 1
            });

            return Result.Success()
                .WithMessage($"Checked existence of file at '{path}'");
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during file existence check"))
                .WithMessage($"Cancelled checking existence of file at '{path}'");
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Entity Framework file existence check failed for {LocationName} and {Path}", this.LocationName, path);

            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error checking file existence at '{path}'");
        }
    }

    /// <inheritdoc />
    public override async Task<Result<Stream>> ReadFileAsync(
        string path,
        IProgress<FileProgress> progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
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
            using var scope = this.CreateScope();
            var context = this.ResolveContext(scope);
            var readyResult = await this.EnsureStorageReadyAsync(context, cancellationToken);
            if (readyResult.IsFailure)
            {
                return Result<Stream>.Failure()
                    .WithErrors(readyResult.Errors)
                    .WithMessages(readyResult.Messages);
            }

            var normalizedPath = this.NormalizePath(path);
            var file = await this.TryGetFileAsync(context, normalizedPath, cancellationToken);
            if (file is null)
            {
                return Result<Stream>.Failure()
                    .WithError(new FileSystemError("File not found", path))
                    .WithMessage($"Failed to read file at '{path}'");
            }

            var content = await this.TryGetFileContentAsync(context, file.Id, cancellationToken);
            if (content is null)
            {
                return Result<Stream>.Failure()
                    .WithError(new FileSystemError("File content not found", path))
                    .WithMessage($"Failed to read file at '{path}'");
            }

            var validationResult = this.ValidateContentConsistency(file, content, path);
            if (validationResult.IsFailure)
            {
                return Result<Stream>.Failure()
                    .WithErrors(validationResult.Errors)
                    .WithMessages(validationResult.Messages);
            }

            var stream = this.DeserializeContent(content);
            if (stream.CanSeek && stream.Length != file.Length)
            {
                await stream.DisposeAsync();

                return Result<Stream>.Failure()
                    .WithError(new FileSystemError("Stored file length does not match payload", path))
                    .WithMessage($"Failed to read file at '{path}'");
            }

            progress?.Report(new FileProgress
            {
                BytesProcessed = file.Length,
                FilesProcessed = 1,
                TotalFiles = 1
            });

            return Result<Stream>.Success(stream)
                .WithMessage($"Read file at '{path}'");
        }
        catch (OperationCanceledException)
        {
            return Result<Stream>.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during file read"))
                .WithMessage($"Cancelled reading file at '{path}'");
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Entity Framework file read failed for {LocationName} and {Path}", this.LocationName, path);

            return Result<Stream>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error reading file at '{path}'");
        }
    }

    /// <inheritdoc />
    public override async Task<Result> WriteFileAsync(
        string path,
        Stream content,
        IProgress<FileProgress> progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Result.Failure()
                .WithError(new FileSystemError("Path cannot be null or empty", path))
                .WithMessage("Invalid path provided");
        }

        if (content is null)
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
            await using var buffered = await this.BufferContentAsync(content, cancellationToken);
            return await this.WriteFilePayloadAsync(path, buffered.ToArray(), progress, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during file write"))
                .WithMessage($"Cancelled writing file at '{path}'");
        }
        catch (BufferedContentLimitExceededException ex)
        {
            return this.CreateBufferedContentLimitFailure(path, ex, $"Failed to write file at '{path}'");
        }
        catch (DbUpdateException ex)
        {
            return await this.TranslateMutationDbUpdateExceptionAsync(path, ex, $"Failed to write file at '{path}'", cancellationToken);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Entity Framework file write failed for {LocationName} and {Path}", this.LocationName, path);

            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error writing file at '{path}'");
        }
    }

    /// <inheritdoc />
    public override Task<Result<Stream>> OpenWriteFileAsync(
        string path,
        bool useTemporaryWrite = false,
        IProgress<FileProgress> progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.FromResult(Result<Stream>.Failure()
                .WithError(new FileSystemError("Path cannot be null or empty", path))
                .WithMessage("Invalid path provided"));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<Stream>.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled opening file for writing at '{path}'"));
        }

        try
        {
            var stream = new MemoryStream();

            return Task.FromResult(Result<Stream>.Success(new OpenWriteFileStream(
                    stream,
                    progress,
                    cancellationToken,
                    onSuccessAsync: async () =>
                    {
                        var result = await this.WriteFilePayloadAsync(path, stream.ToArray(), null, cancellationToken);
                        if (result.IsFailure)
                        {
                            throw this.CreateMutationCommitException(result, path);
                        }
                    }))
                .WithMessage($"Opened file for writing at '{path}'"));
        }
        catch (OperationCanceledException)
        {
            return Task.FromResult(Result<Stream>.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during open for write"))
                .WithMessage($"Cancelled opening file for writing at '{path}'"));
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Entity Framework open-write initialization failed for {LocationName} and {Path}", this.LocationName, path);

            return Task.FromResult(Result<Stream>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error opening file for writing at '{path}'"));
        }
    }

    /// <inheritdoc />
    public override async Task<Result> DeleteFileAsync(
        string path,
        IProgress<FileProgress> progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
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
            return await this.ExecuteMutationAsync(
                [path],
                async (mutationContext, cancellationToken) =>
                {
                    var mutationPath = mutationContext.PrimaryPath;
                    if (mutationPath.NormalizedPath.Length == 0)
                    {
                        return Result.Failure()
                            .WithError(new FileSystemError("Path cannot resolve to the storage root", path))
                            .WithMessage("Invalid path provided");
                    }

                    var file = await this.TryGetTrackedFileAsync(mutationContext.Context, mutationPath.NormalizedPath, includeContent: true, cancellationToken);
                    if (file is null)
                    {
                        var directoryConflict = await this.TryGetTrackedDirectoryAsync(mutationContext.Context, mutationPath.NormalizedPath, cancellationToken);
                        return directoryConflict is not null
                            ? this.CreatePathConflictFailure(path, $"Failed to delete file at '{path}'", "directory", mutationPath.NormalizedPath)
                            : Result.Failure()
                                .WithError(new FileSystemError("File not found", path))
                                .WithMessage($"Failed to delete file at '{path}'");
                    }

                    if (file.Content is not null)
                    {
                        mutationContext.Context.StorageFileContents.Remove(file.Content);
                    }

                    mutationContext.Context.StorageFiles.Remove(file);

                    var parentDirectory = mutationPath.ParentPath is null
                        ? null
                        : await this.TryGetTrackedDirectoryAsync(mutationContext.Context, mutationPath.ParentPath, cancellationToken);
                    if (parentDirectory is not null)
                    {
                        this.TouchDirectory(parentDirectory, mutationContext.Timestamp);
                    }

                    await mutationContext.Context.SaveChangesAsync(cancellationToken);

                    await this.PruneImplicitDirectoriesAsync(mutationContext.Context, mutationPath.ParentPath, mutationContext.Timestamp, cancellationToken);
                    await mutationContext.Context.SaveChangesAsync(cancellationToken);

                    progress?.Report(new FileProgress
                    {
                        BytesProcessed = file.Length,
                        FilesProcessed = 1,
                        TotalFiles = 1
                    });

                    return Result.Success()
                        .WithMessage($"Deleted file at '{path}'");
                },
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during file deletion"))
                .WithMessage($"Cancelled deleting file at '{path}'");
        }
        catch (DbUpdateException ex)
        {
            return await this.TranslateMutationDbUpdateExceptionAsync(path, ex, $"Failed to delete file at '{path}'", cancellationToken);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Entity Framework file delete failed for {LocationName} and {Path}", this.LocationName, path);

            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error deleting file at '{path}'");
        }
    }

    /// <inheritdoc />
    public override async Task<Result<string>> GetChecksumAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
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
            using var scope = this.CreateScope();
            var context = this.ResolveContext(scope);
            var readyResult = await this.EnsureStorageReadyAsync(context, cancellationToken);
            if (readyResult.IsFailure)
            {
                return Result<string>.Failure()
                    .WithErrors(readyResult.Errors)
                    .WithMessages(readyResult.Messages);
            }

            var normalizedPath = this.NormalizePath(path);
            var file = await this.TryGetFileAsync(context, normalizedPath, cancellationToken);
            if (file is null)
            {
                return Result<string>.Failure()
                    .WithError(new FileSystemError("File not found", path))
                    .WithMessage($"Failed to compute checksum for file at '{path}'");
            }

            if (!string.IsNullOrEmpty(file.Checksum))
            {
                return Result<string>.Success(file.Checksum)
                    .WithMessage($"Computed checksum for file at '{path}'");
            }

            var content = await this.TryGetFileContentAsync(context, file.Id, cancellationToken);
            if (content is null)
            {
                return Result<string>.Failure()
                    .WithError(new FileSystemError("File content not found", path))
                    .WithMessage($"Failed to compute checksum for file at '{path}'");
            }

            var validationResult = this.ValidateContentConsistency(file, content, path);
            if (validationResult.IsFailure)
            {
                return Result<string>.Failure()
                    .WithErrors(validationResult.Errors)
                    .WithMessages(validationResult.Messages);
            }

            await using var stream = this.DeserializeContent(content);
            var checksum = await ComputeChecksumAsync(stream, cancellationToken);

            return Result<string>.Success(checksum)
                .WithMessage($"Computed checksum for file at '{path}'");
        }
        catch (OperationCanceledException)
        {
            return Result<string>.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during checksum computation"))
                .WithMessage($"Cancelled computing checksum for file at '{path}'");
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Entity Framework checksum lookup failed for {LocationName} and {Path}", this.LocationName, path);

            return Result<string>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error computing checksum for file at '{path}'");
        }
    }

    /// <inheritdoc />
    public override async Task<Result<FileMetadata>> GetFileMetadataAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
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
            using var scope = this.CreateScope();
            var context = this.ResolveContext(scope);
            var readyResult = await this.EnsureStorageReadyAsync(context, cancellationToken);
            if (readyResult.IsFailure)
            {
                return Result<FileMetadata>.Failure()
                    .WithErrors(readyResult.Errors)
                    .WithMessages(readyResult.Messages);
            }

            var normalizedPath = this.NormalizePath(path);
            if (normalizedPath.Length == 0)
            {
                return Result<FileMetadata>.Failure()
                    .WithError(new FileSystemError("File or directory not found", path))
                    .WithMessage($"Failed to retrieve metadata for file at '{path}'");
            }

            var file = await this.TryGetFileAsync(context, normalizedPath, cancellationToken);
            if (file is not null)
            {
                return Result<FileMetadata>.Success(new FileMetadata
                {
                    Path = file.NormalizedPath,
                    Length = file.Length,
                    LastModified = file.LastModified.UtcDateTime
                }).WithMessage($"Retrieved metadata for file at '{path}'");
            }

            var directory = await this.TryGetDirectoryAsync(context, normalizedPath, cancellationToken);
            if (directory is not null)
            {
                return Result<FileMetadata>.Success(new FileMetadata
                {
                    Path = directory.NormalizedPath,
                    Length = 0,
                    LastModified = directory.LastModified.UtcDateTime
                }).WithMessage($"Retrieved metadata for file at '{path}'");
            }

            return Result<FileMetadata>.Failure()
                .WithError(new FileSystemError("File or directory not found", path))
                .WithMessage($"Failed to retrieve metadata for file at '{path}'");
        }
        catch (OperationCanceledException)
        {
            return Result<FileMetadata>.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during metadata lookup"))
                .WithMessage($"Cancelled retrieving metadata for file at '{path}'");
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Entity Framework metadata lookup failed for {LocationName} and {Path}", this.LocationName, path);

            return Result<FileMetadata>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error retrieving metadata for file at '{path}'");
        }
    }

    /// <inheritdoc />
    public override async Task<Result> SetFileMetadataAsync(string path, FileMetadata metadata, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Result.Failure()
                .WithError(new FileSystemError("Path cannot be null or empty", path))
                .WithMessage("Invalid path provided");
        }

        if (metadata is null)
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

        try
        {
            return await this.ExecuteMutationAsync(
                [path],
                async (mutationContext, cancellationToken) =>
                {
                    var mutationPath = mutationContext.PrimaryPath;
                    var timestamp = metadata.LastModified?.ToUniversalTime();
                    var file = await this.TryGetTrackedFileAsync(mutationContext.Context, mutationPath.NormalizedPath, includeContent: false, cancellationToken);
                    if (file is not null)
                    {
                        if (timestamp.HasValue)
                        {
                            this.TouchFile(file, timestamp.Value);
                            await this.TouchParentDirectoryAsync(mutationContext.Context, mutationPath.ParentPath, mutationContext.Timestamp, cancellationToken);
                            await mutationContext.Context.SaveChangesAsync(cancellationToken);
                        }

                        return Result.Success()
                            .WithMessage($"Set metadata for file at '{path}'");
                    }

                    var directory = await this.TryGetTrackedDirectoryAsync(mutationContext.Context, mutationPath.NormalizedPath, cancellationToken);
                    if (directory is not null)
                    {
                        if (timestamp.HasValue)
                        {
                            this.TouchDirectory(directory, timestamp.Value);
                            await this.TouchParentDirectoryAsync(mutationContext.Context, directory.ParentPath, mutationContext.Timestamp, cancellationToken);
                            await mutationContext.Context.SaveChangesAsync(cancellationToken);
                        }

                        return Result.Success()
                            .WithMessage($"Set metadata for file at '{path}'");
                    }

                    return Result.Failure()
                        .WithError(new FileSystemError("File not found", path))
                        .WithMessage($"Failed to set metadata for file at '{path}'");
                },
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during metadata update"))
                .WithMessage($"Cancelled setting metadata for file at '{path}'");
        }
        catch (DbUpdateException ex)
        {
            return await this.TranslateMutationDbUpdateExceptionAsync(path, ex, $"Failed to set metadata for file at '{path}'", cancellationToken);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Entity Framework metadata update failed for {LocationName} and {Path}", this.LocationName, path);

            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error setting metadata for file at '{path}'");
        }
    }

    /// <inheritdoc />
    public override async Task<Result<FileMetadata>> UpdateFileMetadataAsync(
        string path,
        Func<FileMetadata, FileMetadata> metadataUpdate,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Result<FileMetadata>.Failure()
                .WithError(new FileSystemError("Path cannot be null or empty", path))
                .WithMessage("Invalid path provided");
        }

        if (metadataUpdate is null)
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

        try
        {
            var currentMetadataResult = await this.GetFileMetadataAsync(path, cancellationToken);
            if (currentMetadataResult.IsFailure)
            {
                return Result<FileMetadata>.Failure()
                    .WithErrors(currentMetadataResult.Errors)
                    .WithMessages(currentMetadataResult.Messages);
            }

            var updatedMetadata = metadataUpdate(currentMetadataResult.Value);
            if (updatedMetadata is null)
            {
                return Result<FileMetadata>.Failure()
                    .WithError(new ValidationError("Metadata update function must return metadata", nameof(metadataUpdate)))
                    .WithMessage($"Failed to update metadata for file at '{path}'");
            }

            var setResult = await this.SetFileMetadataAsync(path, updatedMetadata, cancellationToken);
            if (setResult.IsFailure)
            {
                return Result<FileMetadata>.Failure()
                    .WithErrors(setResult.Errors)
                    .WithMessages(setResult.Messages);
            }

            var refreshedMetadataResult = await this.GetFileMetadataAsync(path, cancellationToken);
            if (refreshedMetadataResult.IsFailure)
            {
                return Result<FileMetadata>.Failure()
                    .WithErrors(refreshedMetadataResult.Errors)
                    .WithMessages(refreshedMetadataResult.Messages);
            }

            return Result<FileMetadata>.Success(refreshedMetadataResult.Value)
                .WithMessage($"Updated metadata for file at '{path}'");
        }
        catch (OperationCanceledException)
        {
            return Result<FileMetadata>.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during metadata update"))
                .WithMessage($"Cancelled updating metadata for file at '{path}'");
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Entity Framework metadata refresh failed for {LocationName} and {Path}", this.LocationName, path);

            return Result<FileMetadata>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error updating metadata for file at '{path}'");
        }
    }

    /// <inheritdoc />
    public override async Task<Result> CopyFileAsync(
        string path,
        string destinationPath,
        IProgress<FileProgress> progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(destinationPath))
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

        var failureMessage = $"Failed to copy file from '{path}' to '{destinationPath}'";

        try
        {
            return await this.ExecuteMutationAsync(
                [path, destinationPath],
                async (mutationContext, cancellationToken) =>
                {
                    var sourcePath = mutationContext.GetPath(0);
                    var destinationMutationPath = mutationContext.GetPath(1);
                    if (sourcePath.NormalizedPath.Length == 0 || destinationMutationPath.NormalizedPath.Length == 0)
                    {
                        return Result.Failure()
                            .WithError(new FileSystemError("Path cannot resolve to the storage root", sourcePath.NormalizedPath.Length == 0 ? path : destinationPath))
                            .WithMessage("Invalid path provided");
                    }

                    var distinctPathResult = this.ValidateDistinctPaths(sourcePath, destinationMutationPath, failureMessage);
                    if (distinctPathResult.IsFailure)
                    {
                        return distinctPathResult;
                    }

                    var sourceFile = await this.TryGetTrackedFileAsync(mutationContext.Context, sourcePath.NormalizedPath, includeContent: true, cancellationToken);
                    if (sourceFile is null)
                    {
                        var directoryConflict = await this.TryGetTrackedDirectoryAsync(mutationContext.Context, sourcePath.NormalizedPath, cancellationToken);
                        return directoryConflict is not null
                            ? this.CreatePathConflictFailure(path, failureMessage, "directory", sourcePath.NormalizedPath)
                            : Result.Failure()
                                .WithError(new FileSystemError("Source file not found", path))
                                .WithMessage(failureMessage);
                    }

                    if (sourceFile.Content is null)
                    {
                        return Result.Failure()
                            .WithError(new FileSystemError("File content not found", path))
                            .WithMessage(failureMessage);
                    }

                    var destinationDirectoryConflict = await this.TryGetTrackedDirectoryAsync(mutationContext.Context, destinationMutationPath.NormalizedPath, cancellationToken);
                    if (destinationDirectoryConflict is not null)
                    {
                        return this.CreatePathConflictFailure(destinationPath, failureMessage, "directory", destinationMutationPath.NormalizedPath);
                    }

                    var destinationParentResult = await this.EnsureDirectoryChainAsync(
                        mutationContext.Context,
                        destinationMutationPath.ParentPath ?? string.Empty,
                        mutationContext.Timestamp,
                        explicitTarget: false,
                        destinationPath,
                        cancellationToken);
                    if (destinationParentResult.IsFailure)
                    {
                        return Result.Failure()
                            .WithErrors(destinationParentResult.Errors)
                            .WithMessages(destinationParentResult.Messages);
                    }

                    var existingDestinationFile = await this.TryGetTrackedFileAsync(
                        mutationContext.Context,
                        destinationMutationPath.NormalizedPath,
                        includeContent: true,
                        cancellationToken);
                    if (existingDestinationFile is not null)
                    {
                        if (existingDestinationFile.Content is not null)
                        {
                            mutationContext.Context.StorageFileContents.Remove(existingDestinationFile.Content);
                        }

                        mutationContext.Context.StorageFiles.Remove(existingDestinationFile);
                        await mutationContext.Context.SaveChangesAsync(cancellationToken);
                    }

                    var copiedFileId = Guid.NewGuid();
                    var copiedContent = this.CloneFileContentEntity(sourceFile.Content);
                    copiedContent.FileId = copiedFileId;

                    mutationContext.Context.StorageFiles.Add(new FileStorageFileEntity
                    {
                        Id = copiedFileId,
                        LocationName = this.LocationName,
                        NormalizedPath = destinationMutationPath.NormalizedPath,
                        NormalizedPathHash = destinationMutationPath.PathHash,
                        ParentPath = destinationMutationPath.ParentPath,
                        ParentPathHash = destinationMutationPath.ParentPathHash,
                        Name = destinationMutationPath.Name,
                        ContentType = this.ResolveContentType(destinationMutationPath.NormalizedPath),
                        Length = sourceFile.Length,
                        Checksum = sourceFile.Checksum,
                        LastModified = sourceFile.LastModified,
                        ConcurrencyVersion = Guid.NewGuid(),
                        Content = copiedContent
                    });

                    await this.TouchDirectoriesAsync(
                        mutationContext.Context,
                        [destinationMutationPath.ParentPath],
                        mutationContext.Timestamp,
                        cancellationToken);
                    await mutationContext.Context.SaveChangesAsync(cancellationToken);

                    progress?.Report(new FileProgress
                    {
                        BytesProcessed = sourceFile.Length,
                        FilesProcessed = 1,
                        TotalFiles = 1
                    });

                    return Result.Success()
                        .WithMessage($"Copied file from '{path}' to '{destinationPath}'");
                },
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during file copy"))
                .WithMessage($"Cancelled copying file from '{path}' to '{destinationPath}'");
        }
        catch (DbUpdateException ex)
        {
            return await this.TranslateMutationDbUpdateExceptionAsync([path, destinationPath], ex, failureMessage, cancellationToken);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Entity Framework file copy failed for {LocationName}, {Path}, and {DestinationPath}", this.LocationName, path, destinationPath);

            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error copying file from '{path}' to '{destinationPath}'");
        }
    }

    /// <inheritdoc />
    public override async Task<Result> RenameFileAsync(
        string path,
        string destinationPath,
        IProgress<FileProgress> progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(destinationPath))
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

        try
        {
            return await this.MoveFileInternalAsync(
                path,
                destinationPath,
                progress,
                $"Renamed file from '{path}' to '{destinationPath}'",
                $"Failed to rename file from '{path}' to '{destinationPath}'",
                $"Unexpected error renaming file from '{path}' to '{destinationPath}'",
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during file rename"))
                .WithMessage($"Cancelled renaming file from '{path}' to '{destinationPath}'");
        }
    }

    /// <inheritdoc />
    public override async Task<Result> MoveFileAsync(
        string path,
        string destinationPath,
        IProgress<FileProgress> progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(destinationPath))
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

        try
        {
            return await this.MoveFileInternalAsync(
                path,
                destinationPath,
                progress,
                $"Moved file from '{path}' to '{destinationPath}'",
                $"Failed to move file from '{path}' to '{destinationPath}'",
                $"Unexpected error moving file from '{path}' to '{destinationPath}'",
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during file move"))
                .WithMessage($"Cancelled moving file from '{path}' to '{destinationPath}'");
        }
    }

    /// <inheritdoc />
    public override async Task<Result> RenameDirectoryAsync(string path, string destinationPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(destinationPath))
        {
            return Result.Failure()
                .WithError(new FileSystemError("Old or new path cannot be null or empty", $"{path ?? destinationPath}"))
                .WithMessage("Invalid paths provided");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled renaming directory from '{path}' to '{destinationPath}'");
        }

        var failureMessage = $"Failed to rename directory from '{path}' to '{destinationPath}'";

        try
        {
            return await this.ExecuteMutationAsync(
                [path, destinationPath],
                async (mutationContext, cancellationToken) =>
                {
                    var sourcePath = mutationContext.GetPath(0);
                    var destinationMutationPath = mutationContext.GetPath(1);
                    if (sourcePath.NormalizedPath.Length == 0 || destinationMutationPath.NormalizedPath.Length == 0)
                    {
                        return Result.Failure()
                            .WithError(new FileSystemError("Path cannot resolve to the storage root", sourcePath.NormalizedPath.Length == 0 ? path : destinationPath))
                            .WithMessage("Invalid path provided");
                    }

                    var pathValidationResult = this.ValidateDirectoryRenamePaths(sourcePath, destinationMutationPath, failureMessage);
                    if (pathValidationResult.IsFailure)
                    {
                        return pathValidationResult;
                    }

                    var sourceDirectory = await this.TryGetTrackedDirectoryAsync(mutationContext.Context, sourcePath.NormalizedPath, cancellationToken);
                    if (sourceDirectory is null)
                    {
                        var fileConflict = await this.TryGetTrackedFileAsync(mutationContext.Context, sourcePath.NormalizedPath, includeContent: false, cancellationToken);
                        return fileConflict is not null
                            ? this.CreatePathConflictFailure(path, failureMessage, "file", sourcePath.NormalizedPath)
                            : Result.Failure()
                                .WithError(new FileSystemError("Directory not found", path))
                                .WithMessage(failureMessage);
                    }

                    var destinationDirectoryConflict = await this.TryGetTrackedDirectoryAsync(mutationContext.Context, destinationMutationPath.NormalizedPath, cancellationToken);
                    if (destinationDirectoryConflict is not null)
                    {
                        return this.CreatePathConflictFailure(destinationPath, failureMessage, "directory", destinationMutationPath.NormalizedPath);
                    }

                    var destinationFileConflict = await this.TryGetTrackedFileAsync(mutationContext.Context, destinationMutationPath.NormalizedPath, includeContent: false, cancellationToken);
                    if (destinationFileConflict is not null)
                    {
                        return this.CreatePathConflictFailure(destinationPath, failureMessage, "file", destinationMutationPath.NormalizedPath);
                    }

                    var destinationParentResult = await this.EnsureDirectoryChainAsync(
                        mutationContext.Context,
                        destinationMutationPath.ParentPath ?? string.Empty,
                        mutationContext.Timestamp,
                        explicitTarget: false,
                        destinationPath,
                        cancellationToken);
                    if (destinationParentResult.IsFailure)
                    {
                        return Result.Failure()
                            .WithErrors(destinationParentResult.Errors)
                            .WithMessages(destinationParentResult.Messages);
                    }

                    var subtreePrefix = sourcePath.NormalizedPath + "/";
                    var directories = await mutationContext.Context.StorageDirectories
                        .Where(d => d.LocationName == this.LocationName)
                        .Where(d => d.NormalizedPath == sourcePath.NormalizedPath || d.NormalizedPath.StartsWith(subtreePrefix))
                        .OrderBy(d => d.NormalizedPath)
                        .ToListAsync(cancellationToken);
                    var files = await mutationContext.Context.StorageFiles
                        .Where(f => f.LocationName == this.LocationName)
                        .Where(f => f.NormalizedPath.StartsWith(subtreePrefix))
                        .OrderBy(f => f.NormalizedPath)
                        .ToListAsync(cancellationToken);

                    var sourceParentPath = sourcePath.ParentPath;

                    foreach (var directory in directories)
                    {
                        var updatedPath = RewriteSubtreePath(sourcePath.NormalizedPath, destinationMutationPath.NormalizedPath, directory.NormalizedPath);
                        this.UpdateDirectoryLocation(
                            directory,
                            updatedPath,
                            updateLastModified: directory.Id == sourceDirectory.Id,
                            mutationContext.Timestamp);
                    }

                    foreach (var file in files)
                    {
                        var updatedPath = RewriteSubtreePath(sourcePath.NormalizedPath, destinationMutationPath.NormalizedPath, file.NormalizedPath);
                        this.UpdateFileLocation(file, updatedPath, updateLastModified: false, mutationContext.Timestamp);
                    }

                    await this.TouchDirectoriesAsync(
                        mutationContext.Context,
                        [sourceParentPath, destinationMutationPath.ParentPath],
                        mutationContext.Timestamp,
                        cancellationToken);
                    await mutationContext.Context.SaveChangesAsync(cancellationToken);

                    await this.PruneImplicitDirectoriesAsync(mutationContext.Context, sourceParentPath, mutationContext.Timestamp, cancellationToken);
                    await mutationContext.Context.SaveChangesAsync(cancellationToken);

                    return Result.Success()
                        .WithMessage($"Renamed directory from '{path}' to '{destinationPath}'");
                },
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during directory rename"))
                .WithMessage($"Cancelled renaming directory from '{path}' to '{destinationPath}'");
        }
        catch (DbUpdateException ex)
        {
            return await this.TranslateMutationDbUpdateExceptionAsync([path, destinationPath], ex, failureMessage, cancellationToken);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Entity Framework directory rename failed for {LocationName}, {Path}, and {DestinationPath}", this.LocationName, path, destinationPath);

            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error renaming directory from '{path}' to '{destinationPath}'");
        }
    }

    /// <inheritdoc />
    public override async Task<Result> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        if (path is null)
        {
            return Result.Failure()
                .WithError(new FileSystemError("Path cannot be null", path))
                .WithMessage("Invalid path provided");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled checking if '{path}' is a directory");
        }

        try
        {
            using var scope = this.CreateScope();
            var context = this.ResolveContext(scope);
            var readyResult = await this.EnsureStorageReadyAsync(context, cancellationToken);
            if (readyResult.IsFailure)
            {
                return Result.Failure()
                    .WithErrors(readyResult.Errors)
                    .WithMessages(readyResult.Messages);
            }

            var normalizedPath = this.NormalizePath(path);
            if (normalizedPath.Length == 0)
            {
                return Result.Success()
                    .WithMessage($"Checked if '{path}' is a directory");
            }

            var directory = await this.TryGetDirectoryAsync(context, normalizedPath, cancellationToken);
            if (directory is null)
            {
                return Result.Failure()
                    .WithError(new NotFoundError("Directory not found"))
                    .WithMessage($"Failed to check if '{path}' is a directory");
            }

            return Result.Success()
                .WithMessage($"Checked if '{path}' is a directory");
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during directory existence check"))
                .WithMessage($"Cancelled checking if '{path}' is a directory");
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Entity Framework directory existence check failed for {LocationName} and {Path}", this.LocationName, path);

            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error checking if '{path}' is a directory");
        }
    }

    /// <inheritdoc />
    public override async Task<Result> CreateDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
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

        try
        {
            return await this.ExecuteMutationAsync(
                [path],
                async (mutationContext, cancellationToken) =>
                {
                    var mutationPath = mutationContext.PrimaryPath;
                    if (mutationPath.NormalizedPath.Length == 0)
                    {
                        return Result.Failure()
                            .WithError(new FileSystemError("Path cannot resolve to the storage root", path))
                            .WithMessage("Invalid path provided");
                    }

                    var fileConflict = await this.TryGetTrackedFileAsync(mutationContext.Context, mutationPath.NormalizedPath, includeContent: false, cancellationToken);
                    if (fileConflict is not null)
                    {
                        return this.CreatePathConflictFailure(path, $"Failed to create directory at '{path}'", "file", mutationPath.NormalizedPath);
                    }

                    var existingDirectory = await this.TryGetTrackedDirectoryAsync(mutationContext.Context, mutationPath.NormalizedPath, cancellationToken);
                    var directoryResult = await this.EnsureDirectoryChainAsync(
                        mutationContext.Context,
                        mutationPath.NormalizedPath,
                        mutationContext.Timestamp,
                        explicitTarget: true,
                        path,
                        cancellationToken);
                    if (directoryResult.IsFailure)
                    {
                        return Result.Failure()
                            .WithErrors(directoryResult.Errors)
                            .WithMessages(directoryResult.Messages);
                    }

                    if (existingDirectory is null || !existingDirectory.IsExplicit)
                    {
                        await this.TouchParentDirectoryAsync(mutationContext.Context, mutationPath.ParentPath, mutationContext.Timestamp, cancellationToken);
                    }

                    await mutationContext.Context.SaveChangesAsync(cancellationToken);

                    return Result.Success()
                        .WithMessage($"Created directory at '{path}'");
                },
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during directory creation"))
                .WithMessage($"Cancelled creating directory at '{path}'");
        }
        catch (DbUpdateException ex)
        {
            return await this.TranslateMutationDbUpdateExceptionAsync(path, ex, $"Failed to create directory at '{path}'", cancellationToken);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Entity Framework directory create failed for {LocationName} and {Path}", this.LocationName, path);

            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error creating directory at '{path}'");
        }
    }

    /// <inheritdoc />
    public override async Task<Result> DeleteDirectoryAsync(string path, bool recursive, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
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

        try
        {
            return await this.ExecuteMutationAsync(
                [path],
                async (mutationContext, cancellationToken) =>
                {
                    var mutationPath = mutationContext.PrimaryPath;
                    if (mutationPath.NormalizedPath.Length == 0)
                    {
                        return Result.Failure()
                            .WithError(new FileSystemError("Path cannot resolve to the storage root", path))
                            .WithMessage("Invalid path provided");
                    }

                    var directory = await this.TryGetTrackedDirectoryAsync(mutationContext.Context, mutationPath.NormalizedPath, cancellationToken);
                    if (directory is null)
                    {
                        var fileConflict = await this.TryGetTrackedFileAsync(mutationContext.Context, mutationPath.NormalizedPath, includeContent: false, cancellationToken);
                        return fileConflict is not null
                            ? this.CreatePathConflictFailure(path, $"Failed to delete directory at '{path}'", "file", mutationPath.NormalizedPath)
                            : Result.Failure()
                                .WithError(new FileSystemError("Directory not found", path))
                                .WithMessage($"Failed to delete directory at '{path}'");
                    }

                    var hasChildren = await this.HasDirectChildrenAsync(mutationContext.Context, mutationPath.NormalizedPath, cancellationToken);
                    if (hasChildren && !recursive)
                    {
                        return Result.Failure()
                            .WithError(new ConflictError("Directory not empty"))
                            .WithMessage($"Failed to delete directory at '{path}'");
                    }

                    if (recursive)
                    {
                        var subtreePrefix = mutationPath.NormalizedPath + "/";
                        var files = await mutationContext.Context.StorageFiles
                            .Where(f => f.LocationName == this.LocationName)
                            .Where(f => f.NormalizedPath == mutationPath.NormalizedPath || f.NormalizedPath.StartsWith(subtreePrefix))
                            .OrderBy(f => f.NormalizedPath)
                            .Include(f => f.Content)
                            .ToListAsync(cancellationToken);

                        foreach (var file in files)
                        {
                            if (file.Content is not null)
                            {
                                mutationContext.Context.StorageFileContents.Remove(file.Content);
                            }
                        }

                        mutationContext.Context.StorageFiles.RemoveRange(files);

                        var directories = await mutationContext.Context.StorageDirectories
                            .Where(d => d.LocationName == this.LocationName)
                            .Where(d => d.NormalizedPath == mutationPath.NormalizedPath || d.NormalizedPath.StartsWith(subtreePrefix))
                            .OrderByDescending(d => d.NormalizedPath.Length)
                            .ThenBy(d => d.NormalizedPath)
                            .ToListAsync(cancellationToken);

                        mutationContext.Context.StorageDirectories.RemoveRange(directories);
                    }
                    else
                    {
                        mutationContext.Context.StorageDirectories.Remove(directory);
                    }

                    await this.TouchParentDirectoryAsync(mutationContext.Context, directory.ParentPath, mutationContext.Timestamp, cancellationToken);
                    await mutationContext.Context.SaveChangesAsync(cancellationToken);

                    await this.PruneImplicitDirectoriesAsync(mutationContext.Context, directory.ParentPath, mutationContext.Timestamp, cancellationToken);
                    await mutationContext.Context.SaveChangesAsync(cancellationToken);

                    return Result.Success()
                        .WithMessage($"Deleted directory at '{path}'");
                },
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during directory deletion"))
                .WithMessage($"Cancelled deleting directory at '{path}'");
        }
        catch (DbUpdateException ex)
        {
            return await this.TranslateMutationDbUpdateExceptionAsync(path, ex, $"Failed to delete directory at '{path}'", cancellationToken);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Entity Framework directory delete failed for {LocationName} and {Path}", this.LocationName, path);

            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error deleting directory at '{path}'");
        }
    }

    /// <inheritdoc />
    public override async Task<Result<(IEnumerable<string> Files, string NextContinuationToken)>> ListFilesAsync(
        string path,
        string searchPattern = null,
        bool recursive = false,
        string continuationToken = null,
        CancellationToken cancellationToken = default)
    {
        if (path is null)
        {
            return Result<(IEnumerable<string> Files, string NextContinuationToken)>.Failure()
                .WithError(new FileSystemError("Path cannot be null", path))
                .WithMessage("Invalid path provided");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result<(IEnumerable<string> Files, string NextContinuationToken)>.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled listing files in '{path}'");
        }

        try
        {
            using var scope = this.CreateScope();
            var context = this.ResolveContext(scope);
            var readyResult = await this.EnsureStorageReadyAsync(context, cancellationToken);
            if (readyResult.IsFailure)
            {
                return Result<(IEnumerable<string> Files, string NextContinuationToken)>.Failure()
                    .WithErrors(readyResult.Errors)
                    .WithMessages(readyResult.Messages);
            }

            var normalizedRootPath = this.NormalizePath(path);
            var directoryResult = await this.EnsureDirectoryRootExistsAsync(context, normalizedRootPath, path, cancellationToken);
            if (directoryResult.IsFailure)
            {
                return Result<(IEnumerable<string> Files, string NextContinuationToken)>.Failure()
                    .WithErrors(directoryResult.Errors)
                    .WithMessages(directoryResult.Messages);
            }

            var tokenResult = this.ParseSeekContinuationToken(continuationToken, normalizedRootPath, recursive, searchPattern);
            if (tokenResult.IsFailure)
            {
                return Result<(IEnumerable<string> Files, string NextContinuationToken)>.Failure()
                    .WithErrors(tokenResult.Errors)
                    .WithMessages(tokenResult.Messages);
            }

            var pageSize = Math.Max(1, this.options.PageSize);
            var batchSize = Math.Max(pageSize * 4, 32);
            var files = new List<string>(pageSize);
            var seekPath = tokenResult.Value;
            var hasNextPage = false;

            while (!hasNextPage)
            {
                var candidates = await this.BuildFileListQuery(context, normalizedRootPath, recursive, seekPath)
                    .OrderBy(f => f.NormalizedPath)
                    .Select(f => new FileListRow(f.NormalizedPath, f.Name))
                    .Take(batchSize)
                    .ToListAsync(cancellationToken);

                if (candidates.Count == 0)
                {
                    break;
                }

                foreach (var candidate in candidates)
                {
                    seekPath = candidate.NormalizedPath;

                    if (!string.IsNullOrEmpty(searchPattern) && !candidate.Name.Match(searchPattern))
                    {
                        continue;
                    }

                    if (files.Count < pageSize)
                    {
                        files.Add(candidate.NormalizedPath);
                    }
                    else
                    {
                        hasNextPage = true;
                        break;
                    }
                }

                if (candidates.Count < batchSize)
                {
                    break;
                }
            }

            var nextToken = hasNextPage && files.Count > 0
                ? this.EncodeSeekContinuationToken(normalizedRootPath, recursive, searchPattern, files[^1])
                : null;

            return Result<(IEnumerable<string> Files, string NextContinuationToken)>.Success((files, nextToken))
                .WithMessage($"Listed files in '{path}' with pattern '{searchPattern}'");
        }
        catch (OperationCanceledException)
        {
            return Result<(IEnumerable<string> Files, string NextContinuationToken)>.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during file listing"))
                .WithMessage($"Cancelled listing files in '{path}'");
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Entity Framework file listing failed for {LocationName} and {Path}", this.LocationName, path);

            return Result<(IEnumerable<string> Files, string NextContinuationToken)>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error listing files in '{path}'");
        }
    }

    /// <inheritdoc />
    public override async Task<Result<IEnumerable<string>>> ListDirectoriesAsync(
        string path,
        string searchPattern = null,
        bool recursive = false,
        CancellationToken cancellationToken = default)
    {
        if (path is null)
        {
            return Result<IEnumerable<string>>.Failure()
                .WithError(new FileSystemError("Path cannot be null", path))
                .WithMessage("Invalid path provided");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Result<IEnumerable<string>>.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled listing directories at '{path}'");
        }

        try
        {
            using var scope = this.CreateScope();
            var context = this.ResolveContext(scope);
            var readyResult = await this.EnsureStorageReadyAsync(context, cancellationToken);
            if (readyResult.IsFailure)
            {
                return Result<IEnumerable<string>>.Failure()
                    .WithErrors(readyResult.Errors)
                    .WithMessages(readyResult.Messages);
            }

            var normalizedRootPath = this.NormalizePath(path);
            var directoryResult = await this.EnsureDirectoryRootExistsAsync(context, normalizedRootPath, path, cancellationToken);
            if (directoryResult.IsFailure)
            {
                return Result<IEnumerable<string>>.Failure()
                    .WithErrors(directoryResult.Errors)
                    .WithMessages(directoryResult.Messages);
            }

            var directories = await this.BuildDirectoryListQuery(context, normalizedRootPath, recursive)
                .OrderBy(d => d.NormalizedPath)
                .Select(d => new DirectoryListRow(d.NormalizedPath, d.Name))
                .ToListAsync(cancellationToken);

            var filtered = directories
                .Where(d => string.IsNullOrEmpty(searchPattern) || d.Name.Match(searchPattern))
                .Select(d => d.NormalizedPath)
                .ToList();

            return Result<IEnumerable<string>>.Success(filtered)
                .WithMessage($"Listed directories at '{path}'");
        }
        catch (OperationCanceledException)
        {
            return Result<IEnumerable<string>>.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during directory listing"))
                .WithMessage($"Cancelled listing directories at '{path}'");
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Entity Framework directory listing failed for {LocationName} and {Path}", this.LocationName, path);

            return Result<IEnumerable<string>>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error listing directories at '{path}'");
        }
    }

    /// <inheritdoc />
    public override async Task<Result> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled"))
                .WithMessage($"Cancelled health check for Entity Framework storage at '{this.LocationName}'");
        }

        try
        {
            using var scope = this.CreateScope();
            var context = this.ResolveContext(scope);
            var validationResult = this.ValidateStorageModel(context);
            if (validationResult.IsFailure)
            {
                return validationResult;
            }

            if (!await context.Database.CanConnectAsync(cancellationToken))
            {
                return Result.Failure()
                    .WithError(new FileSystemError("Database connection is unavailable", this.LocationName))
                    .WithMessage($"Failed to check health of Entity Framework storage at '{this.LocationName}'");
            }

            _ = await context.StorageDirectories
                .AsNoTracking()
                .Where(d => d.LocationName == this.LocationName)
                .OrderBy(d => d.NormalizedPath)
                .Select(d => d.Id)
                .FirstOrDefaultAsync(cancellationToken);
            _ = await context.StorageFiles
                .AsNoTracking()
                .Where(f => f.LocationName == this.LocationName)
                .OrderBy(f => f.NormalizedPath)
                .Select(f => f.Id)
                .FirstOrDefaultAsync(cancellationToken);
            _ = await context.StorageFileContents
                .AsNoTracking()
                .OrderBy(c => c.FileId)
                .Select(c => c.FileId)
                .FirstOrDefaultAsync(cancellationToken);

            this.logger.LogDebug(
                "Validated Entity Framework file storage provider for location {LocationName} using {ContextType}",
                this.LocationName,
                typeof(TContext).FullName);

            return Result.Success()
                .WithMessage($"Entity Framework storage at '{this.LocationName}' is healthy");
        }
        catch (OperationCanceledException)
        {
            return Result.Failure()
                .WithError(new OperationCancelledError("Operation cancelled during health check"))
                .WithMessage($"Cancelled health check for Entity Framework storage at '{this.LocationName}'");
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Entity Framework file storage health check failed for {LocationName}", this.LocationName);

            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Unexpected error checking health of Entity Framework storage at '{this.LocationName}'");
        }
    }

    /// <summary>
    /// Creates a new service scope for a file storage operation.
    /// </summary>
    /// <returns>A new service scope.</returns>
    protected virtual IServiceScope CreateScope() => this.serviceProvider.CreateScope();

    /// <summary>
    /// Resolves a fresh <typeparamref name="TContext" /> instance from the supplied scope.
    /// </summary>
    /// <param name="scope">The service scope that owns the database context.</param>
    /// <returns>The resolved database context.</returns>
    protected virtual TContext ResolveContext(IServiceScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return scope.ServiceProvider.GetRequiredService<TContext>();
    }

    /// <summary>
    /// Validates that the resolved <typeparamref name="TContext" /> model contains the Entity Framework
    /// file-storage entities required by this provider.
    /// </summary>
    /// <param name="context">The database context to validate.</param>
    /// <returns>A success result when the model is compatible; otherwise a failure result describing the missing mappings.</returns>
    protected virtual Result ValidateStorageModel(TContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var missingEntityNames = new List<string>();
        if (context.Model.FindEntityType(typeof(FileStorageFileEntity)) is null)
        {
            missingEntityNames.Add(nameof(FileStorageFileEntity));
        }

        if (context.Model.FindEntityType(typeof(FileStorageFileContentEntity)) is null)
        {
            missingEntityNames.Add(nameof(FileStorageFileContentEntity));
        }

        if (context.Model.FindEntityType(typeof(FileStorageDirectoryEntity)) is null)
        {
            missingEntityNames.Add(nameof(FileStorageDirectoryEntity));
        }

        return missingEntityNames.Count == 0
            ? Result.Success()
            : Result.Failure()
                .WithError(new ExceptionError(
                    new InvalidOperationException(
                        $"The DbContext '{typeof(TContext).FullName}' is missing the following file-storage entity mappings: {string.Join(", ", missingEntityNames)}.")))
                .WithMessage($"Entity Framework storage at '{this.LocationName}' is not fully mapped");
    }

    /// <summary>
    /// Gets the logical lease owner identifier used for future row-lease coordination.
    /// </summary>
    /// <returns>The lease owner identifier.</returns>
    protected virtual string GetLeaseOwner() => this.leaseOwner;

    /// <summary>
    /// Normalizes a caller-supplied storage path into a provider-owned logical path.
    /// </summary>
    /// <param name="path">The caller-supplied path.</param>
    /// <returns>The normalized logical path.</returns>
    protected virtual string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var segments = path
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var normalizedSegments = new List<string>(segments.Length);

        foreach (var segment in segments)
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (normalizedSegments.Count > 0)
                {
                    normalizedSegments.RemoveAt(normalizedSegments.Count - 1);
                }

                continue;
            }

            normalizedSegments.Add(segment.ToLowerInvariant());
        }

        return string.Join("/", normalizedSegments);
    }

    /// <summary>
    /// Gets the normalized parent path for the supplied normalized path.
    /// </summary>
    /// <param name="normalizedPath">The normalized path.</param>
    /// <returns>The parent path, an empty string for a direct child of root, or <see langword="null" /> for the root row.</returns>
    protected virtual string GetParentPath(string normalizedPath)
    {
        var value = normalizedPath ?? string.Empty;
        if (value.Length == 0)
        {
            return null;
        }

        var separatorIndex = value.LastIndexOf('/');
        return separatorIndex < 0 ? string.Empty : value[..separatorIndex];
    }

    /// <summary>
    /// Gets the final path segment for the supplied normalized path.
    /// </summary>
    /// <param name="normalizedPath">The normalized path.</param>
    /// <returns>The final path segment or an empty string for the root row.</returns>
    protected virtual string GetEntryName(string normalizedPath)
    {
        var value = normalizedPath ?? string.Empty;
        if (value.Length == 0)
        {
            return string.Empty;
        }

        var separatorIndex = value.LastIndexOf('/');
        return separatorIndex < 0 ? value : value[(separatorIndex + 1)..];
    }

    /// <summary>
    /// Resolves the content type for the supplied normalized file path.
    /// </summary>
    /// <param name="normalizedPath">The normalized file path.</param>
    /// <returns>The detected content type.</returns>
    protected virtual ContentType ResolveContentType(string normalizedPath) =>
        ContentTypeExtensions.FromFileName(normalizedPath, ContentType.DEFAULT);

    /// <summary>
    /// Computes the provider-owned path hash used for exact-path lookups.
    /// </summary>
    /// <param name="normalizedPath">The normalized path to hash.</param>
    /// <returns>The lowercase hexadecimal path hash.</returns>
    protected virtual string ComputePathHash(string normalizedPath) => HashHelper.ComputeSha256(normalizedPath ?? string.Empty).ToLower();

    /// <summary>
    /// Ensures that the logical root directory row exists for the current location.
    /// </summary>
    /// <param name="context">The active database context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resolved or created root directory row.</returns>
    protected virtual Task<FileStorageDirectoryEntity> EnsureRootDirectoryAsync(
        TContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        return this.EnsureRootDirectoryInternalAsync(context, cancellationToken);
    }

    /// <summary>
    /// Buffers a content stream in memory while enforcing the configured maximum buffered content size.
    /// </summary>
    /// <param name="content">The content stream to buffer.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A seekable memory stream positioned at the beginning.</returns>
    protected virtual async Task<MemoryStream> BufferContentAsync(
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var originalPosition = content.CanSeek ? content.Position : 0;
        var buffered = new MemoryStream();
        var buffer = new byte[81920];
        var totalBytes = 0L;

        try
        {
            int read;
            while ((read = await content.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
            {
                totalBytes += read;
                if (this.options.MaximumBufferedContentSize is long limit && totalBytes > limit)
                {
                    throw new BufferedContentLimitExceededException(limit, totalBytes);
                }

                await buffered.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            buffered.Position = 0;
            return buffered;
        }
        catch
        {
            await buffered.DisposeAsync();
            throw;
        }
        finally
        {
            if (content.CanSeek)
            {
                content.Position = originalPosition;
            }
        }
    }

    /// <summary>
    /// Creates the future persisted content representation for a buffered file payload.
    /// </summary>
    /// <param name="normalizedPath">The normalized file path.</param>
    /// <param name="content">The content stream to serialize.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The content entity that should be persisted for the file.</returns>
    protected virtual Task<FileStorageFileContentEntity> SerializeContentAsync(
        string normalizedPath,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        return this.SerializeContentInternalAsync(normalizedPath, content, cancellationToken);
    }

    /// <summary>
    /// Creates a readable stream from a persisted content entity.
    /// </summary>
    /// <param name="contentEntity">The persisted content entity.</param>
    /// <returns>A readable stream for the stored payload.</returns>
    protected virtual Stream DeserializeContent(FileStorageFileContentEntity contentEntity)
    {
        ArgumentNullException.ThrowIfNull(contentEntity);

        var hasTextContent = contentEntity.ContentText is not null;
        var hasBinaryContent = contentEntity.ContentBinary is not null;
        if (hasTextContent == hasBinaryContent)
        {
            throw new InvalidOperationException("Stored file content must contain either text or binary payload, but not both.");
        }

        return hasBinaryContent
            ? new MemoryStream(contentEntity.ContentBinary, writable: false)
            : new MemoryStream(EncodeTextContent(contentEntity.ContentText, contentEntity.TextEncodingName, contentEntity.TextHasByteOrderMark), writable: false);
    }

    private async Task<Result> ExecuteMutationAsync(
        IEnumerable<string> paths,
        Func<MutationExecutionContext, CancellationToken, Task<Result>> action,
        CancellationToken cancellationToken)
    {
        var orderedPaths = this.CreateOrderedMutationPaths(paths);
        var totalAttempts = Math.Max(1, this.options.RetryCount);

        for (var attempt = 1; attempt <= totalAttempts; attempt++)
        {
            using var scope = this.CreateScope();
            var context = this.ResolveContext(scope);
            var validationResult = this.ValidateStorageModel(context);
            if (validationResult.IsFailure)
            {
                return Result.Failure()
                    .WithErrors(validationResult.Errors)
                    .WithMessages(validationResult.Messages);
            }

            await using var transaction = await this.BeginMutationTransactionAsync(context, cancellationToken);

            try
            {
                var timestamp = DateTimeOffset.UtcNow;
                var rootDirectory = await this.EnsureRootDirectoryAsync(context, cancellationToken);
                MutationLeaseHandle leaseHandle = MutationLeaseHandle.Empty;

                if (transaction is not null)
                {
                    leaseHandle = await this.AcquireMutationLeasesAsync(context, orderedPaths, timestamp, cancellationToken);
                    await this.ReloadTrackedLeaseTargetsAsync(context, leaseHandle, cancellationToken);
                }

                var mutationContext = new MutationExecutionContext(
                    context,
                    rootDirectory,
                    orderedPaths,
                    timestamp,
                    this.GetLeaseOwner());

                var result = await action(mutationContext, cancellationToken);
                if (result.IsSuccess && transaction is not null)
                {
                    await this.ReleaseMutationLeasesAsync(context, leaseHandle, cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }

                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (this.ShouldRetryMutationException(context, ex, transaction is not null) && attempt < totalAttempts)
            {
                var delay = this.CalculateRetryDelay(attempt);
                this.logger.LogWarning(
                    ex,
                    "Entity Framework file storage mutation retry {Attempt}/{TotalAttempts} for {LocationName} after transient contention",
                    attempt,
                    totalAttempts,
                    this.LocationName);

                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken);
                }
            }
            catch (Exception ex) when (this.ShouldRetryMutationException(context, ex, transaction is not null))
            {
                throw this.WrapRetryableMutationException(ex);
            }
        }

        throw new InvalidOperationException("Mutation execution exited without producing a result.");
    }

    private Task<Result<FileStorageDirectoryEntity>> EnsureStorageReadyAsync(TContext context, CancellationToken cancellationToken)
    {
        var validationResult = this.ValidateStorageModel(context);
        if (validationResult.IsFailure)
        {
            return Task.FromResult(Result<FileStorageDirectoryEntity>.Failure()
                .WithErrors(validationResult.Errors)
                .WithMessages(validationResult.Messages));
        }

        return Task.FromResult(Result<FileStorageDirectoryEntity>.Success());
    }

    private async Task<Result> EnsureDirectoryRootExistsAsync(
        TContext context,
        string normalizedRootPath,
        string originalPath,
        CancellationToken cancellationToken)
    {
        if (normalizedRootPath.Length == 0)
        {
            return Result.Success();
        }

        var directory = await this.TryGetDirectoryAsync(context, normalizedRootPath, cancellationToken);
        if (directory is not null)
        {
            return Result.Success();
        }

        return Result.Failure()
            .WithError(new FileSystemError("Directory not found", originalPath))
            .WithMessage($"Failed to list entries in '{originalPath}'");
    }

    private async Task<Result<FileStorageDirectoryEntity>> EnsureDirectoryChainAsync(
        TContext context,
        string normalizedDirectoryPath,
        DateTimeOffset now,
        bool explicitTarget,
        string originalPath,
        CancellationToken cancellationToken)
    {
        var current = await this.EnsureRootDirectoryAsync(context, cancellationToken);
        if (normalizedDirectoryPath.Length == 0)
        {
            return Result<FileStorageDirectoryEntity>.Success(current);
        }

        var segments = normalizedDirectoryPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var currentPath = string.Empty;

        for (var index = 0; index < segments.Length; index++)
        {
            currentPath = currentPath.Length == 0 ? segments[index] : $"{currentPath}/{segments[index]}";

            var fileConflict = await this.TryGetTrackedFileAsync(context, currentPath, includeContent: false, cancellationToken);
            if (fileConflict is not null)
            {
                    return Result<FileStorageDirectoryEntity>.Failure()
                        .WithError(new ConflictError("Path conflicts with an existing file"))
                        .WithMessage($"Failed to materialize parent directories for '{originalPath}'");
            }

            var directory = await this.TryGetTrackedDirectoryAsync(context, currentPath, cancellationToken);
            if (directory is null)
            {
                var parentPath = this.GetParentPath(currentPath);
                if (current is not null)
                {
                    this.TouchDirectory(current, now);
                }

                directory = new FileStorageDirectoryEntity
                {
                    Id = Guid.NewGuid(),
                    LocationName = this.LocationName,
                    NormalizedPath = currentPath,
                    NormalizedPathHash = this.ComputePathHash(currentPath),
                    ParentPath = parentPath,
                    ParentPathHash = parentPath is null ? null : this.ComputePathHash(parentPath),
                    Name = this.GetEntryName(currentPath),
                    IsExplicit = explicitTarget && index == segments.Length - 1,
                    LastModified = now,
                    ConcurrencyVersion = Guid.NewGuid()
                };

                context.StorageDirectories.Add(directory);
            }
            else if (explicitTarget && index == segments.Length - 1 && !directory.IsExplicit)
            {
                directory.IsExplicit = true;
                this.TouchDirectory(directory, now);
            }

            current = directory;
        }

        return Result<FileStorageDirectoryEntity>.Success(current);
    }

    private IQueryable<FileStorageFileEntity> BuildFileListQuery(TContext context, string normalizedRootPath, bool recursive, string continuationPath = null)
    {
        var query = context.StorageFiles
            .AsNoTracking()
            .Where(f => f.LocationName == this.LocationName);

        if (recursive)
        {
            if (normalizedRootPath.Length > 0)
            {
                var subtreePrefix = normalizedRootPath + "/";
                query = query.Where(f => f.NormalizedPath.StartsWith(subtreePrefix));
            }
        }
        else
        {
            var parentPathHash = this.ComputePathHash(normalizedRootPath);
            query = query.Where(f => f.ParentPathHash == parentPathHash && f.ParentPath == normalizedRootPath);
        }

        if (!string.IsNullOrEmpty(continuationPath))
        {
            query = query.Where(f => string.Compare(f.NormalizedPath, continuationPath) > 0);
        }

        return query;
    }

    private IQueryable<FileStorageDirectoryEntity> BuildDirectoryListQuery(TContext context, string normalizedRootPath, bool recursive)
    {
        var query = context.StorageDirectories
            .AsNoTracking()
            .Where(d => d.LocationName == this.LocationName)
            .Where(d => d.NormalizedPath != string.Empty)
            .Where(d => d.NormalizedPath != normalizedRootPath);

        if (recursive)
        {
            if (normalizedRootPath.Length == 0)
            {
                return query;
            }

            var subtreePrefix = normalizedRootPath + "/";
            return query.Where(d => d.NormalizedPath.StartsWith(subtreePrefix));
        }

        var parentPathHash = this.ComputePathHash(normalizedRootPath);
        return query.Where(d => d.ParentPathHash == parentPathHash && d.ParentPath == normalizedRootPath);
    }

    private Result ValidateContentConsistency(FileStorageFileEntity file, FileStorageFileContentEntity content, string path)
    {
        _ = file;

        var hasTextContent = content.ContentText is not null;
        var hasBinaryContent = content.ContentBinary is not null;
        if (hasTextContent == hasBinaryContent)
        {
            return Result.Failure()
                .WithError(new FileSystemError("Stored file content is invalid", path))
                .WithMessage($"Failed to read file at '{path}'");
        }

        if (hasTextContent && string.IsNullOrWhiteSpace(content.TextEncodingName))
        {
            return Result.Failure()
                .WithError(new FileSystemError("Stored file content is missing text encoding information", path))
                .WithMessage($"Failed to read file at '{path}'");
        }

        return Result.Success();
    }

    private async Task<FileStorageDirectoryEntity> EnsureRootDirectoryInternalAsync(TContext context, CancellationToken cancellationToken)
    {
        var existing = await this.TryGetTrackedDirectoryAsync(context, string.Empty, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var entity = new FileStorageDirectoryEntity
        {
            Id = Guid.NewGuid(),
            LocationName = this.LocationName,
            NormalizedPath = string.Empty,
            NormalizedPathHash = this.ComputePathHash(string.Empty),
            ParentPath = null,
            ParentPathHash = null,
            Name = string.Empty,
            IsExplicit = false,
            LastModified = DateTimeOffset.UtcNow,
            ConcurrencyVersion = Guid.NewGuid()
        };

        context.StorageDirectories.Add(entity);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return entity;
        }
        catch (DbUpdateException ex)
        {
            this.logger.LogDebug(ex, "Root directory row creation raced for {LocationName}; reloading persisted row", this.LocationName);

            context.Entry(entity).State = EntityState.Detached;

            var confirmed = await this.TryGetTrackedDirectoryAsync(context, string.Empty, cancellationToken);
            if (confirmed is not null)
            {
                return confirmed;
            }

            throw;
        }
    }

    private async Task<FileStorageFileContentEntity> SerializeContentInternalAsync(
        string normalizedPath,
        Stream content,
        CancellationToken cancellationToken)
    {
        await using var buffered = await this.BufferContentAsync(content, cancellationToken);
        return this.CreateFileContentEntity(normalizedPath, buffered.ToArray());
    }

    private async Task<FileStorageFileEntity> TryGetFileAsync(TContext context, string normalizedPath, CancellationToken cancellationToken)
    {
        var pathHash = this.ComputePathHash(normalizedPath);
        var candidates = await context.StorageFiles
            .AsNoTracking()
            .Where(f => f.LocationName == this.LocationName && f.NormalizedPathHash == pathHash)
            .ToListAsync(cancellationToken);

        var file = candidates.SingleOrDefault(f => f.NormalizedPath == normalizedPath);
        if (file is null && candidates.Count > 0)
        {
            this.logger.LogWarning(
                "Hash collision or inconsistent file path detected for {LocationName} and {NormalizedPath}",
                this.LocationName,
                normalizedPath);
        }

        return file;
    }

    private async Task<FileStorageFileEntity> TryGetTrackedFileAsync(
        TContext context,
        string normalizedPath,
        bool includeContent,
        CancellationToken cancellationToken)
    {
        var pathHash = this.ComputePathHash(normalizedPath);
        IQueryable<FileStorageFileEntity> query = context.StorageFiles
            .Where(f => f.LocationName == this.LocationName && f.NormalizedPathHash == pathHash);

        if (includeContent)
        {
            query = query.Include(f => f.Content);
        }

        var candidates = await query.ToListAsync(cancellationToken);
        var file = candidates.SingleOrDefault(f => f.NormalizedPath == normalizedPath);
        if (file is null && candidates.Count > 0)
        {
            this.logger.LogWarning(
                "Hash collision or inconsistent tracked file path detected for {LocationName} and {NormalizedPath}",
                this.LocationName,
                normalizedPath);
        }

        return file;
    }

    private async Task<FileStorageFileContentEntity> TryGetFileContentAsync(TContext context, Guid fileId, CancellationToken cancellationToken) =>
        await context.StorageFileContents
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.FileId == fileId, cancellationToken);

    private async Task<FileStorageDirectoryEntity> TryGetDirectoryAsync(TContext context, string normalizedPath, CancellationToken cancellationToken)
    {
        var pathHash = this.ComputePathHash(normalizedPath);
        var candidates = await context.StorageDirectories
            .AsNoTracking()
            .Where(d => d.LocationName == this.LocationName && d.NormalizedPathHash == pathHash)
            .ToListAsync(cancellationToken);

        var directory = candidates.SingleOrDefault(d => d.NormalizedPath == normalizedPath);
        if (directory is null && candidates.Count > 0)
        {
            this.logger.LogWarning(
                "Hash collision or inconsistent directory path detected for {LocationName} and {NormalizedPath}",
                this.LocationName,
                normalizedPath);
        }

        return directory;
    }

    private async Task<FileStorageDirectoryEntity> TryGetTrackedDirectoryAsync(
        TContext context,
        string normalizedPath,
        CancellationToken cancellationToken)
    {
        var pathHash = this.ComputePathHash(normalizedPath);
        var candidates = await context.StorageDirectories
            .Where(d => d.LocationName == this.LocationName && d.NormalizedPathHash == pathHash)
            .ToListAsync(cancellationToken);

        return candidates.SingleOrDefault(d => d.NormalizedPath == normalizedPath);
    }

    private async Task<Result> WriteFilePayloadAsync(
        string path,
        byte[] payloadBytes,
        IProgress<FileProgress> progress,
        CancellationToken cancellationToken)
    {
        this.EnsureBufferedContentSizeAllowed(payloadBytes.LongLength);

        return await this.ExecuteMutationAsync(
            [path],
            async (mutationContext, cancellationToken) =>
            {
                var mutationPath = mutationContext.PrimaryPath;
                if (mutationPath.NormalizedPath.Length == 0)
                {
                    return Result.Failure()
                        .WithError(new FileSystemError("Path cannot resolve to the storage root", path))
                        .WithMessage("Invalid path provided");
                }

                var directoryConflict = await this.TryGetTrackedDirectoryAsync(mutationContext.Context, mutationPath.NormalizedPath, cancellationToken);
                if (directoryConflict is not null)
                {
                    return this.CreatePathConflictFailure(path, $"Failed to write file at '{path}'", "directory", mutationPath.NormalizedPath);
                }

                var parentDirectoryResult = await this.EnsureDirectoryChainAsync(
                    mutationContext.Context,
                    mutationPath.ParentPath ?? string.Empty,
                    mutationContext.Timestamp,
                    explicitTarget: false,
                    path,
                    cancellationToken);
                if (parentDirectoryResult.IsFailure)
                {
                    return Result.Failure()
                        .WithErrors(parentDirectoryResult.Errors)
                        .WithMessages(parentDirectoryResult.Messages);
                }

                var existingFile = await this.TryGetTrackedFileAsync(mutationContext.Context, mutationPath.NormalizedPath, includeContent: true, cancellationToken);
                var persistedContent = this.CreateFileContentEntity(mutationPath.NormalizedPath, payloadBytes);
                var checksum = Convert.ToBase64String(SHA256.HashData(payloadBytes));

                if (existingFile is null)
                {
                    var fileId = Guid.NewGuid();
                    persistedContent.FileId = fileId;

                    existingFile = new FileStorageFileEntity
                    {
                        Id = fileId,
                        LocationName = this.LocationName,
                        NormalizedPath = mutationPath.NormalizedPath,
                        NormalizedPathHash = mutationPath.PathHash,
                        ParentPath = mutationPath.ParentPath,
                        ParentPathHash = mutationPath.ParentPathHash,
                        Name = mutationPath.Name,
                        ContentType = this.ResolveContentType(mutationPath.NormalizedPath),
                        Length = payloadBytes.LongLength,
                        Checksum = checksum,
                        LastModified = mutationContext.Timestamp,
                        ConcurrencyVersion = Guid.NewGuid(),
                        Content = persistedContent
                    };

                    mutationContext.Context.StorageFiles.Add(existingFile);
                }
                else
                {
                    existingFile.ParentPath = mutationPath.ParentPath;
                    existingFile.ParentPathHash = mutationPath.ParentPathHash;
                    existingFile.Name = mutationPath.Name;
                    existingFile.ContentType = this.ResolveContentType(mutationPath.NormalizedPath);
                    existingFile.Length = payloadBytes.LongLength;
                    existingFile.Checksum = checksum;
                    this.TouchFile(existingFile, mutationContext.Timestamp);

                    if (existingFile.Content is null)
                    {
                        persistedContent.FileId = existingFile.Id;
                        existingFile.Content = persistedContent;
                        mutationContext.Context.StorageFileContents.Add(persistedContent);
                    }
                    else
                    {
                        existingFile.Content.ContentText = persistedContent.ContentText;
                        existingFile.Content.TextEncodingName = persistedContent.TextEncodingName;
                        existingFile.Content.TextHasByteOrderMark = persistedContent.TextHasByteOrderMark;
                        existingFile.Content.ContentBinary = persistedContent.ContentBinary;
                    }
                }

                if (parentDirectoryResult.Value is not null)
                {
                    this.TouchDirectory(parentDirectoryResult.Value, mutationContext.Timestamp);
                }

                await mutationContext.Context.SaveChangesAsync(cancellationToken);

                progress?.Report(new FileProgress
                {
                    BytesProcessed = payloadBytes.LongLength,
                    FilesProcessed = 1,
                    TotalFiles = 1
                });

                return Result.Success()
                    .WithMessage($"Wrote file at '{path}'");
            },
            cancellationToken);
    }

    private async Task<Result> MoveFileInternalAsync(
        string path,
        string destinationPath,
        IProgress<FileProgress> progress,
        string successMessage,
        string failureMessage,
        string unexpectedMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            return await this.ExecuteMutationAsync(
                [path, destinationPath],
                async (mutationContext, cancellationToken) =>
                {
                    var sourcePath = mutationContext.GetPath(0);
                    var destinationMutationPath = mutationContext.GetPath(1);
                    if (sourcePath.NormalizedPath.Length == 0 || destinationMutationPath.NormalizedPath.Length == 0)
                    {
                        return Result.Failure()
                            .WithError(new FileSystemError("Path cannot resolve to the storage root", sourcePath.NormalizedPath.Length == 0 ? path : destinationPath))
                            .WithMessage("Invalid path provided");
                    }

                    var distinctPathResult = this.ValidateDistinctPaths(sourcePath, destinationMutationPath, failureMessage);
                    if (distinctPathResult.IsFailure)
                    {
                        return distinctPathResult;
                    }

                    var sourceFile = await this.TryGetTrackedFileAsync(mutationContext.Context, sourcePath.NormalizedPath, includeContent: false, cancellationToken);
                    if (sourceFile is null)
                    {
                        var directoryConflict = await this.TryGetTrackedDirectoryAsync(mutationContext.Context, sourcePath.NormalizedPath, cancellationToken);
                        return directoryConflict is not null
                            ? this.CreatePathConflictFailure(path, failureMessage, "directory", sourcePath.NormalizedPath)
                            : Result.Failure()
                                .WithError(new FileSystemError("Source file not found", path))
                                .WithMessage(failureMessage);
                    }

                    var destinationDirectoryConflict = await this.TryGetTrackedDirectoryAsync(mutationContext.Context, destinationMutationPath.NormalizedPath, cancellationToken);
                    if (destinationDirectoryConflict is not null)
                    {
                        return this.CreatePathConflictFailure(destinationPath, failureMessage, "directory", destinationMutationPath.NormalizedPath);
                    }

                    var destinationParentResult = await this.EnsureDirectoryChainAsync(
                        mutationContext.Context,
                        destinationMutationPath.ParentPath ?? string.Empty,
                        mutationContext.Timestamp,
                        explicitTarget: false,
                        destinationPath,
                        cancellationToken);
                    if (destinationParentResult.IsFailure)
                    {
                        return Result.Failure()
                            .WithErrors(destinationParentResult.Errors)
                            .WithMessages(destinationParentResult.Messages);
                    }

                    var existingDestinationFile = await this.TryGetTrackedFileAsync(
                        mutationContext.Context,
                        destinationMutationPath.NormalizedPath,
                        includeContent: true,
                        cancellationToken);
                    if (existingDestinationFile is not null)
                    {
                        if (existingDestinationFile.Content is not null)
                        {
                            mutationContext.Context.StorageFileContents.Remove(existingDestinationFile.Content);
                        }

                        mutationContext.Context.StorageFiles.Remove(existingDestinationFile);
                        await mutationContext.Context.SaveChangesAsync(cancellationToken);
                    }

                    var sourceParentPath = sourcePath.ParentPath;
                    this.UpdateFileLocation(sourceFile, destinationMutationPath, updateLastModified: false, mutationContext.Timestamp);

                    await this.TouchDirectoriesAsync(
                        mutationContext.Context,
                        [sourceParentPath, destinationMutationPath.ParentPath],
                        mutationContext.Timestamp,
                        cancellationToken);
                    await mutationContext.Context.SaveChangesAsync(cancellationToken);

                    await this.PruneImplicitDirectoriesAsync(mutationContext.Context, sourceParentPath, mutationContext.Timestamp, cancellationToken);
                    await mutationContext.Context.SaveChangesAsync(cancellationToken);

                    progress?.Report(new FileProgress
                    {
                        BytesProcessed = sourceFile.Length,
                        FilesProcessed = 1,
                        TotalFiles = 1
                    });

                    return Result.Success()
                        .WithMessage(successMessage);
                },
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DbUpdateException ex)
        {
            return await this.TranslateMutationDbUpdateExceptionAsync([path, destinationPath], ex, failureMessage, cancellationToken);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Entity Framework file relocation failed for {LocationName}, {Path}, and {DestinationPath}", this.LocationName, path, destinationPath);

            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage(unexpectedMessage);
        }
    }

    private MutationPath CreateMutationPath(string path, int originalIndex)
    {
        var normalizedPath = this.NormalizePath(path);
        var parentPath = this.GetParentPath(normalizedPath);

        return new MutationPath(
            originalIndex,
            path,
            normalizedPath,
            parentPath,
            this.GetEntryName(normalizedPath),
            this.ComputePathHash(normalizedPath),
            parentPath is null ? null : this.ComputePathHash(parentPath));
    }

    private IReadOnlyList<MutationPath> CreateOrderedMutationPaths(IEnumerable<string> paths) =>
        paths
            .Where(path => path is not null)
            .Select((path, index) => this.CreateMutationPath(path, index))
            .OrderBy(path => path.NormalizedPath, StringComparer.Ordinal)
            .ThenBy(path => path.OriginalPath, StringComparer.Ordinal)
            .ToList();

    private async Task<IDbContextTransaction> BeginMutationTransactionAsync(TContext context, CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is not null)
        {
            return null;
        }

        try
        {
            return await context.Database.BeginTransactionAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
        {
            this.logger.LogDebug(ex, "Explicit transaction support is not available for file storage location {LocationName}", this.LocationName);
            return null;
        }
    }

    private async Task<Result> TranslateMutationDbUpdateExceptionAsync(
        string path,
        DbUpdateException exception,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        var leaseContention = this.FindLeaseContentionException(exception);
        if (leaseContention is not null)
        {
            var details = string.IsNullOrWhiteSpace(leaseContention.Path)
                ? string.Empty
                : $" (contention at '{leaseContention.Path}')";

            return Result.Failure()
                .WithError(new ResourceUnavailableError("Storage path is currently locked by another operation"))
                .WithMessage(failureMessage + details);
        }

        if (this.IsTransientLockContentionException(exception, providerName: null))
        {
            return Result.Failure()
                .WithError(new ResourceUnavailableError("Storage mutation could not acquire the required database locks in time"))
                .WithMessage(failureMessage);
        }

        try
        {
            var normalizedPath = this.NormalizePath(path);
            using var scope = this.CreateScope();
            var context = this.ResolveContext(scope);

            var validationResult = this.ValidateStorageModel(context);
            if (validationResult.IsFailure)
            {
                return Result.Failure()
                    .WithErrors(validationResult.Errors)
                    .WithMessages(validationResult.Messages);
            }

            var directory = await this.TryGetDirectoryAsync(context, normalizedPath, cancellationToken);
            if (directory is not null)
            {
                return this.CreatePathConflictFailure(path, failureMessage, "directory", directory.NormalizedPath, exception);
            }

            var file = await this.TryGetFileAsync(context, normalizedPath, cancellationToken);
            if (file is not null)
            {
                return this.CreatePathConflictFailure(path, failureMessage, "file", file.NormalizedPath, exception);
            }

            var conflictingAncestor = await this.FindAncestorFileConflictAsync(context, normalizedPath, cancellationToken);
            if (conflictingAncestor is not null)
            {
                return this.CreatePathConflictFailure(path, failureMessage, "file", conflictingAncestor.NormalizedPath, exception);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this.logger.LogDebug(ex, "Mutation conflict inspection failed for {LocationName} and {Path}", this.LocationName, path);
        }

        return Result.Failure()
            .WithError(new FileSystemError("Storage mutation conflicted with another operation", path, exception))
            .WithMessage(failureMessage);
    }

    private async Task<Result> TranslateMutationDbUpdateExceptionAsync(
        IEnumerable<string> paths,
        DbUpdateException exception,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        foreach (var path in paths
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var translatedResult = await this.TranslateMutationDbUpdateExceptionAsync(path, exception, failureMessage, cancellationToken);
            if (translatedResult.Errors?.Any(error => error is ConflictError or ResourceUnavailableError) == true)
            {
                return translatedResult;
            }
        }

        return Result.Failure()
            .WithError(new FileSystemError("Storage mutation conflicted with another operation", paths.FirstOrDefault(), exception))
            .WithMessage(failureMessage);
    }

    private async Task<FileStorageFileEntity> FindAncestorFileConflictAsync(
        TContext context,
        string normalizedPath,
        CancellationToken cancellationToken)
    {
        var currentPath = this.GetParentPath(normalizedPath);
        while (currentPath is not null)
        {
            var file = await this.TryGetFileAsync(context, currentPath, cancellationToken);
            if (file is not null)
            {
                return file;
            }

            currentPath = this.GetParentPath(currentPath);
        }

        return null;
    }

    private async Task<bool> HasDirectChildrenAsync(TContext context, string normalizedPath, CancellationToken cancellationToken)
    {
        var pathHash = this.ComputePathHash(normalizedPath);
        var deletedFileIds = context.ChangeTracker
            .Entries<FileStorageFileEntity>()
            .Where(entry => entry.State == EntityState.Deleted &&
                entry.Entity.LocationName == this.LocationName &&
                entry.Entity.ParentPathHash == pathHash &&
                entry.Entity.ParentPath == normalizedPath)
            .Select(entry => entry.Entity.Id)
            .ToArray();
        var hasFiles = await context.StorageFiles.AnyAsync(
            f => f.LocationName == this.LocationName &&
                f.ParentPathHash == pathHash &&
                f.ParentPath == normalizedPath &&
                !deletedFileIds.Contains(f.Id),
            cancellationToken);
        if (hasFiles)
        {
            return true;
        }

        var deletedDirectoryIds = context.ChangeTracker
            .Entries<FileStorageDirectoryEntity>()
            .Where(entry => entry.State == EntityState.Deleted &&
                entry.Entity.LocationName == this.LocationName &&
                entry.Entity.ParentPathHash == pathHash &&
                entry.Entity.ParentPath == normalizedPath)
            .Select(entry => entry.Entity.Id)
            .ToArray();

        return await context.StorageDirectories.AnyAsync(
            d => d.LocationName == this.LocationName &&
                d.ParentPathHash == pathHash &&
                d.ParentPath == normalizedPath &&
                !deletedDirectoryIds.Contains(d.Id),
            cancellationToken);
    }

    private async Task PruneImplicitDirectoriesAsync(
        TContext context,
        string startingPath,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        var currentPath = startingPath;
        while (!string.IsNullOrEmpty(currentPath))
        {
            var directory = await this.TryGetTrackedDirectoryAsync(context, currentPath, cancellationToken);
            if (directory is null || directory.IsExplicit || await this.HasDirectChildrenAsync(context, currentPath, cancellationToken))
            {
                break;
            }

            var parentPath = directory.ParentPath;
            context.StorageDirectories.Remove(directory);
            if (!string.IsNullOrEmpty(parentPath))
            {
                var parentDirectory = await this.TryGetTrackedDirectoryAsync(context, parentPath, cancellationToken);
                if (parentDirectory is not null)
                {
                    this.TouchDirectory(parentDirectory, timestamp);
                }
            }

            currentPath = parentPath;
        }
    }

    private async Task TouchDirectoriesAsync(
        TContext context,
        IEnumerable<string> directoryPaths,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        foreach (var directoryPath in directoryPaths
                     .Where(path => path is not null)
                     .Distinct(StringComparer.Ordinal))
        {
            var directory = await this.TryGetTrackedDirectoryAsync(context, directoryPath, cancellationToken);
            if (directory is not null)
            {
                this.TouchDirectory(directory, now);
            }
        }
    }

    private async Task TouchParentDirectoryAsync(TContext context, string parentPath, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (parentPath is null)
        {
            return;
        }

        var directory = await this.TryGetTrackedDirectoryAsync(context, parentPath, cancellationToken);
        if (directory is not null)
        {
            this.TouchDirectory(directory, now);
        }
    }

    private void TouchDirectory(FileStorageDirectoryEntity directory, DateTimeOffset timestamp)
    {
        directory.LastModified = timestamp;
        directory.ConcurrencyVersion = Guid.NewGuid();
    }

    private void TouchFile(FileStorageFileEntity file, DateTimeOffset timestamp)
    {
        file.LastModified = timestamp;
        file.ConcurrencyVersion = Guid.NewGuid();
    }

    private void UpdateDirectoryLocation(
        FileStorageDirectoryEntity directory,
        string normalizedPath,
        bool updateLastModified,
        DateTimeOffset timestamp)
    {
        var parentPath = this.GetParentPath(normalizedPath);
        directory.NormalizedPath = normalizedPath;
        directory.NormalizedPathHash = this.ComputePathHash(normalizedPath);
        directory.ParentPath = parentPath;
        directory.ParentPathHash = parentPath is null ? null : this.ComputePathHash(parentPath);
        directory.Name = this.GetEntryName(normalizedPath);
        directory.ConcurrencyVersion = Guid.NewGuid();

        if (updateLastModified)
        {
            directory.LastModified = timestamp;
        }
    }

    private void UpdateFileLocation(
        FileStorageFileEntity file,
        MutationPath path,
        bool updateLastModified,
        DateTimeOffset timestamp)
    {
        file.NormalizedPath = path.NormalizedPath;
        file.NormalizedPathHash = path.PathHash;
        file.ParentPath = path.ParentPath;
        file.ParentPathHash = path.ParentPathHash;
        file.Name = path.Name;
        file.ContentType = this.ResolveContentType(path.NormalizedPath);
        file.ConcurrencyVersion = Guid.NewGuid();

        if (updateLastModified)
        {
            file.LastModified = timestamp;
        }
    }

    private void UpdateFileLocation(
        FileStorageFileEntity file,
        string normalizedPath,
        bool updateLastModified,
        DateTimeOffset timestamp)
        => this.UpdateFileLocation(file, this.CreateMutationPath(normalizedPath, originalIndex: -1), updateLastModified, timestamp);

    private FileStorageFileContentEntity CloneFileContentEntity(FileStorageFileContentEntity source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new FileStorageFileContentEntity
        {
            ContentText = source.ContentText,
            TextEncodingName = source.TextEncodingName,
            TextHasByteOrderMark = source.TextHasByteOrderMark,
            ContentBinary = source.ContentBinary?.ToArray()
        };
    }

    private FileStorageFileContentEntity CreateFileContentEntity(string normalizedPath, byte[] bytes)
    {
        var contentType = this.ResolveContentType(normalizedPath);
        if (contentType.IsBinary())
        {
            return CreateBinaryFileContentEntity(bytes);
        }

        if (TryCreateTextFileContentEntity(bytes, out var textContent))
        {
            return textContent;
        }

        return CreateBinaryFileContentEntity(bytes);
    }

    private static FileStorageFileContentEntity CreateBinaryFileContentEntity(byte[] bytes) =>
        new()
        {
            ContentBinary = bytes,
            ContentText = null,
            TextEncodingName = null,
            TextHasByteOrderMark = false
        };

    private static bool TryCreateTextFileContentEntity(byte[] bytes, out FileStorageFileContentEntity content)
    {
        try
        {
            var encodingInfo = DetectTextEncoding(bytes);
            var text = encodingInfo.Encoding.GetString(bytes, encodingInfo.PreambleLength, bytes.Length - encodingInfo.PreambleLength);
            var roundTripBytes = EncodeTextContent(text, encodingInfo.Encoding.WebName, encodingInfo.HasByteOrderMark);
            if (!roundTripBytes.AsSpan().SequenceEqual(bytes))
            {
                content = null;
                return false;
            }

            content = CreateTextFileContentEntity(text, encodingInfo.Encoding.WebName, encodingInfo.HasByteOrderMark);

            return true;
        }
        catch (ArgumentException)
        {
            content = null;
            return false;
        }
    }

    private static FileStorageFileContentEntity CreateTextFileContentEntity(
        string text,
        string encodingName,
        bool hasByteOrderMark)
    {
        return new FileStorageFileContentEntity
        {
            ContentBinary = null,
            ContentText = text,
            TextEncodingName = encodingName,
            TextHasByteOrderMark = hasByteOrderMark
        };
    }

    private void EnsureBufferedContentSizeAllowed(long actualSize)
    {
        if (this.options.MaximumBufferedContentSize is long limit && actualSize > limit)
        {
            throw new BufferedContentLimitExceededException(limit, actualSize);
        }
    }

    private Result CreateBufferedContentLimitFailure(string path, BufferedContentLimitExceededException exception, string failureMessage) =>
        Result.Failure()
            .WithError(new ValidationError(
                $"Buffered content size {exception.ActualSize} bytes exceeds the configured limit of {exception.Limit} bytes.",
                nameof(EntityFrameworkFileStorageOptions.MaximumBufferedContentSize),
                exception.ActualSize))
            .WithMessage(failureMessage);

    private Result CreatePathConflictFailure(
        string path,
        string failureMessage,
        string conflictingEntryType,
        string conflictingPath,
        Exception exception = null)
    {
        var details = string.IsNullOrWhiteSpace(conflictingPath)
            ? string.Empty
            : $" (conflict at '{conflictingPath}')";

        return Result.Failure()
            .WithError(new ConflictError($"Path conflicts with an existing {conflictingEntryType}"))
            .WithMessage(failureMessage + details);
    }

    private Result CreateSemanticConflictFailure(string failureMessage, string conflictMessage) =>
        Result.Failure()
            .WithError(new ConflictError(conflictMessage))
            .WithMessage(failureMessage);

    private async Task<MutationLeaseHandle> AcquireMutationLeasesAsync(
        TContext context,
        IReadOnlyList<MutationPath> mutationPaths,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (this.options.LeaseDuration <= TimeSpan.Zero || mutationPaths.Count == 0)
        {
            return MutationLeaseHandle.Empty;
        }

        var leaseTargets = await this.ResolveMutationLeaseTargetsAsync(context, mutationPaths, cancellationToken);
        if (leaseTargets.Count == 0)
        {
            return MutationLeaseHandle.Empty;
        }

        var leaseOwner = this.GetLeaseOwner();
        var lockedUntil = now.Add(this.options.LeaseDuration);

        if (this.SupportsExecuteUpdate(context))
        {
            foreach (var target in leaseTargets)
            {
                var updatedRows = target.Kind == MutationLeaseTargetKind.Directory
                    ? await context.StorageDirectories
                        .Where(d => d.Id == target.Id && (d.LockedUntil == null || d.LockedUntil < now || d.LockedBy == leaseOwner))
                        .ExecuteUpdateAsync(
                            setters => setters
                                .SetProperty(d => d.LockedBy, leaseOwner)
                                .SetProperty(d => d.LockedUntil, lockedUntil)
                                .SetProperty(d => d.ConcurrencyVersion, Guid.NewGuid()),
                            cancellationToken)
                    : await context.StorageFiles
                        .Where(f => f.Id == target.Id && (f.LockedUntil == null || f.LockedUntil < now || f.LockedBy == leaseOwner))
                        .ExecuteUpdateAsync(
                            setters => setters
                                .SetProperty(f => f.LockedBy, leaseOwner)
                                .SetProperty(f => f.LockedUntil, lockedUntil)
                                .SetProperty(f => f.ConcurrencyVersion, Guid.NewGuid()),
                            cancellationToken);

                if (updatedRows == 0)
                {
                    throw new MutationLeaseContentionException(target.Path);
                }
            }
        }
        else
        {
            var directoryIds = leaseTargets
                .Where(target => target.Kind == MutationLeaseTargetKind.Directory)
                .Select(target => target.Id)
                .ToHashSet();
            var fileIds = leaseTargets
                .Where(target => target.Kind == MutationLeaseTargetKind.File)
                .Select(target => target.Id)
                .ToHashSet();
            var trackedDirectories = await context.StorageDirectories
                .Where(d => directoryIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, cancellationToken);
            var trackedFiles = await context.StorageFiles
                .Where(f => fileIds.Contains(f.Id))
                .ToDictionaryAsync(f => f.Id, cancellationToken);

            foreach (var target in leaseTargets)
            {
                if (target.Kind == MutationLeaseTargetKind.Directory)
                {
                    if (!trackedDirectories.TryGetValue(target.Id, out var directory) ||
                        (directory.LockedUntil >= now && !string.Equals(directory.LockedBy, leaseOwner, StringComparison.Ordinal)))
                    {
                        throw new MutationLeaseContentionException(target.Path);
                    }

                    directory.LockedBy = leaseOwner;
                    directory.LockedUntil = lockedUntil;
                    directory.ConcurrencyVersion = Guid.NewGuid();
                }
                else
                {
                    if (!trackedFiles.TryGetValue(target.Id, out var file) ||
                        (file.LockedUntil >= now && !string.Equals(file.LockedBy, leaseOwner, StringComparison.Ordinal)))
                    {
                        throw new MutationLeaseContentionException(target.Path);
                    }

                    file.LockedBy = leaseOwner;
                    file.LockedUntil = lockedUntil;
                    file.ConcurrencyVersion = Guid.NewGuid();
                }
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        return new MutationLeaseHandle(leaseTargets);
    }

    private async Task ReleaseMutationLeasesAsync(
        TContext context,
        MutationLeaseHandle leaseHandle,
        CancellationToken cancellationToken)
    {
        if (leaseHandle.IsEmpty)
        {
            return;
        }

        var leaseOwner = this.GetLeaseOwner();

        if (this.SupportsExecuteUpdate(context))
        {
            foreach (var target in leaseHandle.Targets.Reverse())
            {
                if (target.Kind == MutationLeaseTargetKind.Directory)
                {
                    await context.StorageDirectories
                        .Where(d => d.Id == target.Id && d.LockedBy == leaseOwner)
                        .ExecuteUpdateAsync(
                            setters => setters
                                .SetProperty(d => d.LockedBy, (string)null)
                                .SetProperty(d => d.LockedUntil, (DateTimeOffset?)null)
                                .SetProperty(d => d.ConcurrencyVersion, Guid.NewGuid()),
                            cancellationToken);
                }
                else
                {
                    await context.StorageFiles
                        .Where(f => f.Id == target.Id && f.LockedBy == leaseOwner)
                        .ExecuteUpdateAsync(
                            setters => setters
                                .SetProperty(f => f.LockedBy, (string)null)
                                .SetProperty(f => f.LockedUntil, (DateTimeOffset?)null)
                                .SetProperty(f => f.ConcurrencyVersion, Guid.NewGuid()),
                            cancellationToken);
                }
            }

            return;
        }

        var directoryIds = leaseHandle.Targets
            .Where(target => target.Kind == MutationLeaseTargetKind.Directory)
            .Select(target => target.Id)
            .ToHashSet();
        var fileIds = leaseHandle.Targets
            .Where(target => target.Kind == MutationLeaseTargetKind.File)
            .Select(target => target.Id)
            .ToHashSet();
        var trackedDirectories = await context.StorageDirectories
            .Where(d => directoryIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, cancellationToken);
        var trackedFiles = await context.StorageFiles
            .Where(f => fileIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, cancellationToken);

        foreach (var target in leaseHandle.Targets.Reverse())
        {
            if (target.Kind == MutationLeaseTargetKind.Directory)
            {
                if (trackedDirectories.TryGetValue(target.Id, out var directory) &&
                    string.Equals(directory.LockedBy, leaseOwner, StringComparison.Ordinal))
                {
                    directory.LockedBy = null;
                    directory.LockedUntil = null;
                    directory.ConcurrencyVersion = Guid.NewGuid();
                }
            }
            else if (trackedFiles.TryGetValue(target.Id, out var file) &&
                     string.Equals(file.LockedBy, leaseOwner, StringComparison.Ordinal))
            {
                file.LockedBy = null;
                file.LockedUntil = null;
                file.ConcurrencyVersion = Guid.NewGuid();
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task ReloadTrackedLeaseTargetsAsync(
        TContext context,
        MutationLeaseHandle leaseHandle,
        CancellationToken cancellationToken)
    {
        if (leaseHandle.IsEmpty || !this.SupportsExecuteUpdate(context))
        {
            return;
        }

        var directoryIds = leaseHandle.Targets
            .Where(target => target.Kind == MutationLeaseTargetKind.Directory)
            .Select(target => target.Id)
            .ToHashSet();
        var fileIds = leaseHandle.Targets
            .Where(target => target.Kind == MutationLeaseTargetKind.File)
            .Select(target => target.Id)
            .ToHashSet();

        foreach (var entry in context.ChangeTracker.Entries<FileStorageDirectoryEntity>().ToList())
        {
            if (entry.State != EntityState.Detached && directoryIds.Contains(entry.Entity.Id))
            {
                await entry.ReloadAsync(cancellationToken);
            }
        }

        foreach (var entry in context.ChangeTracker.Entries<FileStorageFileEntity>().ToList())
        {
            if (entry.State != EntityState.Detached && fileIds.Contains(entry.Entity.Id))
            {
                await entry.ReloadAsync(cancellationToken);
            }
        }
    }

    private async Task<IReadOnlyList<MutationLeaseTarget>> ResolveMutationLeaseTargetsAsync(
        TContext context,
        IReadOnlyList<MutationPath> mutationPaths,
        CancellationToken cancellationToken)
    {
        var chainPaths = mutationPaths
            .SelectMany(path => this.EnumerateAncestorPathsInclusive(path.NormalizedPath))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (chainPaths.Count == 0)
        {
            return [];
        }

        var pathLookup = chainPaths.ToHashSet(StringComparer.Ordinal);
        var pathHashes = chainPaths
            .Select(this.ComputePathHash)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var directories = await context.StorageDirectories
            .AsNoTracking()
            .Where(d => d.LocationName == this.LocationName && pathHashes.Contains(d.NormalizedPathHash))
            .Select(d => new MutationLeaseTarget(MutationLeaseTargetKind.Directory, d.Id, d.NormalizedPath))
            .ToListAsync(cancellationToken);
        var files = await context.StorageFiles
            .AsNoTracking()
            .Where(f => f.LocationName == this.LocationName && pathHashes.Contains(f.NormalizedPathHash))
            .Select(f => new MutationLeaseTarget(MutationLeaseTargetKind.File, f.Id, f.NormalizedPath))
            .ToListAsync(cancellationToken);
        var directoryLookup = directories
            .Where(target => pathLookup.Contains(target.Path))
            .ToDictionary(target => target.Path, StringComparer.Ordinal);
        var fileLookup = files
            .Where(target => pathLookup.Contains(target.Path))
            .ToDictionary(target => target.Path, StringComparer.Ordinal);
        var selectedTargets = new Dictionary<string, MutationLeaseTarget>(StringComparer.Ordinal);

        foreach (var mutationPath in mutationPaths)
        {
            var chain = this.EnumerateAncestorPathsInclusive(mutationPath.NormalizedPath).ToArray();
            var directoryLeasePaths = chain
                .Where(path => directoryLookup.ContainsKey(path) && path.Length > 0)
                .ToList();

            if (directoryLeasePaths.Count == 0 && directoryLookup.ContainsKey(string.Empty))
            {
                directoryLeasePaths.Add(string.Empty);
            }

            foreach (var path in directoryLeasePaths)
            {
                selectedTargets.TryAdd($"D|{path}", directoryLookup[path]);
            }

            foreach (var path in chain)
            {
                if (fileLookup.TryGetValue(path, out var fileTarget))
                {
                    selectedTargets.TryAdd($"F|{path}", fileTarget);
                }
            }
        }

        return selectedTargets.Values
            .OrderBy(target => target.Path, StringComparer.Ordinal)
            .ThenBy(target => target.Kind)
            .ToList();
    }

    private IEnumerable<string> EnumerateAncestorPathsInclusive(string normalizedPath)
    {
        var currentPath = normalizedPath ?? string.Empty;

        while (true)
        {
            yield return currentPath;

            if (currentPath.Length == 0)
            {
                yield break;
            }

            currentPath = this.GetParentPath(currentPath) ?? string.Empty;
        }
    }

    private Result ValidateDistinctPaths(MutationPath sourcePath, MutationPath destinationPath, string failureMessage) =>
        string.Equals(sourcePath.NormalizedPath, destinationPath.NormalizedPath, StringComparison.Ordinal)
            ? this.CreateSemanticConflictFailure(failureMessage, "Source and destination paths cannot be the same")
            : Result.Success();

    private Result ValidateDirectoryRenamePaths(MutationPath sourcePath, MutationPath destinationPath, string failureMessage)
    {
        var distinctPathResult = this.ValidateDistinctPaths(sourcePath, destinationPath, failureMessage);
        if (distinctPathResult.IsFailure)
        {
            return distinctPathResult;
        }

        if (IsPathWithinSubtree(destinationPath.NormalizedPath, sourcePath.NormalizedPath))
        {
            return this.CreateSemanticConflictFailure(failureMessage, "Destination path cannot be inside the source subtree");
        }

        if (IsPathWithinSubtree(sourcePath.NormalizedPath, destinationPath.NormalizedPath))
        {
            return this.CreateSemanticConflictFailure(failureMessage, "Source path cannot be inside the destination subtree");
        }

        return Result.Success();
    }

    private Exception CreateMutationCommitException(Result result, string path)
    {
        var message = result.Messages?.FirstOrDefault()
            ?? result.Errors?.FirstOrDefault()?.Message
            ?? $"Failed to finalize file write at '{path}'.";

        return new IOException(message);
    }

    private bool ShouldRetryMutationException(TContext context, Exception exception, bool hasTransaction)
    {
        if (!hasTransaction)
        {
            return false;
        }

        return this.FindLeaseContentionException(exception) is not null ||
            exception is DbUpdateConcurrencyException ||
            this.HasDirectoryInsertRetryableConflict(exception) ||
            this.IsTransientLockContentionException(exception, context.Database.ProviderName);
    }

    private bool HasDirectoryInsertRetryableConflict(Exception exception) =>
        exception is DbUpdateException dbUpdateException &&
        dbUpdateException.Entries.Any(entry => entry.Entity is FileStorageDirectoryEntity && entry.State == EntityState.Added);

    private bool IsTransientLockContentionException(Exception exception, string providerName)
    {
        var current = exception;
        while (current is not null)
        {
            if (current is TimeoutException)
            {
                return true;
            }

            var isSqlServer = string.Equals(providerName, "Microsoft.EntityFrameworkCore.SqlServer", StringComparison.Ordinal) ||
                current.GetType().FullName?.Contains("SqlException", StringComparison.Ordinal) == true;
            if (isSqlServer &&
                this.TryGetExceptionIntProperty(current, "Number", out var sqlServerNumber) &&
                sqlServerNumber is 1205 or 1222 or 3960 or 41302 or 41305 or 41325 or 41839)
            {
                return true;
            }

            var isSqlite = string.Equals(providerName, "Microsoft.EntityFrameworkCore.Sqlite", StringComparison.Ordinal) ||
                current.GetType().FullName?.Contains("SqliteException", StringComparison.Ordinal) == true;
            if (isSqlite)
            {
                if (this.TryGetExceptionIntProperty(current, "SqliteErrorCode", out var sqliteErrorCode) &&
                    sqliteErrorCode is 5 or 6)
                {
                    return true;
                }

                if (current.Message.Contains("database is locked", StringComparison.OrdinalIgnoreCase) ||
                    current.Message.Contains("database is busy", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            var isPostgres = string.Equals(providerName, "Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal) ||
                current.GetType().FullName?.Contains("PostgresException", StringComparison.Ordinal) == true;
            if (isPostgres &&
                this.TryGetExceptionStringProperty(current, "SqlState", out var sqlState) &&
                sqlState is "40001" or "40P01" or "55P03")
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }

    private TimeSpan CalculateRetryDelay(int attempt)
    {
        if (this.options.RetryBackoff <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var multiplier = 1L << Math.Min(Math.Max(0, attempt - 1), 6);
        var ticks = this.options.RetryBackoff.Ticks;
        if (ticks > long.MaxValue / multiplier)
        {
            return TimeSpan.MaxValue;
        }

        return TimeSpan.FromTicks(ticks * multiplier);
    }

    private Exception WrapRetryableMutationException(Exception exception) =>
        exception is DbUpdateException
            ? exception
            : new DbUpdateException("Storage mutation failed after exhausting replay-safe transient retries.", exception);

    private MutationLeaseContentionException FindLeaseContentionException(Exception exception)
    {
        var current = exception;
        while (current is not null)
        {
            if (current is MutationLeaseContentionException leaseException)
            {
                return leaseException;
            }

            current = current.InnerException;
        }

        return null;
    }

    private bool TryGetExceptionIntProperty(Exception exception, string propertyName, out int value)
    {
        var property = exception.GetType().GetProperty(propertyName);
        if (property?.PropertyType == typeof(int) &&
            property.GetValue(exception) is int intValue)
        {
            value = intValue;
            return true;
        }

        value = default;
        return false;
    }

    private bool TryGetExceptionStringProperty(Exception exception, string propertyName, out string value)
    {
        var property = exception.GetType().GetProperty(propertyName);
        if (property?.PropertyType == typeof(string) &&
            property.GetValue(exception) is string stringValue)
        {
            value = stringValue;
            return true;
        }

        value = null;
        return false;
    }

    private bool SupportsExecuteUpdate(TContext dbContext)
    {
        var providerName = dbContext.Database.ProviderName;

        return !(providerName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) ?? false) &&
               !(providerName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private string EncodeSeekContinuationToken(
        string normalizedRootPath,
        bool recursive,
        string searchPattern,
        string lastReturnedPath)
    {
        var payload = new SeekContinuationTokenPayload
        {
            LocationName = this.LocationName,
            RootPath = normalizedRootPath ?? string.Empty,
            Recursive = recursive,
            SearchPattern = searchPattern ?? string.Empty,
            LastReturnedPath = lastReturnedPath ?? string.Empty
        };

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
    }

    private Result<string> ParseSeekContinuationToken(
        string continuationToken,
        string normalizedRootPath,
        bool recursive,
        string searchPattern)
    {
        if (string.IsNullOrWhiteSpace(continuationToken))
        {
            return Result<string>.Success(null);
        }

        try
        {
            var payloadBytes = Convert.FromBase64String(continuationToken);
            var payload = JsonSerializer.Deserialize<SeekContinuationTokenPayload>(payloadBytes);
            if (payload is null || string.IsNullOrWhiteSpace(payload.LocationName))
            {
                return InvalidContinuationToken();
            }

            if (!string.Equals(payload.LocationName, this.LocationName, StringComparison.Ordinal) ||
                !string.Equals(payload.RootPath ?? string.Empty, normalizedRootPath ?? string.Empty, StringComparison.Ordinal) ||
                payload.Recursive != recursive ||
                !string.Equals(payload.SearchPattern ?? string.Empty, searchPattern ?? string.Empty, StringComparison.Ordinal))
            {
                return InvalidContinuationToken("Invalid continuation token shape");
            }

            if (!string.IsNullOrEmpty(payload.LastReturnedPath) &&
                !IsPathWithinSubtree(payload.LastReturnedPath, normalizedRootPath))
            {
                return InvalidContinuationToken("Invalid continuation token shape");
            }

            return Result<string>.Success(payload.LastReturnedPath);
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return InvalidContinuationToken();
        }
    }

    private static Result<string> InvalidContinuationToken(string message = "Invalid continuation token") =>
        Result<string>.Failure()
            .WithError(new ValidationError(message, "continuationToken"))
            .WithMessage("The supplied continuation token is invalid for this file listing request");

    private static bool IsPathWithinSubtree(string path, string rootPath)
    {
        var normalizedPath = path ?? string.Empty;
        var normalizedRootPath = rootPath ?? string.Empty;
        if (normalizedRootPath.Length == 0)
        {
            return true;
        }

        return normalizedPath == normalizedRootPath || normalizedPath.StartsWith(normalizedRootPath + "/", StringComparison.Ordinal);
    }

    private static string RewriteSubtreePath(string sourceRootPath, string destinationRootPath, string currentPath) =>
        string.Equals(currentPath, sourceRootPath, StringComparison.Ordinal)
            ? destinationRootPath
            : destinationRootPath + currentPath[sourceRootPath.Length..];

    private static (Encoding Encoding, bool HasByteOrderMark, int PreambleLength) DetectTextEncoding(byte[] bytes)
    {
        if (bytes.Length >= 4 && bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF)
        {
            return (new UTF32Encoding(true, true, true), true, 4);
        }

        if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00)
        {
            return (new UTF32Encoding(false, true, true), true, 4);
        }

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return (new UTF8Encoding(true, true), true, 3);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return (new UnicodeEncoding(true, true, true), true, 2);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return (new UnicodeEncoding(false, true, true), true, 2);
        }

        return (new UTF8Encoding(false, true), false, 0);
    }

    private static byte[] EncodeTextContent(string text, string encodingName, bool hasByteOrderMark)
    {
        var encoding = CreateStrictEncoding(encodingName);
        var body = encoding.GetBytes(text ?? string.Empty);
        if (!hasByteOrderMark)
        {
            return body;
        }

        var preamble = encoding.GetPreamble();
        if (preamble.Length == 0)
        {
            return body;
        }

        var bytes = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, bytes, preamble.Length, body.Length);

        return bytes;
    }

    private static Encoding CreateStrictEncoding(string encodingName) =>
        Encoding.GetEncoding(
            encodingName ?? Encoding.UTF8.WebName,
            EncoderFallback.ExceptionFallback,
            DecoderFallback.ExceptionFallback);

    private static async Task<string> ComputeChecksumAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream, cancellationToken);

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        return Convert.ToBase64String(hash);
    }

    private static EntityFrameworkFileStorageOptions CreateOptions(
        ILoggerFactory loggerFactory,
        EntityFrameworkFileStorageConfiguration configuration)
    {
        var options = new EntityFrameworkFileStorageOptionsBuilder()
            .Apply(configuration)
            .Build();

        options.LoggerFactory ??= loggerFactory ?? NullLoggerFactory.Instance;

        return options;
    }

    private sealed class SeekContinuationTokenPayload
    {
        public string LocationName { get; set; }

        public string RootPath { get; set; }

        public bool Recursive { get; set; }

        public string SearchPattern { get; set; }

        public string LastReturnedPath { get; set; }
    }

    private sealed record FileListRow(string NormalizedPath, string Name);

    private sealed record DirectoryListRow(string NormalizedPath, string Name);

    private sealed record MutationLeaseHandle(IReadOnlyList<MutationLeaseTarget> Targets)
    {
        public static MutationLeaseHandle Empty { get; } = new([]);

        public bool IsEmpty => this.Targets.Count == 0;
    }

    private sealed record MutationLeaseTarget(MutationLeaseTargetKind Kind, Guid Id, string Path);

    private sealed record MutationExecutionContext(
        TContext Context,
        FileStorageDirectoryEntity RootDirectory,
        IReadOnlyList<MutationPath> Paths,
        DateTimeOffset Timestamp,
        string LeaseOwner)
    {
        public MutationPath PrimaryPath => this.GetPath(0);

        public MutationPath GetPath(int originalIndex) => this.Paths.Single(path => path.OriginalIndex == originalIndex);
    }

    private sealed record MutationPath(
        int OriginalIndex,
        string OriginalPath,
        string NormalizedPath,
        string ParentPath,
        string Name,
        string PathHash,
        string ParentPathHash);

    private enum MutationLeaseTargetKind
    {
        Directory = 0,
        File = 1
    }

    private sealed class MutationLeaseContentionException(string path)
        : InvalidOperationException($"Storage row lease contention detected at '{path}'.")
    {
        public string Path { get; } = path;
    }

    private sealed class BufferedContentLimitExceededException(long limit, long actualSize)
        : InvalidOperationException($"Buffered content exceeded the configured limit of {limit} bytes (actual: {actualSize} bytes).")
    {
        public long Limit { get; } = limit;

        public long ActualSize { get; } = actualSize;
    }
}
