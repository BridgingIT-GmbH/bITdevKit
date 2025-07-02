// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Identity;

using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;

/// <summary>
/// Represents a requirement for entity-level permission authorization.
/// This class is used in conjunction with the ASP.NET Core authorization system to validate entity permissions.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="EntityPermissionRequirement"/> class.
/// </remarks>
/// <param name="permission">The permission value that is required for authorization.</param>
[DebuggerDisplay("Permissions={Permissions}")]
public class EntityPermissionRequirement : IAuthorizationRequirement
{
    public EntityPermissionRequirement(string permission)
    {
        this.Permissions = [permission];
    }

    public EntityPermissionRequirement(string[] permissions)
    {
        this.Permissions = permissions;
    }

    /// <summary>
    /// Gets the permissions that any of is required for authorization.
    /// </summary>
    /// <value>
    /// A string representing any of the required permissions.
    /// </value>
    public string[] Permissions { get; init; }
}