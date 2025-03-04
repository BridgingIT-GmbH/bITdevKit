// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System.Diagnostics;

/// <summary>
/// Represents metadata for a file or blob, used in IFileStorageProvider operations.
/// Supports properties like path, size, and last modification time.
/// </summary>
[DebuggerDisplay("Path={Path}")]
public class FileMetadata
{
    /// <summary>
    /// Gets or sets the relative path of the file or blob.
    /// Example: `var metadata = new FileMetadata { Path = "folder/file.txt" };`
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// Gets or sets the length of the file or blob in bytes.
    /// Example: `var metadata = new FileMetadata { Length = 1024 };`
    /// </summary>
    public long Length { get; set; }

    /// <summary>
    /// Gets or sets the last modification time of the file or blob in UTC.
    /// Example: `var metadata = new FileMetadata { LastModified = DateTime.UtcNow };`
    /// </summary>
    public DateTime? LastModified { get; set; }

    /// <summary>
    /// Creates a copy of the current FileMetadata with updated values.
    /// Example: `var updated = metadata.WithLength(2048);`
    /// </summary>
    /// <param name="update">A function to update the metadata properties.</param>
    /// <returns>A new FileMetadata instance with updated values.</returns>
    public FileMetadata With(Func<FileMetadata, FileMetadata> update)
    {
        return update?.Invoke(this ?? new FileMetadata()) ?? this;
    }
}