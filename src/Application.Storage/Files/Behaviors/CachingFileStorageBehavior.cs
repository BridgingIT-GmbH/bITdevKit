// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Caching.Memory;

/// <summary>
/// A behavior that caches file storage operations, reducing repeated I/O for read-heavy scenarios.
/// </summary>
public class CachingFileStorageBehavior(IFileStorageProvider innerProvider, IMemoryCache cache, CachingOptions options)
    : IFileStorageBehavior
{
    private readonly IMemoryCache cache = cache ?? throw new ArgumentNullException(nameof(cache));
    private readonly CachingOptions options = options ?? new CachingOptions();

    public IFileStorageProvider InnerProvider { get; } = innerProvider ?? throw new ArgumentNullException(nameof(innerProvider));

    public string LocationName => this.InnerProvider.LocationName;

    public string Description => this.InnerProvider.Description;

    public bool SupportsNotifications => this.InnerProvider.SupportsNotifications;

    public async Task<Result> FileExistsAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"exists_{path}";
        if (this.cache.TryGetValue(cacheKey, out bool cachedResult))
        {
            return Result.Success()
                .WithMessage($"Cached existence check for file at '{path}'");
        }

        var result = await this.InnerProvider.FileExistsAsync(path, progress, cancellationToken);
        if (result.IsSuccess)
        {
            this.cache.Set(cacheKey, true, this.options.CacheDuration);
        }
        return result;
    }

    public async Task<Result<Stream>> ReadFileAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"read_{path}";
        if (this.cache.TryGetValue(cacheKey, out Stream cachedStream))
        {
            cachedStream.Position = 0; // Reset stream position for reuse
            return Result<Stream>.Success(cachedStream)
                .WithMessage($"Cached read for file at '{path}'");
        }

        var result = await this.InnerProvider.ReadFileAsync(path, progress, cancellationToken);
        if (result.IsSuccess)
        {
            var memoryStream = new MemoryStream();
            await result.Value.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;
            this.cache.Set(cacheKey, memoryStream, this.options.CacheDuration);
            return Result<Stream>.Success(memoryStream)
                .WithMessage($"Read and cached file at '{path}'");
        }
        return result;
    }

    // Implement other methods similarly, caching where appropriate (e.g., GetChecksumAsync, GetFileInfoAsync)
    // Non-cached methods (e.g., WriteFileAsync, DeleteFileAsync) delegate directly to innerProvider
    public async Task<Result> WriteFileAsync(string path, Stream content, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        var result = await this.InnerProvider.WriteFileAsync(path, content, progress, cancellationToken);
        if (result.IsSuccess)
        {
            this.cache.Remove($"exists_{path}");
            this.cache.Remove($"read_{path}");
            this.cache.Remove($"checksum_{path}");
            this.cache.Remove($"info_{path}");
        }
        return result;
    }

    public async Task<Result> DeleteFileAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        var result = await this.InnerProvider.DeleteFileAsync(path, progress, cancellationToken);
        if (result.IsSuccess)
        {
            this.cache.Remove($"exists_{path}");
            this.cache.Remove($"read_{path}");
            this.cache.Remove($"checksum_{path}");
            this.cache.Remove($"info_{path}");
        }
        return result;
    }

    public async Task<Result<string>> GetChecksumAsync(string path, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"checksum_{path}";
        if (this.cache.TryGetValue(cacheKey, out string cachedChecksum))
        {
            return Result<string>.Success(cachedChecksum)
                .WithMessage($"Cached checksum for file at '{path}'");
        }

        var result = await this.InnerProvider.GetChecksumAsync(path, cancellationToken);
        if (result.IsSuccess)
        {
            this.cache.Set(cacheKey, result.Value, this.options.CacheDuration);
        }
        return result;
    }

    public async Task<Result<FileMetadata>> GetFileMetadataAsync(string path, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"info_{path}";
        if (this.cache.TryGetValue(cacheKey, out FileMetadata cachedMetadata))
        {
            return Result<FileMetadata>.Success(cachedMetadata)
                .WithMessage($"Cached metadata for file at '{path}'");
        }

        var result = await this.InnerProvider.GetFileMetadataAsync(path, cancellationToken);
        if (result.IsSuccess)
        {
            this.cache.Set(cacheKey, result.Value, this.options.CacheDuration);
        }
        return result;
    }

    // ... Implement remaining methods (SetFileMetadataAsync, UpdateFileMetadataAsync, ListFilesAsync, CopyFileAsync, etc.)
    // Delegate non-caching operations to innerProvider, clearing cache as needed
    public async Task<Result> SetFileMetadataAsync(string path, FileMetadata metadata, CancellationToken cancellationToken = default)
    {
        var result = await this.InnerProvider.SetFileMetadataAsync(path, metadata, cancellationToken);
        if (result.IsSuccess)
        {
            this.cache.Remove($"info_{path}");
        }
        return result;
    }

    public async Task<Result<FileMetadata>> UpdateFileMetadataAsync(string path, Func<FileMetadata, FileMetadata> metadataUpdate, CancellationToken cancellationToken = default)
    {
        var result = await this.InnerProvider.UpdateFileMetadataAsync(path, metadataUpdate, cancellationToken);
        if (result.IsSuccess)
        {
            this.cache.Remove($"info_{path}");
        }
        return result;
    }

    public async Task<Result<(IEnumerable<string> Files, string NextContinuationToken)>> ListFilesAsync(
        string path, string searchPattern = null, bool recursive = false, string continuationToken = null, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"list_{path}_{searchPattern}_{recursive}_{continuationToken}";
        if (this.cache.TryGetValue(cacheKey, out (IEnumerable<string> Files, string NextContinuationToken) cachedResult))
        {
            return Result<(IEnumerable<string> Files, string NextContinuationToken)>.Success(cachedResult)
                .WithMessage($"Cached file listing for '{path}'");
        }

        var result = await this.InnerProvider.ListFilesAsync(path, searchPattern, recursive, continuationToken, cancellationToken);
        if (result.IsSuccess)
        {
            this.cache.Set(cacheKey, result.Value, this.options.CacheDuration);
        }
        return result;
    }

    public async Task<Result> CopyFileAsync(string sourcePath, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        var result = await this.InnerProvider.CopyFileAsync(sourcePath, destinationPath, progress, cancellationToken);
        if (result.IsSuccess)
        {
            this.cache.Remove($"exists_{sourcePath}");
            this.cache.Remove($"read_{sourcePath}");
            this.cache.Remove($"checksum_{sourcePath}");
            this.cache.Remove($"info_{sourcePath}");
            this.cache.Remove($"exists_{destinationPath}");
            this.cache.Remove($"read_{destinationPath}");
            this.cache.Remove($"checksum_{destinationPath}");
            this.cache.Remove($"info_{destinationPath}");
        }
        return result;
    }

    public async Task<Result> RenameFileAsync(string oldPath, string newPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        var result = await this.InnerProvider.RenameFileAsync(oldPath, newPath, progress, cancellationToken);
        if (result.IsSuccess)
        {
            this.cache.Remove($"exists_{oldPath}");
            this.cache.Remove($"read_{oldPath}");
            this.cache.Remove($"checksum_{oldPath}");
            this.cache.Remove($"info_{oldPath}");
            this.cache.Remove($"exists_{newPath}");
            this.cache.Remove($"read_{newPath}");
            this.cache.Remove($"checksum_{newPath}");
            this.cache.Remove($"info_{newPath}");
        }
        return result;
    }

    public async Task<Result> MoveFileAsync(string sourcePath, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        var result = await this.InnerProvider.MoveFileAsync(sourcePath, destinationPath, progress, cancellationToken);
        if (result.IsSuccess)
        {
            this.cache.Remove($"exists_{sourcePath}");
            this.cache.Remove($"read_{sourcePath}");
            this.cache.Remove($"checksum_{sourcePath}");
            this.cache.Remove($"info_{sourcePath}");
            this.cache.Remove($"exists_{destinationPath}");
            this.cache.Remove($"read_{destinationPath}");
            this.cache.Remove($"checksum_{destinationPath}");
            this.cache.Remove($"info_{destinationPath}");
        }
        return result;
    }

    public async Task<Result> CopyFilesAsync(IEnumerable<(string SourcePath, string DestinationPath)> filePairs, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        var result = await this.InnerProvider.CopyFilesAsync(filePairs, progress, cancellationToken);
        if (result.IsSuccess)
        {
            foreach (var (source, dest) in filePairs)
            {
                this.cache.Remove($"exists_{source}");
                this.cache.Remove($"read_{source}");
                this.cache.Remove($"checksum_{source}");
                this.cache.Remove($"info_{source}");
                this.cache.Remove($"exists_{dest}");
                this.cache.Remove($"read_{dest}");
                this.cache.Remove($"checksum_{dest}");
                this.cache.Remove($"info_{dest}");
            }
        }
        return result;
    }

    public async Task<Result> MoveFilesAsync(IEnumerable<(string SourcePath, string DestinationPath)> filePairs, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        var result = await this.InnerProvider.MoveFilesAsync(filePairs, progress, cancellationToken);
        if (result.IsSuccess)
        {
            foreach (var (source, dest) in filePairs)
            {
                this.cache.Remove($"exists_{source}");
                this.cache.Remove($"read_{source}");
                this.cache.Remove($"checksum_{source}");
                this.cache.Remove($"info_{source}");
                this.cache.Remove($"exists_{dest}");
                this.cache.Remove($"read_{dest}");
                this.cache.Remove($"checksum_{dest}");
                this.cache.Remove($"info_{dest}");
            }
        }
        return result;
    }

    public async Task<Result> DeleteFilesAsync(IEnumerable<string> paths, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        var result = await this.InnerProvider.DeleteFilesAsync(paths, progress, cancellationToken);
        if (result.IsSuccess)
        {
            foreach (var path in paths)
            {
                this.cache.Remove($"exists_{path}");
                this.cache.Remove($"read_{path}");
                this.cache.Remove($"checksum_{path}");
                this.cache.Remove($"info_{path}");
            }
        }
        return result;
    }

    public async Task<Result> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"isdir_{path}";
        if (this.cache.TryGetValue(cacheKey, out bool cachedResult))
        {
            return Result.Success()
                .WithMessage($"Cached existence check for directory at '{path}'");
        }

        var result = await this.InnerProvider.DirectoryExistsAsync(path, cancellationToken);
        if (result.IsSuccess)
        {
            this.cache.Set(cacheKey, true, this.options.CacheDuration);
        }
        return result;
    }

    public async Task<Result> CreateDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        var result = await this.InnerProvider.CreateDirectoryAsync(path, cancellationToken);
        if (result.IsSuccess)
        {
            this.cache.Remove($"isdir_{path}");
        }
        return result;
    }

    public async Task<Result> DeleteDirectoryAsync(string path, bool recursive, CancellationToken cancellationToken = default)
    {
        var result = await this.InnerProvider.DeleteDirectoryAsync(path, recursive, cancellationToken);
        if (result.IsSuccess)
        {
            this.cache.Remove($"isdir_{path}");
        }
        return result;
    }

    public async Task<Result<IEnumerable<string>>> ListDirectoriesAsync(
        string path, string searchPattern = null, bool recursive = false, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"listdirs_{path}_{searchPattern}_{recursive}";
        if (this.cache.TryGetValue(cacheKey, out IEnumerable<string> cachedDirectories))
        {
            return Result<IEnumerable<string>>.Success(cachedDirectories)
                .WithMessage($"Cached directory listing for '{path}'");
        }

        var result = await this.InnerProvider.ListDirectoriesAsync(path, searchPattern, recursive, cancellationToken);
        if (result.IsSuccess)
        {
            this.cache.Set(cacheKey, result.Value, this.options.CacheDuration);
        }
        return result;
    }

    public async Task<Result> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        return await this.InnerProvider.CheckHealthAsync(cancellationToken);
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Configuration options for caching behavior.
/// </summary>
public class CachingOptions
{
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(10);
}