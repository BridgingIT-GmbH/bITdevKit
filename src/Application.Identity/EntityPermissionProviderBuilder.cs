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
    public UserPermissionProviderBuilder WithPermission(string entityType, object entityId, string permission)
    {
        this.provider.GrantUserPermissionAsync(this.userId, entityType, entityId, permission).GetAwaiter().GetResult();
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
    public RolePermissionProviderBuilder WithPermission(string entityType, object entityId, string permission)
    {
        this.provider.GrantRolePermissionAsync(this.roleName, entityType, entityId, permission).GetAwaiter().GetResult();
        return this;
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