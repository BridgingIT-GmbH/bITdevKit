// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an access denied error for resource access.
/// </summary>
public class AccessDeniedError(string message = null) : ResultErrorBase(message ?? "Access denied")
{
    public AccessDeniedError() : this(null)
    {
    }
}