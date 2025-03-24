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
    /// Retrieves the parent path from the current path by removing the last segment after the final slash. Returns null
    /// if the path is empty or does not contain a slash.
    /// </summary>
    public string GetParentPath()
    {
        if (string.IsNullOrEmpty(this.Path))
        {
            return null;
        }

        var lastSlashIndex = this.Path.LastIndexOf('/');
        if (lastSlashIndex < 0)
        {
            return null;
        }

        return this.Path[..lastSlashIndex];
    }

    /// <summary>
    /// Retrieves the name of the file or directory from the given path. If the path is null or empty, it returns null.
    /// </summary>
    public string GetFileName()
    {
        if (string.IsNullOrEmpty(this.Path))
        {
            return null;
        }

        var lastSlashIndex = this.Path.LastIndexOf('/');
        if (lastSlashIndex < 0)
        {
            return this.Path;
        }

        return this.Path[(lastSlashIndex + 1)..];
    }

    /// <summary>
    /// Retrieves the file extension from the Path property. Returns null if the Path is empty or does not contain a dot.
    /// </summary>
    /// <returns>The file extension as a string, or null if no extension exists.</returns>
    public string GetFileExtension()
    {
        if (string.IsNullOrEmpty(this.Path))
        {
            return null;
        }

        var lastDotIndex = this.Path.LastIndexOf('.');
        if (lastDotIndex < 0)
        {
            return null;
        }

        return this.Path[(lastDotIndex + 1)..];
    }
}