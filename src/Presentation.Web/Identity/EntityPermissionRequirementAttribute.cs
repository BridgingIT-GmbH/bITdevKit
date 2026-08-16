// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Identity;

using BridgingIT.DevKit.Application.Identity;
using Microsoft.AspNetCore.Authorization;

/// <summary>
/// Identifies a declaration with entity permission requirement metadata.
/// </summary>
/// <param name="entityType">The name of the entity type.</param>
/// <param name="permission">The permission used by the operation.</param>
public class EntityPermissionRequirementAttribute(Type entityType, string permission)
    : AuthorizeAttribute($"{nameof(EntityPermissionRequirement)}_{entityType.FullName}_{permission}")
{
}
