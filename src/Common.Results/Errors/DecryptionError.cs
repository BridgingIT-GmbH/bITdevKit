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
    public string Path { get; } = path;
    public Exception InnerException { get; } = innerException;
}