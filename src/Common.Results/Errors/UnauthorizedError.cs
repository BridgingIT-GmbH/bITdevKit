// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Represents a request that lacks valid authentication.</summary>
/// <param name="message">The authentication-failure description, or <see langword="null"/> to use <c>Unauthorized</c>.</param>
public class UnauthorizedError(string message = null) : ResultErrorBase(message ?? "Unauthorized") // 401
{
    /// <summary>Initializes an unauthorized error with the default message.</summary>
    public UnauthorizedError() : this(null)
    {
    }
}
