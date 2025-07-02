// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Represents progress information for file operations, tracking bytes and file counts.
/// Used with IProgress<T> to report progress in IFileStorageProvider methods.
/// </summary>
public readonly struct FileProgress
{
    /// <summary>
    /// Gets the total bytes processed during the operation.
    /// Example: `progress.Report(new FileProgress { BytesProcessed = 1024 });`
    /// </summary>
    public long BytesProcessed { get; init; }

    /// <summary>
    /// Gets the number of files processed in a multi-file operation.
    /// Example: `progress.Report(new FileProgress { FilesProcessed = 5, TotalFiles = 10 });`
    /// </summary>
    public long FilesProcessed { get; init; }

    /// <summary>
    /// Gets the total number of files expected in a multi-file operation, if known.
    /// Example: `progress.Report(new FileProgress { FilesProcessed = 3, TotalFiles = 8 });`
    /// </summary>
    public long TotalFiles { get; init; }
}
