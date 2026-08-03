// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System.Collections;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using BridgingIT.DevKit.Common;

/// <summary>
/// Emits low-cardinality file-storage operation metrics while preserving provider semantics.
/// </summary>
/// <example>
/// <code>
/// services.AddFileStorage(factory => factory
///     .RegisterProvider("documents", storage => storage
///         .UseLocal("Documents", rootPath)
///         .WithMetrics()));
/// </code>
/// </example>
/// <remarks>
/// Initializes a new instance of the <see cref="MetricsFileStorageBehavior" /> class.
/// </remarks>
/// <param name="innerProvider">The decorated file-storage provider.</param>
/// <param name="meterFactory">The optional meter factory used to emit measurements.</param>
/// <example>
/// <code>
/// var behavior = new MetricsFileStorageBehavior(provider, meterFactory);
/// </code>
/// </example>
public sealed class MetricsFileStorageBehavior(
    IFileStorageProvider innerProvider,
    IMeterFactory meterFactory = null) : IFileStorageBehavior
{
    private readonly string location = Metrics.NormalizePart(innerProvider?.LocationName);
    private readonly string provider = Metrics.NormalizeTypeName(innerProvider?.GetType() ?? typeof(IFileStorageProvider));

    /// <inheritdoc />
    public IFileStorageProvider InnerProvider { get; } = innerProvider ?? throw new ArgumentNullException(nameof(innerProvider));

    /// <inheritdoc />
    public string LocationName => this.InnerProvider.LocationName;

    /// <inheritdoc />
    public string Description => this.InnerProvider.Description;

    /// <inheritdoc />
    public bool SupportsNotifications => this.InnerProvider.SupportsNotifications;

    /// <inheritdoc />
    public Task<Result> FileExistsAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync("exists", () => this.InnerProvider.FileExistsAsync(path, progress, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<Result<Stream>> ReadFileAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(
            "read",
            () => this.InnerProvider.ReadFileAsync(path, progress, cancellationToken),
            cancellationToken,
            bytes: result => result is { IsSuccess: true } ? TryGetLength(result.Value) : 0);

    /// <inheritdoc />
    public Task<Result> RenameDirectoryAsync(string path, string destinationPath, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync("rename-directory", () => this.InnerProvider.RenameDirectoryAsync(path, destinationPath, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<Result> WriteFileAsync(string path, Stream content, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(
            "write",
            () => this.InnerProvider.WriteFileAsync(path, content, progress, cancellationToken),
            cancellationToken,
            bytes: TryGetLength(content));

    /// <inheritdoc />
    public Task<Result<Stream>> OpenWriteFileAsync(string path, bool useTemporaryWrite = false, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync("open-write", () => this.InnerProvider.OpenWriteFileAsync(path, useTemporaryWrite, progress, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<Result> DeleteFileAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync("delete", () => this.InnerProvider.DeleteFileAsync(path, progress, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<Result<string>> GetChecksumAsync(string path, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync("checksum", () => this.InnerProvider.GetChecksumAsync(path, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<Result<FileMetadata>> GetFileMetadataAsync(string path, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync("metadata-get", () => this.InnerProvider.GetFileMetadataAsync(path, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<Result> SetFileMetadataAsync(string path, FileMetadata metadata, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync("metadata-set", () => this.InnerProvider.SetFileMetadataAsync(path, metadata, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<Result<FileMetadata>> UpdateFileMetadataAsync(string path, Func<FileMetadata, FileMetadata> metadataUpdate, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync("metadata-update", () => this.InnerProvider.UpdateFileMetadataAsync(path, metadataUpdate, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<Result<(IEnumerable<string> Files, string NextContinuationToken)>> ListFilesAsync(
        string path,
        string searchPattern = null,
        bool recursive = false,
        string continuationToken = null,
        CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(
            "list-files",
            () => this.InnerProvider.ListFilesAsync(path, searchPattern, recursive, continuationToken, cancellationToken),
            cancellationToken,
            itemCount: result => result is { IsSuccess: true } ? TryGetCount(result.Value.Files) : 0);

    /// <inheritdoc />
    public Task<Result> CopyFileAsync(string path, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync("copy", () => this.InnerProvider.CopyFileAsync(path, destinationPath, progress, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<Result> RenameFileAsync(string path, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync("rename", () => this.InnerProvider.RenameFileAsync(path, destinationPath, progress, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<Result> MoveFileAsync(string path, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync("move", () => this.InnerProvider.MoveFileAsync(path, destinationPath, progress, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<Result> CopyFilesAsync(IEnumerable<(string SourcePath, string DestinationPath)> filePairs, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync("copy-batch", () => this.InnerProvider.CopyFilesAsync(filePairs, progress, cancellationToken), cancellationToken, itemCount: TryGetCount(filePairs));

    /// <inheritdoc />
    public Task<Result> MoveFilesAsync(IEnumerable<(string SourcePath, string DestinationPath)> filePairs, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync("move-batch", () => this.InnerProvider.MoveFilesAsync(filePairs, progress, cancellationToken), cancellationToken, itemCount: TryGetCount(filePairs));

    /// <inheritdoc />
    public Task<Result> DeleteFilesAsync(IEnumerable<string> paths, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync("delete-batch", () => this.InnerProvider.DeleteFilesAsync(paths, progress, cancellationToken), cancellationToken, itemCount: TryGetCount(paths));

    /// <inheritdoc />
    public Task<Result> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync("directory-exists", () => this.InnerProvider.DirectoryExistsAsync(path, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<Result> CreateDirectoryAsync(string path, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync("directory-create", () => this.InnerProvider.CreateDirectoryAsync(path, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<Result> DeleteDirectoryAsync(string path, bool recursive, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync("directory-delete", () => this.InnerProvider.DeleteDirectoryAsync(path, recursive, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public Task<Result<IEnumerable<string>>> ListDirectoriesAsync(string path, string searchPattern = null, bool recursive = false, CancellationToken cancellationToken = default) =>
        this.ExecuteAsync(
            "list-directories",
            () => this.InnerProvider.ListDirectoriesAsync(path, searchPattern, recursive, cancellationToken),
            cancellationToken,
            itemCount: result => result is { IsSuccess: true } ? TryGetCount(result.Value) : 0);

    /// <inheritdoc />
    public Task<Result> CheckHealthAsync(CancellationToken cancellationToken = default) =>
        this.ExecuteAsync("health", () => this.InnerProvider.CheckHealthAsync(cancellationToken), cancellationToken);

    private async Task<Result<T>> ExecuteAsync<T>(
        string operation,
        Func<Task<Result<T>>> next,
        CancellationToken cancellationToken,
        Func<Result<T>, long> bytes = null,
        Func<Result<T>, long> itemCount = null)
    {
        if (meterFactory is null || cancellationToken.IsCancellationRequested)
        {
            return await next().ConfigureAwait(false);
        }

        var started = Stopwatch.GetTimestamp();
        this.AddCounter("filestorage_operations", 1, operation);

        var result = await next().ConfigureAwait(false);
        this.Record(operation, started, result, bytes?.Invoke(result) ?? 0, itemCount?.Invoke(result) ?? 0);

        return result;
    }

    private async Task<Result<T>> ExecuteAsync<T>(
        string operation,
        Func<Task<Result<T>>> next,
        CancellationToken cancellationToken,
        long bytes)
    {
        if (meterFactory is null || cancellationToken.IsCancellationRequested)
        {
            return await next().ConfigureAwait(false);
        }

        var started = Stopwatch.GetTimestamp();
        this.AddCounter("filestorage_operations", 1, operation);

        var result = await next().ConfigureAwait(false);
        this.Record(operation, started, result, result.IsSuccess ? bytes : 0, itemCount: 0);

        return result;
    }

    private async Task<Result> ExecuteAsync(
        string operation,
        Func<Task<Result>> next,
        CancellationToken cancellationToken,
        long bytes = 0,
        long itemCount = 0)
    {
        if (meterFactory is null || cancellationToken.IsCancellationRequested)
        {
            return await next().ConfigureAwait(false);
        }

        var started = Stopwatch.GetTimestamp();
        this.AddCounter("filestorage_operations", 1, operation);

        var result = await next().ConfigureAwait(false);
        this.Record(operation, started, result, result.IsSuccess ? bytes : 0, result.IsSuccess ? itemCount : 0);

        return result;
    }

    private void Record(string operation, long started, IResult result, long bytes, long itemCount)
    {
        this.AddHistogram("filestorage_operation_duration", Stopwatch.GetElapsedTime(started).TotalMilliseconds, operation);

        if (result.IsFailure)
        {
            this.AddCounter("filestorage_operation_failures", 1, operation);
        }

        if (bytes > 0)
        {
            this.AddCounter("filestorage_bytes", bytes, operation);
        }

        if (itemCount > 0)
        {
            this.AddCounter("filestorage_items", itemCount, operation);
        }
    }

    private void AddCounter(string name, long value, string operation)
    {
        meterFactory
            .Create(Metrics.MeterName)
            .CreateCounter<long>(name)
            .Add(value, this.Tags(operation));
    }

    private void AddHistogram(string name, double value, string operation)
    {
        meterFactory
            .Create(Metrics.MeterName)
            .CreateHistogram<double>(name, unit: "ms")
            .Record(value, this.Tags(operation));
    }

    private KeyValuePair<string, object>[] Tags(string operation) =>
    [
        new("operation", operation),
        new("location", this.location),
        new("provider", this.provider)
    ];

    private static long TryGetLength(Stream stream)
    {
        try
        {
            return stream is { CanSeek: true } ? stream.Length : 0;
        }
        catch (NotSupportedException)
        {
            return 0;
        }
        catch (ObjectDisposedException)
        {
            return 0;
        }
    }

    private static long TryGetCount<T>(IEnumerable<T> items) =>
        items switch
        {
            null => 0,
            ICollection<T> collection => collection.Count,
            IReadOnlyCollection<T> collection => collection.Count,
            ICollection collection => collection.Count,
            _ => 0
        };
}
