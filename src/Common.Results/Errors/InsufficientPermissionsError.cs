// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Represents an error indicating insufficient permissions for an operation.
/// </summary>
public class InsufficientPermissionsError(string message = null, string requiredPermission = null)
    : ResultErrorBase(message ?? "Insufficient permissions")
{
    public string RequiredPermission { get; } = requiredPermission;

    public InsufficientPermissionsError() : this(null, null)
    {
    }

    public InsufficientPermissionsError(string requiredPermission) : this(null, requiredPermission)
    {
    }
}