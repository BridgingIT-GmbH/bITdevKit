// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Windows.Storage;

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;

/// <summary>
/// A file storage provider that extends LocalFileStorageProvider to access network shares with Windows credentials
/// using minimal P/Invoke for token acquisition and RunImpersonated for execution.
/// </summary>
public class NetworkFileStorageProvider : LocalFileStorageProvider
{
    private readonly IWindowsImpersonationService impersonationService;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of NetworkFileStorageProvider with the specified network share path and impersonation service.
    /// </summary>
    /// <param name="locationName">The logical name of the storage location.</param>
    /// <param name="rootPath">The UNC path to the network share (e.g., \\server\share).</param>
    /// <param name="impersonationService">The Windows impersonation service to use.</param>
    public NetworkFileStorageProvider(string locationName, string rootPath, IWindowsImpersonationService impersonationService)
        : base(locationName, rootPath, false)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("NetworkFileStorageProvider is only supported on Windows.");
        }

        this.impersonationService = impersonationService ?? throw new ArgumentNullException(nameof(impersonationService));
    }

    /// <summary>
    /// Initializes a new instance of NetworkFileStorageProvider with the specified network share path and credentials.
    /// </summary>
    /// <param name="locationName">The logical name of the storage location.</param>
    /// <param name="rootPath">The UNC path to the network share (e.g., \\server\share).</param>
    /// <param name="username">The username for authentication (e.g., domain\username).</param>
    /// <param name="password">The password for authentication.</param>
    /// <param name="domain">The domain for authentication (optional, defaults to the local machine).</param>
    public NetworkFileStorageProvider(string locationName, string rootPath, string username = null, string password = null, string domain = null)
        : this(locationName, rootPath, new WindowsImpersonationService(username, password, domain))
    {
    }

    public override async Task<Result<Stream>> ReadFileAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        return await this.impersonationService.ExecuteImpersonatedAsync(() => base.ReadFileAsync(path, progress, cancellationToken));
    }

    public override async Task<Result> WriteFileAsync(string path, Stream content, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        return await this.impersonationService.ExecuteImpersonatedAsync(() => base.WriteFileAsync(path, content, progress, cancellationToken));
    }

    public override async Task<Result> DeleteFileAsync(string path, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        return await this.impersonationService.ExecuteImpersonatedAsync(() => base.DeleteFileAsync(path, progress, cancellationToken));
    }

    public override async Task<Result<string>> GetChecksumAsync(string path, CancellationToken cancellationToken = default)
    {
        return await this.impersonationService.ExecuteImpersonatedAsync(() => base.GetChecksumAsync(path, cancellationToken));
    }

    public override async Task<Result<FileMetadata>> GetFileMetadataAsync(string path, CancellationToken cancellationToken = default)
    {
        return await this.impersonationService.ExecuteImpersonatedAsync(() => base.GetFileMetadataAsync(path, cancellationToken));
    }

    public override async Task<Result> SetFileMetadataAsync(string path, FileMetadata metadata, CancellationToken cancellationToken = default)
    {
        return await this.impersonationService.ExecuteImpersonatedAsync(() => base.SetFileMetadataAsync(path, metadata, cancellationToken));
    }

    public override async Task<Result<FileMetadata>> UpdateFileMetadataAsync(string path, Func<FileMetadata, FileMetadata> metadataUpdate, CancellationToken cancellationToken = default)
    {
        return await this.impersonationService.ExecuteImpersonatedAsync(() => base.UpdateFileMetadataAsync(path, metadataUpdate, cancellationToken));
    }

    public override async Task<Result<(IEnumerable<string> Files, string NextContinuationToken)>> ListFilesAsync(string path, string searchPattern = null, bool recursive = false, string continuationToken = null, CancellationToken cancellationToken = default)
    {
        return await this.impersonationService.ExecuteImpersonatedAsync(() => base.ListFilesAsync(path, searchPattern, recursive, continuationToken, cancellationToken));
    }

    public override async Task<Result> CopyFileAsync(string sourcePath, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        return await this.impersonationService.ExecuteImpersonatedAsync(() => base.CopyFileAsync(sourcePath, destinationPath, progress, cancellationToken));
    }

    public override async Task<Result> RenameFileAsync(string oldPath, string newPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        return await this.impersonationService.ExecuteImpersonatedAsync(() => base.RenameFileAsync(oldPath, newPath, progress, cancellationToken));
    }

    public override async Task<Result> MoveFileAsync(string sourcePath, string destinationPath, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        return await this.impersonationService.ExecuteImpersonatedAsync(() => base.MoveFileAsync(sourcePath, destinationPath, progress, cancellationToken));
    }

    public override async Task<Result> CopyFilesAsync(IEnumerable<(string SourcePath, string DestinationPath)> filePairs, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        return await this.impersonationService.ExecuteImpersonatedAsync(() => base.CopyFilesAsync(filePairs, progress, cancellationToken));
    }

    public override async Task<Result> MoveFilesAsync(IEnumerable<(string SourcePath, string DestinationPath)> filePairs, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        return await this.impersonationService.ExecuteImpersonatedAsync(() => base.MoveFilesAsync(filePairs, progress, cancellationToken));
    }

    public override async Task<Result> DeleteFilesAsync(IEnumerable<string> paths, IProgress<FileProgress> progress = null, CancellationToken cancellationToken = default)
    {
        return await this.impersonationService.ExecuteImpersonatedAsync(() => base.DeleteFilesAsync(paths, progress, cancellationToken));
    }

    public override async Task<Result> IsDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        return await this.impersonationService.ExecuteImpersonatedAsync(() => base.IsDirectoryAsync(path, cancellationToken));
    }

    public override async Task<Result> CreateDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        return await this.impersonationService.ExecuteImpersonatedAsync(() => base.CreateDirectoryAsync(path, cancellationToken));
    }

    public override async Task<Result> DeleteDirectoryAsync(string path, bool recursive, CancellationToken cancellationToken = default)
    {
        return await this.impersonationService.ExecuteImpersonatedAsync(() => base.DeleteDirectoryAsync(path, recursive, cancellationToken));
    }

    public override async Task<Result<IEnumerable<string>>> ListDirectoriesAsync(string path, string searchPattern = null, bool recursive = false, CancellationToken cancellationToken = default)
    {
        return await this.impersonationService.ExecuteImpersonatedAsync(() => base.ListDirectoriesAsync(path, searchPattern, recursive, cancellationToken));
    }

    public override async Task<Result> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        return await this.impersonationService.ExecuteImpersonatedAsync(() => base.CheckHealthAsync(cancellationToken));
    }

    /// <summary>
    /// Disposes the resources used by the provider.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (this.disposed)
        {
            return;
        }

        if (disposing)
        {
            this.impersonationService.Dispose();
            base.Dispose(disposing);
        }

        this.disposed = true;
    }
}
