// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Describes a completed transfer between blob storage and file storage.
/// </summary>
/// <example>
/// <code>
/// var filePath = transfer.FilePath;
/// var blobLength = transfer.Blob.Length;
/// </code>
/// </example>
public sealed class BlobFileTransferInfo
{
    /// <summary>
    /// Gets the blob metadata associated with the transfer.
    /// </summary>
    /// <example>
    /// <code>
    /// var blobKey = transfer.Blob.Key;
    /// </code>
    /// </example>
    public BlobInfo Blob { get; init; }

    /// <summary>
    /// Gets the file provider path used by the transfer.
    /// </summary>
    /// <example>
    /// <code>
    /// var path = transfer.FilePath;
    /// </code>
    /// </example>
    public string FilePath { get; init; }

    /// <summary>
    /// Gets the number of bytes transferred.
    /// </summary>
    /// <example>
    /// <code>
    /// var bytes = transfer.BytesTransferred;
    /// </code>
    /// </example>
    public long BytesTransferred { get; init; }
}
