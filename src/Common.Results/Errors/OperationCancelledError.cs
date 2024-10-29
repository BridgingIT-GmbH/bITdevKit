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
    public OperationCancelledError() : this(null)
    {
    }
}