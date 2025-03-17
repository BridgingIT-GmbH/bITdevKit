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
    public string Path { get; } = path;
    public int? StatusCode { get; } = statusCode;
    public Exception InnerException { get; } = innerException;
}