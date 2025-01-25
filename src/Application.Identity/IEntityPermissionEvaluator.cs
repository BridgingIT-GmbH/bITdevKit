// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Identity;

using BridgingIT.DevKit.Domain.Model;

/// <summary>
/// Interface for evaluating permissions on entities.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
public interface IEntityPermissionEvaluator<TEntity> where TEntity : class, IEntity
{
    /// <summary>
    /// Checks if the user has the specified permission on the given entity.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="roles">The roles the user belongs to.</param>
    /// <param name="entity">The entity to check permissions for.</param>
    /// <param name="permission">The permission to check.</param>
    /// <param name="bypassCache">Whether to bypass the cache.</param>
    public Task<bool> HasPermissionAsync(string userId, string[] roles, TEntity entity, string permission, bool bypassCache = false);

    /// <summary>
    /// Checks if the user has the specified permission on the given entity.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="roles">The roles the user belongs to.</param>
    /// <param name="entityId">The entity id to get permissions for.</param>
    /// <param name="permission">The permission to check.</param>
    /// <param name="bypassCache">Whether to bypass the cache.</param>
    public Task<bool> HasPermissionAsync(string userId, string[] roles, object entityId, string permission, bool bypassCache = false);

    /// <summary>
    /// Checks if the user has the specified permission on the entity type.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="roles">The roles the user belongs to.</param>
    /// <param name="permission">The permission to check.</param>
    /// <param name="bypassCache">Whether to bypass the cache.</param>
    Task<bool> HasPermissionAsync(string userId, string[] roles, string permission, bool bypassCache = false); // wildcard permission

    /// <summary>
    /// Gets the permissions for the user on the given entity.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="roles">The roles the user belongs to.</param>
    /// <param name="entity">The entity to get permissions for.</param>
    Task<IReadOnlyCollection<EntityPermissionInfo>> GetPermissionsAsync(string userId, string[] roles, TEntity entity);

    /// <summary>
    /// Gets the permissions for the user for the given entity id.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="roles">The roles the user belongs to.</param>
    /// <param name="entityId">The entity id to get permissions for.</param>
    Task<IReadOnlyCollection<EntityPermissionInfo>> GetPermissionsAsync(string userId, string[] roles, object entityId);

    /// <summary>
    /// Gets the permissions for the user on the entity type.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="roles">The roles the user belongs to.</param>
    Task<IReadOnlyCollection<EntityPermissionInfo>> GetPermissionsAsync(string userId, string[] roles); // wildcard permission
}