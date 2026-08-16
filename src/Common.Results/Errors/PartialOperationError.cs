// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an error where only a partial set of operations succeeded, such as in bulk file operations.
/// </summary>
public class PartialOperationError(string message, IEnumerable<string> failedPaths, Exception innerException = null) : ResultErrorBase(message ?? "Partial operation failure")
{
    /// <summary>Gets the paths whose individual operations failed; an empty sequence is used when none were supplied.</summary>
    public IEnumerable<string> FailedPaths { get; } = failedPaths ?? [];

    /// <summary>Gets the exception that caused or describes the partial failure, when available.</summary>
    public Exception InnerException { get; } = innerException;
}
