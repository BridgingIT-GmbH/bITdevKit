// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Represents an error associated with an invalid or unusable argument.</summary>
/// <param name="argument">The argument description or name; when omitted, the base error message is <c>Argument error</c>.</param>
public class ArgumentError(string argument = null) : ResultErrorBase(argument ?? "Argument error")
{
    /// <summary>Gets the supplied argument description or name.</summary>
    public string Argument { get; } = argument;
}
