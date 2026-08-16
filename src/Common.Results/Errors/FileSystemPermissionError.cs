// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an error due to insufficient permissions for a file system operation.
/// </summary>
public class FileSystemPermissionError(string message, string path, Exception innerException = null) : ResultErrorBase(message ?? "Permission denied")
{
    /// <summary>Gets the file-system path for which permission was denied.</summary>
    public string Path { get; } = path;

    /// <summary>Gets the message from the supplied exception, when available.</summary>
    public string Details { get; } = innerException?.Message;
}
