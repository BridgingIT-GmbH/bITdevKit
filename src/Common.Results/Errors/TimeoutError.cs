// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Represents a timeout error in async operations.
/// </summary>
public class TimeoutError(string message) : ResultErrorBase(message ?? "Timeout")
{
    public TimeoutError() : this(null)
    {
    }
}