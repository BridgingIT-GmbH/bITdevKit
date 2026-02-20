// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an error indicating that an operation precondition was not met.
/// </summary>
public class PreconditionError(string message) : ResultErrorBase(message)
{
}