// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Identity;

/// <summary>
/// Defines the contract for a service that provides access to entity permissions.
/// This interface abstracts the storage and retrieval of permissions, allowing for different implementations.
/// </summary>
public interface IEntityPermissionProvider
{
    /// <summary>
    /// Checks if a user has a specific permission for an entity of type TEntity, either directly or through their roles.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="roles">The roles assigned to the user.</param>
    /// <param name="entityId">The ID of the specific entity.</param>
    /// <param name="permission">The permission to check.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>True if the user has the permission, false otherwise.</returns>
    Task<bool> HasPermissionAsync<TEntity>(string userId, string[] roles, object entityId, string permission, CancellationToken cancellationToken = default)
    {
        return this.HasPermissionAsync(userId, roles, typeof(TEntity).FullName, entityId, permission, cancellationToken);
    }

    /// <summary>
    /// Checks if a user has a specific permission for an entity, either directly or through their roles.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="roles">The roles assigned to the user.</param>
    /// <param name="entityType">The type of the entity.</param>
    /// <param name="entityId">The ID of the specific entity.</param>
    /// <param name="permission">The permission to check.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>True if the user has the permission, false otherwise.</returns>
    Task<bool> HasPermissionAsync(string userId, string[] roles, string entityType, object entityId, string permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has a specific wildcard permission for all entities of type TEntity.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="roles">The roles assigned to the user.</param>
    /// <param name="permission">The permission to check.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>True if the user has the wildcard permission, false otherwise.</returns>
    Task<bool> HasPermissionAsync<TEntity>(string userId, string[] roles, string permission, CancellationToken cancellationToken = default)
    {
        return this.HasPermissionAsync(userId, roles, typeof(TEntity).FullName, permission, cancellationToken);
    }

    /// <summary>
    /// Checks if a user has a wildcard permission (no id) for all entities of a specific type.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="roles">The roles assigned to the user.</param>
    /// <param name="entityType">The type of the entity.</param>
    /// <param name="permission">The permission to check.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>True if the user has the wildcard permission, false otherwise.</returns>
    Task<bool> HasPermissionAsync(string userId, string[] roles, string entityType, string permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all entity IDs of type TEntity for which a user has a certain permission.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="roles">The roles assigned to the user.</param>
    /// <param name="permission">The permission to check.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A list of entity IDs for which the user has the specified permission.</returns>
    Task<IEnumerable<string>> GetEntityIdsWithPermissionAsync<TEntity>(string userId, string[] roles, string permission, CancellationToken cancellationToken = default)
    {
        return this.GetEntityIdsWithPermissionAsync(userId, roles, typeof(TEntity).FullName, permission, cancellationToken);
    }

    /// <summary>
    /// Retrieves all entity IDs of a specific type for which a user has a certain permission.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="roles">The roles assigned to the user.</param>
    /// <param name="entityType">The type of the entity.</param>
    /// <param name="permission">The permission to check.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A list of entity IDs for which the user has the specified permission.</returns>
    Task<IEnumerable<string>> GetEntityIdsWithPermissionAsync(string userId, string[] roles, string entityType, string permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the hierarchy path for a specific entity of type TEntity.
    /// </summary>
    /// <param name="entityId">The ID of the specific entity.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A list of objects representing the hierarchy path.</returns>
    Task<IEnumerable<object>> GetHierarchyPathAsync<TEntity>(object entityId, CancellationToken cancellationToken = default)
    {
        return this.GetHierarchyPathAsync(typeof(TEntity), entityId, cancellationToken);
    }

    /// <summary>
    /// Retrieves the hierarchy path for a specific entity.
    /// </summary>
    /// <param name="entityType">The type of the entity.</param>
    /// <param name="entityId">The ID of the specific entity.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A list of objects representing the hierarchy path.</returns>
    Task<IEnumerable<object>> GetHierarchyPathAsync(Type entityType, object entityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Grants a specific permission to a user for an entity of type TEntity.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="entityId">The ID of the specific entity.</param>
    /// <param name="permission">The permission to grant.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task GrantUserPermissionAsync<TEntity>(string userId, object entityId, string permission, CancellationToken cancellationToken = default)
    {
        return this.GrantUserPermissionAsync(userId, typeof(TEntity).FullName, entityId, permission, cancellationToken);
    }

    /// <summary>
    /// Grants a specific permission to a user for a specific entity.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="entityType">The type of the entity.</param>
    /// <param name="entityId">The ID of the specific entity.</param>
    /// <param name="permission">The permission to grant.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task GrantUserPermissionAsync(string userId, string entityType, object entityId, string permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all granted permissions for all users and roles for an entity of type TEntity.
    /// </summary>
    /// <param name="entityId">The ID of the specific entity.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A read-only collection of entity permission information.</returns>
    Task<IReadOnlyCollection<EntityPermissionInfo>> GetPermissionsAsync<TEntity>(object entityId, CancellationToken cancellationToken = default)
    {
        return this.GetPermissionsAsync(typeof(TEntity).FullName, entityId, cancellationToken);
    }

    /// <summary>
    /// Retrieves all granted permissions for all users and roles for a specific entity (id) or entity type (wildcard).
    /// </summary>
    /// <param name="entityType">The type of the entity.</param>
    /// <param name="entityId">The ID of the specific entity.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A read-only collection of entity permission information.</returns>
    Task<IReadOnlyCollection<EntityPermissionInfo>> GetPermissionsAsync(string entityType, object entityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all granted permissions for all users for an entity of type TEntity.
    /// </summary>
    /// <param name="entityId">The ID of the specific entity.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A read-only collection of entity permission information.</returns>
    Task<IReadOnlyCollection<EntityPermissionInfo>> GetUsersPermissionsAsync<TEntity>(object entityId, CancellationToken cancellationToken = default)
    {
        return this.GetUsersPermissionsAsync(typeof(TEntity).FullName, entityId, cancellationToken);
    }

    /// <summary>
    /// Retrieves all granted permissions for all users for a specific entity (id) or entity type (wildcard).
    /// </summary>
    /// <param name="entityType">The type of the entity.</param>
    /// <param name="entityId">The ID of the specific entity.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A read-only collection of entity permission information.</returns>
    Task<IReadOnlyCollection<EntityPermissionInfo>> GetUsersPermissionsAsync(string entityType, object entityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all granted permissions for all roles for an entity of type TEntity.
    /// </summary>
    /// <param name="entityId">The ID of the specific entity.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A read-only collection of entity permission information.</returns>
    Task<IReadOnlyCollection<EntityPermissionInfo>> GetRolesPermissionsAsync<TEntity>(object entityId, CancellationToken cancellationToken = default)
    {
        return this.GetRolesPermissionsAsync(typeof(TEntity).FullName, entityId, cancellationToken);
    }

    /// <summary>
    /// Retrieves all granted permissions for all roles for a specific entity (id) or entity type (wildcard).
    /// </summary>
    /// <param name="entityType">The type of the entity.</param>
    /// <param name="entityId">The ID of the specific entity.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A read-only collection of entity permission information.</returns>
    Task<IReadOnlyCollection<EntityPermissionInfo>> GetRolesPermissionsAsync(string entityType, object entityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a specific permission from a user for an entity of type TEntity.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="entityId">The ID of the specific entity.</param>
    /// <param name="permission">The permission to revoke.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RevokeUserPermissionAsync<TEntity>(string userId, object entityId, string permission, CancellationToken cancellationToken = default)
    {
        return this.RevokeUserPermissionAsync(userId, typeof(TEntity).FullName, entityId, permission, cancellationToken);
    }

    /// <summary>
    /// Revokes a specific permission from a user for a specific entity.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="entityType">The type of the entity.</param>
    /// <param name="entityId">The ID of the specific entity.</param>
    /// <param name="permission">The permission to revoke.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RevokeUserPermissionAsync(string userId, string entityType, object entityId, string permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes all permissions from a user.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RevokeUserPermissionsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Grants a specific permission to a role for an entity of type TEntity.
    /// </summary>
    /// <param name="role">The name of the role.</param>
    /// <param name="entityId">The ID of the specific entity.</param>
    /// <param name="permission">The permission to grant.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task GrantRolePermissionAsync<TEntity>(string role, object entityId, string permission, CancellationToken cancellationToken = default)
    {
        return this.GrantRolePermissionAsync(role, typeof(TEntity).FullName, entityId, permission, cancellationToken);
    }

    /// <summary>
    /// Grants a specific permission to a role for a specific entity.
    /// </summary>
    /// <param name="role">The name of the role.</param>
    /// <param name="entityType">The type of the entity.</param>
    /// <param name="entityId">The ID of the specific entity.</param>
    /// <param name="permission">The permission to grant.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task GrantRolePermissionAsync(string role, string entityType, object entityId, string permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a specific permission from a role for an entity of type TEntity.
    /// </summary>
    /// <param name="role">The name of the role.</param>
    /// <param name="entityId">The ID of the specific entity.</param>
    /// <param name="permission">The permission to revoke.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RevokeRolePermissionAsync<TEntity>(string role, object entityId, string permission, CancellationToken cancellationToken = default)
    {
        return this.RevokeRolePermissionAsync(role, typeof(TEntity).FullName, entityId, permission, cancellationToken);
    }

    /// <summary>
    /// Revokes a specific permission from a role for a specific entity.
    /// </summary>
    /// <param name="role">The name of the role.</param>
    /// <param name="entityType">The type of the entity.</param>
    /// <param name="entityId">The ID of the specific entity.</param>
    /// <param name="permission">The permission to revoke.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RevokeRolePermissionAsync(string role, string entityType, object entityId, string permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes all permissions from a role.
    /// </summary>
    /// <param name="role">The name of the role.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RevokeRolePermissionsAsync(string role, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all permissions for a user for an entity of type TEntity.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="entityId">The ID of the specific entity.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A read-only collection of permissions.</returns>
    Task<IReadOnlyCollection<string>> GetUserPermissionsAsync<TEntity>(string userId, object entityId, CancellationToken cancellationToken = default)
    {
        return this.GetUserPermissionsAsync(userId, typeof(TEntity).FullName, entityId, cancellationToken);
    }

    /// <summary>
    /// Retrieves all permissions for a user for a specific entity.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="entityType">The type of the entity.</param>
    /// <param name="entityId">The ID of the specific entity.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A read-only collection of permissions.</returns>
    Task<IReadOnlyCollection<string>> GetUserPermissionsAsync(string userId, string entityType, object entityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all permissions for a role for an entity of type TEntity.
    /// </summary>
    /// <param name="role">The name of the role.</param>
    /// <param name="entityId">The ID of the specific entity.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A read-only collection of permissions.</returns>
    Task<IReadOnlyCollection<string>> GetRolePermissionsAsync<TEntity>(string role, object entityId, CancellationToken cancellationToken = default)
    {
        return this.GetRolePermissionsAsync(role, typeof(TEntity).FullName, entityId, cancellationToken);
    }

    /// <summary>
    /// Retrieves all permissions for a role for a specific entity.
    /// </summary>
    /// <param name="role">The name of the role.</param>
    /// <param name="entityType">The type of the entity.</param>
    /// <param name="entityId">The ID of the specific entity.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A read-only collection of permissions.</returns>
    Task<IReadOnlyCollection<string>> GetRolePermissionsAsync(string role, string entityType, object entityId, CancellationToken cancellationToken = default);
}