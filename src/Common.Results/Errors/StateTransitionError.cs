// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an error that occurs when attempting an invalid state transition.
/// </summary>
public class StateTransitionError(string message = null) : ResultErrorBase(message ?? "Invalid state transition")
{
    public StateTransitionError() : this(null)
    {
    }
}