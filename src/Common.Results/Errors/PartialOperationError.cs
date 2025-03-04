// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an error where only a partial set of operations succeeded, such as in bulk file operations.
/// </summary>
public class PartialOperationError : ResultErrorBase
{
    public IEnumerable<string> FailedPaths { get; }
    public Exception InnerException { get; }

    public PartialOperationError(string message, IEnumerable<string> failedPaths, Exception innerException = null)
        : base(message ?? "Partial operation failure")
    {
        this.FailedPaths = failedPaths ?? Enumerable.Empty<string>();
        this.InnerException = innerException;
    }
}
