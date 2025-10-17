// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an error related to file system operations, such as file not found or disk issues.
/// </summary>
public class FileSystemError(string message, string path = null, Exception innerException = null) : ResultErrorBase(message ?? "File system operation failed")
{
    public string Path { get; } = path;
    public Exception InnerException { get; } = innerException;
}
