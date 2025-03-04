// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an error specific to cloud blob storage operations, such as Azure Blob Storage.
/// </summary>
public class BlobStorageError : ResultErrorBase
{
    public string Path { get; }
    public int? StatusCode { get; }
    public Exception InnerException { get; }

    public BlobStorageError(string message, string path, int? statusCode = null, Exception innerException = null)
        : base(message ?? "Blob storage operation failed")
    {
        this.Path = path;
        this.StatusCode = statusCode;
        this.InnerException = innerException;
    }
}