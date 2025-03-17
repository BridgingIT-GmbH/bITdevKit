// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an error due to insufficient permissions for a file system operation.
/// </summary>
public class PermissionError(string message, string path, Exception innerException = null) : ResultErrorBase(message ?? "Permission denied")
{
    public string Path { get; } = path;
    public Exception InnerException { get; } = innerException;
}
