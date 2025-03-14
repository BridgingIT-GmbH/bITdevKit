// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Identity;

public class UserPermissionProviderBuilder
{
    private readonly IEntityPermissionProvider provider;
    private readonly string userId;

    internal UserPermissionProviderBuilder(
        IEntityPermissionProvider provider,
        string userId)
    {
        this.provider = provider;
        this.userId = userId;
    }

    /// <summary>
    /// Grants a permission on a specific entity.
    /// </summary>
    public UserPermissionProviderBuilder WithPermission<TEntity>(object entityId, string permission)
    {
        this.provider.GrantUserPermissionAsync(this.userId, typeof(TEntity).FullName, entityId, permission).GetAwaiter().GetResult();
        return this;
    }

    /// <summary>
    /// Grants a permission on a specific entity.
    /// </summary>
    public UserPermissionProviderBuilder WithPermission(string entityType, object entityId, string permission)
    {
        this.provider.GrantUserPermissionAsync(this.userId, entityType, entityId, permission).GetAwaiter().GetResult();
        return this;
    }

    /// <summary>
    /// Grants a wildcard permission for an entity type.
    /// </summary>
    public UserPermissionProviderBuilder WithPermission<TEntity>(string permission)
    {
        this.provider.GrantUserPermissionAsync(this.userId, typeof(TEntity).FullName, null, permission).GetAwaiter().GetResult();
        return this;
    }

    /// <summary>
    /// Grants a wildcard permission for an entity type.
    /// </summary>
    public UserPermissionProviderBuilder WithPermission(string entityType, string permission)
    {
        this.provider.GrantUserPermissionAsync(this.userId, entityType, null, permission).GetAwaiter().GetResult();
        return this;
    }

    /// <summary>
    /// Starts configuring permissions for another user.
    /// </summary>
    public UserPermissionProviderBuilder ForUser(string userId) => new(this.provider, userId);

    /// <summary>
    /// Switches to configuring role permissions.
    /// </summary>
    public RolePermissionProviderBuilder ForRole(string roleName) => new(this.provider, roleName);

    /// <summary>
    /// Returns the configured provider.
    /// </summary>
    public IEntityPermissionProvider Build() => this.provider;
}
