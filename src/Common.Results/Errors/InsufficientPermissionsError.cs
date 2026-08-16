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
    /// <summary>Gets the permission required to perform the operation, when supplied.</summary>
    public string RequiredPermission { get; } = requiredPermission;

    /// <summary>Initializes an insufficient-permissions error with the default message and no permission name.</summary>
    public InsufficientPermissionsError() : this(null, null)
    {
    }

    /// <summary>Initializes an insufficient-permissions error for a required permission.</summary>
    /// <param name="requiredPermission">The permission required to perform the operation.</param>
    public InsufficientPermissionsError(string requiredPermission) : this(null, requiredPermission)
    {
    }
}
