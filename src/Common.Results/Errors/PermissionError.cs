// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an error due to insufficient permissions for a file system operation.
/// </summary>
public class PermissionError : ResultErrorBase
{
    public string Path { get; }
    public Exception InnerException { get; }

    public PermissionError(string message, string path, Exception innerException = null)
        : base(message ?? "Permission denied")
    {
        this.Path = path;
        this.InnerException = innerException;
    }
}
