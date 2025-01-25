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
[DebuggerDisplay("{Permission}")]
public class EntityPermissionRequirement(string permission) : IAuthorizationRequirement
{
    /// <summary>
    /// Gets the permission value that is required for authorization.
    /// </summary>
    /// <value>
    /// A string representing the required permission value.
    /// </value>
    public string Permission { get; } = permission;
}