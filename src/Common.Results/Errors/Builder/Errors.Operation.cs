// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public static partial class Errors
{
    public static partial class Operation
    {
        /// <summary>Creates a <see cref="OperationCancelledError"/> for operation cancellation scenarios.</summary>
        public static OperationCancelledError OperationCancelled(string message = null)
            => new(message);

        /// <summary>Creates a <see cref="TimeoutError"/> for operation timeout scenarios.</summary>
        public static TimeoutError Timeout(string message = null)
            => new(message);

        /// <summary>Creates an <see cref="OperationError"/> for general operation lifecycle and control errors.</summary>
        public static OperationError Error(string message = null)
            => new(message);
    }
}

/// <summary>
/// Represents a general operation lifecycle or control error.
/// </summary>
/// <param name="message">The error message that describes the operation error. If null, a default message is used.</param>
public class OperationError(string message = null) : ResultErrorBase(message ?? "Operation error")
{
    public OperationError() : this(null)
    {
    }
}
