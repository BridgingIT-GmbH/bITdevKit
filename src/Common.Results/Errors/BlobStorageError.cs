// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an error specific to cloud blob storage operations, such as Azure Blob Storage.
/// </summary>
public class BlobStorageError(string message, string path, int? statusCode = null, Exception innerException = null) : ResultErrorBase(message ?? "Blob storage operation failed")
{
    /// <summary>Gets the blob path associated with the failed operation.</summary>
    public string Path { get; } = path;

    /// <summary>Gets the provider status code, when one was reported.</summary>
    public int? StatusCode { get; } = statusCode;

    /// <summary>Gets the exception that caused or describes the storage failure, when available.</summary>
    public Exception InnerException { get; } = innerException;
}
