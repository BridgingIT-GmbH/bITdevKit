// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Represents a cancellation error in async operations.
/// </summary>
public class OperationCancelledError(string message = null) : ResultErrorBase(message ?? "Operation was cancelled")
{
    /// <summary>Initializes a cancellation error with the default message.</summary>
    public OperationCancelledError() : this(null)
    {
    }

    /// <summary>Throws an <see cref="OperationCanceledException"/> containing this error's message.</summary>
    /// <exception cref="OperationCanceledException">Always thrown to represent the recorded cancellation.</exception>
    public override void Throw()
    {
        throw new OperationCanceledException(this.Message);
    }
}
