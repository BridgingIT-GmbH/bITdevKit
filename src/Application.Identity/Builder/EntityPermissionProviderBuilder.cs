// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Identity;

public class EntityPermissionProviderBuilder(IEntityPermissionProvider provider)
{
    /// <summary>
    /// Starts configuring permissions for a specific user.
    /// </summary>
    public UserPermissionProviderBuilder ForUser(string userId) => new(provider, userId);

    /// <summary>
    /// Starts configuring permissions for a specific role.
    /// </summary>
    public RolePermissionProviderBuilder ForRole(string roleName) => new(provider, roleName);

    /// <summary>
    /// Returns the configured provider.
    /// </summary>
    public IEntityPermissionProvider Build() => provider;
}
