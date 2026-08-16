// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Represents an authenticated request that is not authorized to perform an operation.</summary>
/// <param name="message">The authorization-failure description, or <see langword="null"/> to use <c>Forbidden</c>.</param>
public class ForbiddenError(string message = null) : ResultErrorBase(message ?? "Forbidden") // 403
{
    /// <summary>Initializes a forbidden error with the default message.</summary>
    public ForbiddenError() : this(null) { }
}
