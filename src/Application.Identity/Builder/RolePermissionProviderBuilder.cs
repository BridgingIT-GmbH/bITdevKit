// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Identity;

public class RolePermissionProviderBuilder
{
    private readonly IEntityPermissionProvider provider;
    private readonly string roleName;

    internal RolePermissionProviderBuilder(
        IEntityPermissionProvider provider,
        string roleName)
    {
        this.provider = provider;
        this.roleName = roleName;
    }

    /// <summary>
    /// Grants a permission on a specific entity (id).
    /// </summary>
    public RolePermissionProviderBuilder WithPermission<TEntity>(object entityId, string permission)
    {
        return this.WithPermission(typeof(TEntity).FullName, entityId, permission);
    }

    /// <summary>
    /// Grants a permission on a specific entity (id).
    /// </summary>
    public RolePermissionProviderBuilder WithPermission(string entityType, object entityId, string permission)
    {
        this.provider.GrantRolePermissionAsync(this.roleName, entityType, entityId, permission).GetAwaiter().GetResult();
        return this;
    }

    /// <summary>
    /// Grants a wildcard permission for an entity type (no id).
    /// </summary>
    public RolePermissionProviderBuilder WithPermission<TEntity>(string permission)
    {
        return this.WithPermission(typeof(TEntity).FullName, permission);
    }

    /// <summary>
    /// Grants a wildcard permission for an entity type (no id).
    /// </summary>
    public RolePermissionProviderBuilder WithPermission(string entityType, string permission)
    {
        this.provider.GrantRolePermissionAsync(this.roleName, entityType, null, permission).GetAwaiter().GetResult();
        return this;
    }

    /// <summary>
    /// Starts configuring permissions for a user.
    /// </summary>
    public UserPermissionProviderBuilder ForUser(string userId) => new(this.provider, userId);

    /// <summary>
    /// Starts configuring permissions for another role.
    /// </summary>
    public RolePermissionProviderBuilder ForRole(string roleName) => new(this.provider, roleName);

    /// <summary>
    /// Returns the configured provider.
    /// </summary>
    public IEntityPermissionProvider Build() => this.provider;
}