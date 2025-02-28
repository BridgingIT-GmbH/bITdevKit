// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Identity;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides a mechanism to check permissions for entities.
/// </summary>
public partial class EntityPermissionEvaluator<TEntity>(
    ILoggerFactory loggerFactory,
    IEntityPermissionProvider provider,
    IEnumerable<IDefaultEntityPermissionProvider<TEntity>> defaultProviders,
    ICacheProvider cacheProvider = null,
    EntityPermissionOptions options = null) : IEntityPermissionEvaluator<TEntity>
    where TEntity : class, IEntity
{
    private readonly ILogger<EntityPermissionEvaluator<TEntity>> logger = loggerFactory.CreateLogger<EntityPermissionEvaluator<TEntity>>();
    private readonly EntityPermissionOptions options = options ?? new EntityPermissionOptions();

    /// <summary>
    /// Checks if the user has the specified permission on the entity type.
    /// </summary>
    /// <param name="currentUserAccessor">The accessor for the current user</param>
    /// <param name="entity">The entity to check permissions for.</param>
    /// <param name="permission">The permission to check.</param>
    /// <param name="bypassCache">Whether to bypass the cache.</param>
    public async Task<bool> HasPermissionAsync(ICurrentUserAccessor currentUserAccessor, TEntity entity, string permission, bool bypassCache = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currentUserAccessor, nameof(currentUserAccessor));
        return await this.HasPermissionAsync(currentUserAccessor.UserId, currentUserAccessor.Roles, entity, permission, bypassCache, cancellationToken);
    }

    /// <summary>
    /// Checks if the user has the specified permission on the entity type.
    /// </summary>
    /// <param name="currentUserAccessor">The accessor for the current user</param>
    /// <param name="entityId">The entity id to get permissions for.</param>
    /// <param name="permission">The permission to check.</param>
    /// <param name="bypassCache">Whether to bypass the cache.</param>
    public async Task<bool> HasPermissionAsync(ICurrentUserAccessor currentUserAccessor, object entityId, string permission, bool bypassCache = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currentUserAccessor, nameof(currentUserAccessor));
        return await this.HasPermissionAsync(currentUserAccessor.UserId, currentUserAccessor.Roles, entityId, permission, bypassCache, cancellationToken);
    }

    /// <summary>
    /// Checks if the user has the specified permission on the entity type.
    /// </summary>
    /// <param name="currentUserAccessor">The accessor for the current user</param>
    /// <param name="permission">The permission to check.</param>
    /// <param name="bypassCache">Whether to bypass the cache.</param>
    public async Task<bool> HasPermissionAsync(ICurrentUserAccessor currentUserAccessor, string permission, bool bypassCache = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currentUserAccessor, nameof(currentUserAccessor));
        return await this.HasPermissionAsync(currentUserAccessor.UserId, currentUserAccessor.Roles, permission, bypassCache, cancellationToken);
    }

    /// <summary>
    /// Gets the permissions for the user on the entity type.
    /// </summary>
    /// <param name="currentUserAccessor">The accessor for the current user</param>
    public async Task<IReadOnlyCollection<EntityPermissionInfo>> GetPermissionsAsync(ICurrentUserAccessor currentUserAccessor, TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currentUserAccessor, nameof(currentUserAccessor));
        return await this.GetPermissionsAsync(currentUserAccessor.UserId, currentUserAccessor.Roles, entity, cancellationToken);
    }

    /// <summary>
    /// Gets the permissions for the user on the entity type.
    /// </summary>
    /// <param name="currentUserAccessor">The accessor for the current user</param>
    public async Task<IReadOnlyCollection<EntityPermissionInfo>> GetPermissionsAsync(ICurrentUserAccessor currentUserAccessor, object entityId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currentUserAccessor, nameof(currentUserAccessor));
        return await this.GetPermissionsAsync(currentUserAccessor.UserId, currentUserAccessor.Roles, entityId, cancellationToken);
    }

    /// <summary>
    /// Gets the permissions for the user on the entity type.
    /// </summary>
    /// <param name="currentUserAccessor">The accessor for the current user</param>
    public async Task<IReadOnlyCollection<EntityPermissionInfo>> GetPermissionsAsync(ICurrentUserAccessor currentUserAccessor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currentUserAccessor, nameof(currentUserAccessor));
        return await this.GetPermissionsAsync(currentUserAccessor.UserId, currentUserAccessor.Roles, cancellationToken);
    }

    /// <summary>
    /// Checks if the user has the specified permission on the given entity.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="roles">The roles the user belongs to.</param>
    /// <param name="entity">The entity to check permissions for.</param>
    /// <param name="permission">The permission to check.</param>
    /// <param name="bypassCache">Whether to bypass the cache.</param>
    public async Task<bool> HasPermissionAsync(string userId, string[] roles, TEntity entity, string permission, bool bypassCache = false, CancellationToken cancellationToken = default)
    {
        return await this.HasPermissionAsync(userId, roles, entity?.Id, permission, bypassCache, cancellationToken);
    }

    /// <summary>
    /// Checks if the user/roles has the specified permission on the given entity.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="roles">The roles the user belongs to.</param>
    /// <param name="entityId">The entity id to get permissions for.</param>
    /// <param name="permission">The permission to check.</param>
    /// <param name="bypassCache">Whether to bypass the cache.</param>
    public async Task<bool> HasPermissionAsync(string userId, string[] roles, object entityId, string permission, bool bypassCache = false, CancellationToken cancellationToken = default)
    {
        var entityType = typeof(TEntity).FullName;

        // Check cache first if available
        if (!bypassCache && cacheProvider != null && this.options.EnableCaching) // Try cache first if available
        {
            var cacheKey = EntityPermissionCacheKeys.ForUserEntity(userId, entityType, entityId);
            if (cacheProvider.TryGet(cacheKey, out EntityPermissionCacheEntry entry) &&
                entry?.Permissions?.Contains(permission) == true)
            {
                TypedLogger.LogPermissionCacheHit(this.logger, Constants.LogKey, entityType, entityId?.ToString(), permission, userId, entry.PermissionSources.FirstOrDefault().Value);

                return true;
            }
        }

        // Check actual direct permissions
        var hasPermission = await provider.HasPermissionAsync(userId, roles, entityType, entityId, permission, cancellationToken);
        if (hasPermission)
        {
            TypedLogger.LogPermissionGranted(this.logger, Constants.LogKey, entityType, entityId?.ToString(), permission, userId, "Direct");
            this.UpdateCache(userId, entityType, entityId, permission, "Direct");

            return true;
        }

        // Check hierarchical permissions if entity supports it and hierarchical checks aren't disabled
        var entityTypeConfiguration = this.options.GetEntityTypeConfiguration<TEntity>();
        if (entityTypeConfiguration.IsHierarchical && this.options.EnableHierarchicalPermissions)
        {
            var parentIds = await provider.GetHierarchyPathAsync(typeof(TEntity), entityId, cancellationToken);
            foreach (var parentId in parentIds)
            {
                var hasParentPermission = await provider.HasPermissionAsync(userId, roles, entityType, parentId, permission, cancellationToken);
                if (hasParentPermission)
                {
                    TypedLogger.LogPermissionGrantedFromParent(this.logger, Constants.LogKey, entityType, entityId?.ToString(), permission, userId, $"Parent:{parentId}");

                    this.UpdateCache(userId, entityType, entityId, permission, $"Parent:{parentId}");

                    return true;
                }
            }
        }

        if (defaultProviders?.Any() == true) // Check default providers if available
        {
            foreach (var defaultProvider in defaultProviders)
            {
                if (defaultProvider.GetDefaultPermissions().Contains(permission))
                {
                    TypedLogger.LogPermissionGrantedDefault(this.logger, Constants.LogKey, entityType, entityId?.ToString(), permission, userId, $"Default:{defaultProvider.GetType().PrettyName()}");

                    this.UpdateCache(userId, entityType, entityId, permission, $"Default:{defaultProvider.GetType().PrettyName()}");

                    return true;
                }
            }
        }

        TypedLogger.LogPermissionDenied(this.logger, Constants.LogKey, entityType, entityId?.ToString(), permission, userId);

        return false;
    }

    /// <summary>
    /// Checks if the user/roles has the specified permission on the entity type.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="roles">The roles the user belongs to.</param>
    /// <param name="permission">The permission to check.</param>
    /// <param name="bypassCache">Whether to bypass the cache.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether the user has the permission.</returns>
    public async Task<bool> HasPermissionAsync(string userId, string[] roles, string permission, bool bypassCache = false, CancellationToken cancellationToken = default) // wildcard permission
    {
        var entityType = typeof(TEntity).FullName;

        // Check cache first if available
        if (!bypassCache && cacheProvider != null && this.options.EnableCaching)
        {
            var cacheKey = EntityPermissionCacheKeys.ForUserEntityType(userId, entityType);
            if (cacheProvider.TryGet(cacheKey, out EntityPermissionCacheEntry entry) &&
                entry?.Permissions?.Contains(permission) == true)
            {
                TypedLogger.LogPermissionCacheHit(this.logger, Constants.LogKey, entityType, null, permission, userId, entry.PermissionSources.FirstOrDefault().Value);

                return true;
            }
        }

        // Check type-wide permissions through provider
        var hasPermission = await provider.HasPermissionAsync(userId, roles, entityType, permission, cancellationToken);
        if (hasPermission)
        {
            TypedLogger.LogPermissionGranted(this.logger, Constants.LogKey, entityType, null, permission, userId, "Direct");
            this.UpdateCache(userId, entityType, null, permission, "Direct");
            return true;
        }

        // Check default providers
        if (defaultProviders?.Any() == true)
        {
            foreach (var defaultProvider in defaultProviders)
            {
                if (defaultProvider.GetDefaultPermissions().Contains(permission))
                {
                    TypedLogger.LogPermissionGrantedDefault(this.logger, Constants.LogKey, entityType, null, permission, userId, $"Default:{defaultProvider.GetType().PrettyName()}");
                    this.UpdateCache(userId, entityType, null, permission, $"Default:{defaultProvider.GetType().PrettyName()}");
                    return true;
                }
            }
        }

        TypedLogger.LogPermissionDenied(this.logger, Constants.LogKey, entityType, null, permission, userId);
        return false;
    }

    /// <summary>
    /// Gets the permissions for the user on the given entity.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="roles">The roles the user belongs to.</param>
    /// <param name="entity">The entity to get permissions for.</param>
    public async Task<IReadOnlyCollection<EntityPermissionInfo>> GetPermissionsAsync(string userId, string[] roles, TEntity entity, CancellationToken cancellationToken = default)
    {
        return await this.GetPermissionsAsync(userId, roles, entity?.Id, cancellationToken);
    }

    /// <summary>
    /// Gets the permissions for the user/roles for the given entity id.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="roles">The roles the user belongs to.</param>
    /// <param name="entityId">The entity id to get permissions for.</param>
    public async Task<IReadOnlyCollection<EntityPermissionInfo>> GetPermissionsAsync(string userId, string[] roles, object entityId, CancellationToken cancellationToken = default)
    {
        var entityType = typeof(TEntity).FullName;
        var result = new List<EntityPermissionInfo>();

        // Get direct user permissions
        var directPermissions = await provider.GetUserPermissionsAsync(userId, entityType, entityId, cancellationToken);
        foreach (var permission in directPermissions)
        {
            result.Add(new EntityPermissionInfo
            {
                Permission = permission,
                Source = "Direct",
                //SourceId = entityId,
                UserId = userId,
                EntityType = entityType,
                //EntityId = entity.Id
            });
        }

        // Get role permissions
        if (roles?.Any() == true)
        {
            foreach (var role in roles)
            {
                var rolePermissions = await provider.GetRolePermissionsAsync(role, entityType, entityId, cancellationToken);
                foreach (var permission in rolePermissions)
                {
                    result.Add(new EntityPermissionInfo
                    {
                        Permission = permission,
                        Source = $"Role:{role}",
                        //SourceId = entityId,
                        RoleName = role,
                        EntityType = entityType,
                        //EntityId = entity.Id
                    });
                }
            }
        }

        // Get hierarchical permissions
        var entityConfiguration = this.options.GetEntityTypeConfiguration<TEntity>();
        if (entityConfiguration.IsHierarchical && this.options.EnableHierarchicalPermissions)
        {
            var parentIds = await provider.GetHierarchyPathAsync(typeof(TEntity), entityId, cancellationToken);
            foreach (var parentId in parentIds)
            {
                // Get parent direct permissions
                var parentDirectPermissions = await provider.GetUserPermissionsAsync(userId, entityType, parentId, cancellationToken);
                foreach (var permission in parentDirectPermissions)
                {
                    result.Add(new EntityPermissionInfo
                    {
                        Permission = permission,
                        Source = $"Parent:Entity:{parentId}",
                        //SourceId = parentId,
                        UserId = userId,
                        EntityType = entityType,
                        //EntityId = entity.Id
                    });
                }

                // Get parent role permissions
                if (roles?.Any() == true)
                {
                    foreach (var role in roles)
                    {
                        var parentRolePermissions = await provider.GetRolePermissionsAsync(role, entityType, parentId, cancellationToken);
                        foreach (var permission in parentRolePermissions)
                        {
                            result.Add(new EntityPermissionInfo
                            {
                                Permission = permission,
                                Source = $"Parent:Role:{role}",
                                //SourceId = parentId,
                                RoleName = role,
                                EntityType = entityType,
                                //EntityId = entity.Id
                            });
                        }
                    }
                }
            }
        }

        // Get default permissions
        if (defaultProviders?.Any() == true)
        {
            foreach (var defaultProvider in defaultProviders)
            {
                foreach (var permission in defaultProvider.GetDefaultPermissions())
                {
                    result.Add(new EntityPermissionInfo
                    {
                        Permission = permission,
                        Source = $"Default:{defaultProvider.GetType().PrettyName()}",
                        //SourceId = entityId,
                        EntityType = entityType,
                        //EntityId = null  // wildcard permission
                    });
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the permissions for the user/roles on the entity type.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="roles">The roles the user belongs to.</param>
    public async Task<IReadOnlyCollection<EntityPermissionInfo>> GetPermissionsAsync(string userId, string[] roles, CancellationToken cancellationToken = default) // wildcard permission
    {
        var entityType = typeof(TEntity).FullName;
        var result = new List<EntityPermissionInfo>();

        // Get direct user type-wide permissions
        var directPermissions = await provider.GetUserPermissionsAsync(userId, entityType, null, cancellationToken);
        foreach (var permission in directPermissions)
        {
            result.Add(new EntityPermissionInfo
            {
                Permission = permission,
                Source = "Direct",
                //SourceId = null,
                UserId = userId,
                EntityType = entityType,
                //EntityId = null  // type-wide permission
            });
        }

        // Get role type-wide permissions
        if (roles?.Any() == true)
        {
            foreach (var role in roles)
            {
                var rolePermissions = await provider.GetRolePermissionsAsync(role, entityType, null, cancellationToken);
                foreach (var permission in rolePermissions)
                {
                    result.Add(new EntityPermissionInfo
                    {
                        Permission = permission,
                        Source = $"Role:{role}",
                        //SourceId = null,
                        RoleName = role,
                        EntityType = entityType,
                        //EntityId = null  // type-wide permission
                    });
                }
            }
        }

        // Get default permissions
        if (defaultProviders?.Any() == true)
        {
            foreach (var defaultProvider in defaultProviders)
            {
                var defaultPermissions = defaultProvider.GetDefaultPermissions();
                foreach (var permission in defaultPermissions)
                {
                    result.Add(new EntityPermissionInfo
                    {
                        Permission = permission,
                        Source = $"Default:{defaultProvider.GetType().PrettyName()}",
                        //SourceId = null,
                        EntityType = entityType,
                        //EntityId = null  // type-wide permission
                    });
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Updates the cache with the specified permission.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="entityType">The type of the entity.</param>
    /// <param name="entityId">The ID of the entity.</param>
    /// <param name="permission">The permission to add to the cache.</param>
    /// <param name="source">The source of the permission.</param>
    private void UpdateCache(string userId, string entityType, object entityId, string permission, string source)
    {
        if (cacheProvider == null || !this.options.EnableCaching)
        {
            return;
        }

        var cacheKey = EntityPermissionCacheKeys.ForUserEntity(userId, entityType, entityId);
        var entry = cacheProvider.Get<EntityPermissionCacheEntry>(cacheKey) ?? new EntityPermissionCacheEntry();
        entry.AddPermission(permission, source);
        this.SetCache(cacheKey, entry);
    }

    /// <summary>
    /// Sets the cache with the specified entry.
    /// </summary>
    /// <param name="cacheKey">The cache key.</param>
    /// <param name="entry">The cache entry.</param>
    private void SetCache(string cacheKey, EntityPermissionCacheEntry entry)
    {
        cacheProvider?.Set(cacheKey, entry, slidingExpiration: this.options.CacheLifetime);
    }

    /// <summary>
    /// Provides logging methods for permission evaluation events.
    /// </summary>
    public static partial class TypedLogger
    {
        [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "{LogKey} permission evaluator - granted from cache: {EntityType}/{EntityId}, permission={Permission}, user={UserId}, source={PermissionSource}")]
        public static partial void LogPermissionCacheHit(ILogger logger, string logKey, string entityType, string entityId, string permission, string userId, string permissionSource);

        [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "{LogKey} permission evaluator - granted: {EntityType}/{EntityId}, permission={Permission}, user={UserId}, source={PermissionSource}")]
        public static partial void LogPermissionGranted(ILogger logger, string logKey, string entityType, string entityId, string permission, string userId, string permissionSource);

        [LoggerMessage(EventId = 4, Level = LogLevel.Debug, Message = "{LogKey} permission evaluator - granted (default): {EntityType}/{EntityId}, permission={Permission}, user={UserId}, source={PermissionSource}")]
        public static partial void LogPermissionGrantedDefault(ILogger logger, string logKey, string entityType, string entityId, string permission, string userId, string permissionSource);

        [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "{LogKey} permission evaluator - denied: {EntityType}/{EntityId}, permission={Permission}, user={UserId}")]
        public static partial void LogPermissionDenied(ILogger logger, string logKey, string entityType, string entityId, string permission, string userId);

        [LoggerMessage(EventId = 6, Level = LogLevel.Debug, Message = "{LogKey}  permission evaluator - granted (parent): {EntityType}/{EntityId}, permission={Permission}, user={UserId}, source={PermissionSource}")]
        public static partial void LogPermissionGrantedFromParent(ILogger logger, string logKey, string entityType, string entityId, string permission, string userId, string permissionSource);
    }
}
