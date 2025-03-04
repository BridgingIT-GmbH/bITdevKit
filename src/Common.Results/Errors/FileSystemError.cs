// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an error related to file system operations, such as file not found or disk issues.
/// </summary>
public class FileSystemError : ResultErrorBase
{
    public string Path { get; }
    public Exception InnerException { get; }

    public FileSystemError(string message, string path = null, Exception innerException = null)
        : base(message ?? "File system operation failed")
    {
        this.Path = path;
        this.InnerException = innerException;
    }
}

public class ArgumentError : ResultErrorBase
{
    public string Argument { get; }

    public ArgumentError(string argument = null, Exception innerException = null)
        : base(argument ?? "Argument error")
    {
        this.Argument = argument;
    }
}