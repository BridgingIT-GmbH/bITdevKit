// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Creates uniquely named temporary files with explicit ownership.
/// </summary>
/// <example>
/// <code>
/// await using var temporary = TemporaryFileHelper.Create(prefix: "bdk-export-");
/// await source.CopyToAsync(temporary.Stream);
/// </code>
/// </example>
public static class TemporaryFileHelper
{
    /// <summary>
    /// Creates a temporary file lease.
    /// </summary>
    /// <param name="directoryPath">The directory to use, or the operating-system temporary directory.</param>
    /// <param name="prefix">The file-name prefix.</param>
    /// <param name="extension">The file-name extension.</param>
    /// <returns>A lease that owns the stream and file.</returns>
    /// <example>
    /// <code>
    /// await using var temporary = TemporaryFileHelper.Create(extension: ".bin");
    /// </code>
    /// </example>
    public static TemporaryFileLease Create(
        string directoryPath = null,
        string prefix = "bdk-",
        string extension = ".tmp")
    {
        directoryPath = string.IsNullOrWhiteSpace(directoryPath) ? Path.GetTempPath() : directoryPath;
        prefix = string.IsNullOrWhiteSpace(prefix) ? "bdk-" : prefix;
        extension ??= string.Empty;
        if (extension.Length > 0 && extension[0] != '.')
        {
            extension = $".{extension}";
        }

        Directory.CreateDirectory(directoryPath);
        var path = Path.Combine(directoryPath, $"{prefix}{Guid.NewGuid():N}{extension}");
        var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.Read,
            4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);

        return new TemporaryFileLease(path, stream);
    }
}
