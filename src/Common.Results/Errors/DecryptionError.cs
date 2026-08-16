// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an error related to decryption failures.
/// </summary>
public class DecryptionError(string message, string path = null, Exception innerException = null)
    : ResultErrorBase(message ?? "Decryption operation failed")
{
    /// <summary>Gets the encrypted resource path associated with the failure, when supplied.</summary>
    public string Path { get; } = path;

    /// <summary>Gets the exception that caused or describes the decryption failure, when available.</summary>
    public Exception InnerException { get; } = innerException;
}
