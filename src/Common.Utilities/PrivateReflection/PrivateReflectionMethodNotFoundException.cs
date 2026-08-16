// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.PrivateReflection;

/// <summary>
///     Represents failure to locate a dynamically invoked private method.
/// </summary>
public class PrivateReflectionMethodNotFoundException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="PrivateReflectionMethodNotFoundException"/> class.</summary>
    public PrivateReflectionMethodNotFoundException() { }

    /// <summary>Initializes a new instance with a specified error message.</summary>
    /// <param name="message">The error message.</param>
    public PrivateReflectionMethodNotFoundException(string message)
        : base(message) { }
}
