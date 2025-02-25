// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.Reflection;
using BridgingIT.DevKit.Application.Identity;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides entity permission functionality using Entity Framework Core as the storage mechanism.
/// </summary>
/// <typeparam name="TContext">The type of the DbContext implementing IEntityPermissionContext.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="EntityFrameworkPermissionProvider{TContext}"/> class.
/// </remarks>
public partial class EntityFrameworkPermissionProvider<TContext>
    : IEntityPermissionProvider
    where TContext : DbContext, IEntityPermissionContext
{
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<EntityFrameworkPermissionProvider<TContext>> logger;
    private readonly ICacheProvider cacheProvider;
    private readonly IHierarchyQueryProvider queryProvider;
    private readonly EntityPermissionOptions options;

    public EntityFrameworkPermissionProvider(
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider,
        ICacheProvider cacheProvider = null,
        EntityPermissionOptions options = null)
    {
        this.serviceProvider = serviceProvider;
        this.logger = loggerFactory.CreateLogger<EntityFrameworkPermissionProvider<TContext>>();
        this.options = options ?? new EntityPermissionOptions();

        // Determine provider based on current DbContext
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        this.queryProvider = GetQueryProvider(context.Database.ProviderName);
        this.cacheProvider = cacheProvider;
        this.options = options ?? new EntityPermissionOptions();
    }

    private static IHierarchyQueryProvider GetQueryProvider(string providerName)
    {
        return providerName switch
        {
            "Microsoft.EntityFrameworkCore.InMemory" => new InMemoryHierarchyQueryProvider(),
            "Microsoft.EntityFrameworkCore.SqlServer" => new SqlServerHierarchyQueryProvider(),
            "Microsoft.EntityFrameworkCore.Sqlite" => new SqliteHierarchyQueryProvider(),
            "Npgsql.EntityFrameworkCore.PostgreSQL" => new PostgreSqlHierarchyQueryProvider(),
            _ => throw new NotSupportedException(
                $"Database provider {providerName} is not supported for hierarchical queries. " +
                $"Supported providers are: SQL Server, PostgreSQL, SQLite, and InMemory")
        };
    }

    /// <inheritdoc/>
    public async Task<bool> HasPermissionAsync(
        string userId,
        string[] roles,
        string entityType,
        object entityId,
        string permission, CancellationToken cancellationToken = default)
    {
        entityType = this.options?.GetEntityTypeConfiguration(entityType, false)?.EntityType?.FullName ?? entityType;

        using var scope = this.serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();

        TypedLogger.LogCheckingPermission(this.logger, Constants.LogKey, entityType, entityId?.ToString(), permission, userId);

        // Check user-specific permission first
        var safeEntityId = entityId?.ToString().EmptyToNull();
        var hasUserPermission = await context.EntityPermissions
            .AnyAsync(p =>
                p.UserId == userId &&
                p.EntityType == entityType &&
                p.EntityId == safeEntityId &&
                p.Permission == permission, cancellationToken).AnyContext();

        if (hasUserPermission)
        {
            return true;
        }

        // Check user wildcard permission
        var hasUserWildcard = await context.EntityPermissions
            .AnyAsync(p =>
                p.UserId == userId &&
                p.EntityType == entityType &&
                p.EntityId == null &&
                p.Permission == permission, cancellationToken: cancellationToken).AnyContext();

        if (hasUserWildcard)
        {
            return true;
        }

        // Check role permissions if roles are provided
        if (roles?.Any() == true)
        {
            return await context.EntityPermissions
                .AnyAsync(p =>
                    roles.Contains(p.RoleName) &&
                    p.EntityType == entityType &&
                    (p.EntityId == safeEntityId || p.EntityId == null) &&
                    p.Permission == permission, cancellationToken: cancellationToken).AnyContext();
        }

        return false;
    }

    /// <inheritdoc/>
    public async Task<bool> HasPermissionAsync(
        string userId,
        string[] roles,
        string entityType,
        string permission, CancellationToken cancellationToken = default) // wildcard permission
    {
        entityType = this.options?.GetEntityTypeConfiguration(entityType, false)?.EntityType?.FullName ?? entityType;

        using var scope = this.serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();

        TypedLogger.LogCheckingWildcardPermission(this.logger, Constants.LogKey, entityType, permission, userId);

        // Check user wildcard permission
        var hasUserWildcard = await context.EntityPermissions
            .AnyAsync(p =>
                p.UserId == userId &&
                p.EntityType == entityType &&
                p.EntityId == null &&
                p.Permission == permission, cancellationToken: cancellationToken).AnyContext();

        if (hasUserWildcard)
        {
            return true;
        }

        // Check role wildcard permissions if roles are provided
        if (roles?.Any() == true)
        {
            return await context.EntityPermissions
                .AnyAsync(p =>
                    roles.Contains(p.RoleName) &&
                    p.EntityType == entityType &&
                    p.EntityId == null &&
                    p.Permission == permission, cancellationToken: cancellationToken).AnyContext();
        }

        return false;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<string>> GetEntityIdsWithPermissionAsync(
        string userId,
        string[] roles,
        string entityType,
        string permission, CancellationToken cancellationToken = default)
    {
        entityType = this.options?.GetEntityTypeConfiguration(entityType, false)?.EntityType?.FullName ?? entityType;

        using var scope = this.serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();

        TypedLogger.LogGettingEntityIds(this.logger, Constants.LogKey, entityType, permission, userId);

        // If there's a wildcard permission, return null to indicate all entities are accessible
        var hasWildcard = await this.HasPermissionAsync(userId, roles, entityType, permission, cancellationToken);
        if (hasWildcard)
        {
            TypedLogger.LogWildcardPermissionFound(this.logger, Constants.LogKey, entityType, permission, userId);
            return [];
        }

        // Get all specific entity IDs the user has access to (either directly or through roles)
        var query = context.EntityPermissions
            .Where(p => p.EntityType == entityType &&
                       p.Permission == permission &&
                       p.EntityId != null &&
                       (p.UserId == userId || (roles.Any() && roles.Contains(p.RoleName))));

        var entityIds = await query.Select(p => p.EntityId).Distinct().ToListAsync(cancellationToken: cancellationToken).AnyContext();

        TypedLogger.LogFoundEntityIds(this.logger, Constants.LogKey, entityType, permission, userId, entityIds.Count);

        return entityIds;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<object>> GetHierarchyPathAsync(Type entityType, object entityId, CancellationToken cancellationToken = default)
    {
        using var scope = this.serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();

        var entityConfiguration = this.options?.GetEntityTypeConfiguration(entityType) ?? throw new ArgumentException($"Invalid entity type '{entityType?.FullName}'", nameof(entityType));
        if (!entityConfiguration.IsHierarchical)
        {
            return await Task.FromResult(Array.Empty<object>().AsEnumerable()).AnyContext();
        }

        // For other providers, use existing SQL approach

        var efEntityType = context.Model.FindEntityType(entityType);
        var tableName = efEntityType.GetTableName();
        var schema = efEntityType.GetSchema() ?? "dbo";
        var idColumn = efEntityType.FindProperty(nameof(IEntity.Id)).GetColumnName();
        var parentIdColumn = efEntityType.FindProperty(entityConfiguration.ParentIdProperty.Name).GetColumnName();

        var query = this.queryProvider.CreatePathQuery(schema, tableName, idColumn, parentIdColumn);

        TypedLogger.LogGettingHierarchyPath(this.logger, Constants.LogKey, entityType.Name, entityId?.ToString());

        if (query.IsNullOrEmpty()) // provider can also return no sql, then revert back to linq
        {
            // Use LINQ to get the hierarchy path, instead of SQL queries
            // For this to work we need to call GetHierarchyPathLinqAsync with a generic type (TEntity) which is not known
            // so reflection is used to call the method with the correct generic type
            var getPathMethod = typeof(EntityFrameworkPermissionProvider<TContext>)
                .GetMethod(nameof(GetHierarchyPathLinqAsync), BindingFlags.NonPublic | BindingFlags.Instance)
                .MakeGenericMethod(entityType);

            return await (Task<IEnumerable<object>>)getPathMethod.Invoke(this,
            [
                context,
                entityConfiguration.ParentIdProperty,
                entityId, CancellationToken.None
            ]);
        }

        // Use the same type as the entity ID for the query
        var idType = entityConfiguration.ParentIdProperty.PropertyType;
        var method = typeof(RelationalDatabaseFacadeExtensions).GetMethod(nameof(RelationalDatabaseFacadeExtensions.SqlQueryRaw)).MakeGenericMethod(idType);

        // Execute the query and return the list of parent IDs
        var queryResult = method.Invoke(context.Database, [context.Database, query, new object[] { entityId }, CancellationToken.None]);
        var parentIds = ((IEnumerable<object>)queryResult).ToList();

        TypedLogger.LogFoundHierarchyPath(this.logger, Constants.LogKey, entityType.Name, entityId?.ToString(), parentIds.Count);

        return await Task.FromResult(parentIds.AsEnumerable()).AnyContext();
    }

    /// <inheritdoc/>
    public async Task GrantUserPermissionAsync(string userId, string entityType, object entityId, string permission, CancellationToken cancellationToken = default)
    {
        entityType = this.options?.GetEntityTypeConfiguration(entityType, false)?.EntityType?.FullName ?? entityType;

        using var scope = this.serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();

        TypedLogger.LogGrantingPermission(this.logger, Constants.LogKey, entityType, entityId?.ToString(), permission, userId);

        var existingPermission = await context.EntityPermissions
            .FirstOrDefaultAsync(p =>
                p.UserId == userId &&
                p.EntityType == entityType &&
                p.EntityId == (entityId != null ? entityId.ToString() : default) &&
                p.Permission == permission, cancellationToken: cancellationToken).AnyContext();

        if (existingPermission != null)
        {
            existingPermission.UpdatedDate = DateTimeOffset.UtcNow;
        }
        else
        {
            var date = DateTimeOffset.UtcNow;
            var entityPermission = new EntityPermission
            {
                UserId = userId,
                EntityType = entityType,
                EntityId = entityId?.ToString().EmptyToNull(),
                Permission = permission,
                CreatedDate = date,
                UpdatedDate = date
            };

            context.EntityPermissions.Add(entityPermission);
        }

        await context.SaveChangesAsync(cancellationToken).AnyContext();
        await this.InvalidatePermissionCachesAsync(userId, entityType, entityId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task RevokeUserPermissionAsync(string userId, string entityType, object entityId, string permission, CancellationToken cancellationToken = default)
    {
        entityType = this.options?.GetEntityTypeConfiguration(entityType, false)?.EntityType?.FullName ?? entityType;

        using var scope = this.serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();

        var entityPermission = await context.EntityPermissions
            .FirstOrDefaultAsync(p =>
                p.UserId == userId &&
                p.EntityType == entityType &&
                p.EntityId == (entityId != null ? entityId.ToString() : default) &&
                p.Permission == permission, cancellationToken: cancellationToken).AnyContext();

        if (entityPermission != null)
        {
            TypedLogger.LogRevokingPermission(this.logger, Constants.LogKey, entityType, entityId?.ToString(), permission, userId);
            context.EntityPermissions.Remove(entityPermission);
            await context.SaveChangesAsync(cancellationToken).AnyContext();
            await this.InvalidatePermissionCachesAsync(userId, entityType, entityId, cancellationToken).AnyContext();
        }
    }

    /// <inheritdoc/>
    public async Task RevokeUserPermissionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        using var scope = this.serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();

        var entityPermissions = await context.EntityPermissions
            .Where(p => p.UserId == userId).ToListAsync(cancellationToken: cancellationToken).AnyContext();
        foreach (var entityPermission in entityPermissions)
        {
            TypedLogger.LogRevokingPermission(this.logger, Constants.LogKey, entityPermission.EntityType, entityPermission.EntityId, entityPermission.Permission, userId);
            context.EntityPermissions.Remove(entityPermission);
            await this.InvalidatePermissionCachesAsync(userId, entityPermission.EntityType, entityPermission.EntityId, cancellationToken).AnyContext();
        }

        if (entityPermissions.Count != 0)
        {
            await context.SaveChangesAsync(cancellationToken).AnyContext();
        }
    }

    /// <inheritdoc/>
    public async Task GrantRolePermissionAsync(string roleName, string entityType, object entityId, string permission, CancellationToken cancellationToken = default)
    {
        entityType = this.options?.GetEntityTypeConfiguration(entityType, false)?.EntityType?.FullName ?? entityType;

        using var scope = this.serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();

        TypedLogger.LogGrantingRolePermission(this.logger, Constants.LogKey, entityType, entityId?.ToString(), permission, roleName);

        var existingPermission = await context.EntityPermissions
            .FirstOrDefaultAsync(p =>
                p.RoleName == roleName &&
                p.EntityType == entityType &&
                p.EntityId == (entityId != null ? entityId.ToString() : default) &&
                p.Permission == permission, cancellationToken: cancellationToken).AnyContext();

        if (existingPermission != null)
        {
            existingPermission.UpdatedDate = DateTimeOffset.UtcNow;
        }
        else
        {
            var date = DateTimeOffset.UtcNow;
            var entityPermission = new EntityPermission
            {
                RoleName = roleName,
                EntityType = entityType,
                EntityId = entityId?.ToString().EmptyToNull(),
                Permission = permission,
                CreatedDate = date,
                UpdatedDate = date
            };

            context.EntityPermissions.Add(entityPermission);
        }

        await context.SaveChangesAsync(cancellationToken).AnyContext();
        await this.InvalidateRolePermissionCachesAsync(roleName, entityType, entityId, cancellationToken).AnyContext();
    }

    /// <inheritdoc/>
    public async Task RevokeRolePermissionAsync(string roleName, string entityType, object entityId, string permission, CancellationToken cancellationToken = default)
    {
        entityType = this.options?.GetEntityTypeConfiguration(entityType, false)?.EntityType?.FullName ?? entityType;

        using var scope = this.serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();

        var entityPermission = await context.EntityPermissions
            .FirstOrDefaultAsync(p =>
                p.RoleName == roleName &&
                p.EntityType == entityType &&
                p.EntityId == (entityId != null ? entityId.ToString() : default) &&
                p.Permission == permission, cancellationToken: cancellationToken).AnyContext();

        if (entityPermission != null)
        {
            TypedLogger.LogRevokingRolePermission(this.logger, Constants.LogKey, entityType, entityId?.ToString(), permission, roleName);
            context.EntityPermissions.Remove(entityPermission);
            await context.SaveChangesAsync(cancellationToken).AnyContext();
            await this.InvalidateRolePermissionCachesAsync(roleName, entityType, entityId, cancellationToken).AnyContext();
        }
    }

    /// <inheritdoc/>
    public async Task RevokeRolePermissionsAsync(string roleName, CancellationToken cancellationToken = default)
    {
        using var scope = this.serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();

        var entityPermissions = await context.EntityPermissions
            .Where(p => p.RoleName == roleName).ToListAsync(cancellationToken: cancellationToken).AnyContext();
        foreach (var entityPermission in entityPermissions)
        {
            TypedLogger.LogRevokingRolePermission(this.logger, Constants.LogKey, entityPermission.EntityType, entityPermission.EntityId, entityPermission.Permission, roleName);
            context.EntityPermissions.Remove(entityPermission);
            await this.InvalidateRolePermissionCachesAsync(roleName, entityPermission.EntityType, entityPermission.EntityId, cancellationToken).AnyContext();
        }

        if (entityPermissions.Count != 0)
        {
            await context.SaveChangesAsync(cancellationToken).AnyContext();
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<string>> GetUserPermissionsAsync(
        string userId,
        string entityType,
        object entityId, CancellationToken cancellationToken = default)
    {
        entityType = this.options?.GetEntityTypeConfiguration(entityType, false)?.EntityType?.FullName ?? entityType;

        using var scope = this.serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();

        // Get direct user permissions
        var safeEntityId = entityId?.ToString().EmptyToNull();
        var userPermissions = await context.EntityPermissions
            .Where(p => p.UserId == userId
                && p.EntityType == entityType
                && (p.EntityId == null || p.EntityId == safeEntityId))
            .Select(p => p.Permission)
            .ToListAsync(cancellationToken: cancellationToken).AnyContext();

        // Get wildcard permissions (entityId == null)
        var wildcardPermissions = await context.EntityPermissions
            .Where(p => p.UserId == userId
                && p.EntityType == entityType
                && p.EntityId == null)
            .Select(p => p.Permission)
            .ToListAsync(cancellationToken: cancellationToken).AnyContext();

        return [.. userPermissions.Union(wildcardPermissions)]; // Combine and deduplicate
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<string>> GetRolePermissionsAsync(
        string roleName,
        string entityType,
        object entityId, CancellationToken cancellationToken = default)
    {
        entityType = this.options?.GetEntityTypeConfiguration(entityType, false)?.EntityType?.FullName ?? entityType;

        using var scope = this.serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();

        // Get direct role permissions
        var safeEntityId = entityId?.ToString().EmptyToNull();
        var rolePermissions = await context.EntityPermissions
            .Where(p => p.RoleName == roleName
                && p.EntityType == entityType
                && (p.EntityId == null || p.EntityId == safeEntityId))
            .Select(p => p.Permission)
            .ToListAsync(cancellationToken: cancellationToken).AnyContext();

        // Get wildcard permissions (entityId == null)
        var wildcardPermissions = await context.EntityPermissions
            .Where(p => p.RoleName == roleName
                && p.EntityType == entityType
                && p.EntityId == null)
            .Select(p => p.Permission)
            .ToListAsync(cancellationToken: cancellationToken).AnyContext();

        return [.. rolePermissions.Union(wildcardPermissions)]; // Combine and deduplicate
    }

    private async Task InvalidatePermissionCachesAsync(string userId, string entityType, object entityId, CancellationToken cancellationToken = default)
    {
        if (this.cacheProvider == null)
        {
            return;
        }

        TypedLogger.LogInvalidatingCaches(this.logger, Constants.LogKey, entityType, entityId?.ToString(), userId);

        // Invalidate specific entity permission cache
        if (entityId != null)
        {
            var entityCacheKey = EntityPermissionCacheKeys.ForUserEntity(userId, entityType, entityId);
            await this.cacheProvider.RemoveAsync(entityCacheKey, cancellationToken);
        }

        // Invalidate all entity type permissions for user (includes wildcards)
        var typeCacheKey = EntityPermissionCacheKeys.ForUserEntityType(userId, entityType);
        await this.cacheProvider.RemoveStartsWithAsync(typeCacheKey, cancellationToken);

        // If it's a wildcard permission, invalidate all entity caches of that type
        if (entityId == null)
        {
            var wildcardKey = EntityPermissionCacheKeys.PatternForEntityType(entityType);
            await this.cacheProvider.RemoveStartsWithAsync(wildcardKey, cancellationToken);
        }
    }

    private async Task InvalidateRolePermissionCachesAsync(string roleName, string entityType, object entityId, CancellationToken cancellationToken = default)
    {
        if (this.cacheProvider == null)
        {
            return;
        }

        TypedLogger.LogInvalidatingRoleCaches(this.logger, Constants.LogKey, entityType, entityId?.ToString(), roleName);

        // For role permissions, we need to invalidate all user caches as we don't know which users have this role
        var typeKey = EntityPermissionCacheKeys.PatternForEntityType(entityType);
        await this.cacheProvider.RemoveStartsWithAsync(typeKey, cancellationToken).AnyContext();
    }

    private async Task<IEnumerable<object>> GetHierarchyPathLinqAsync<TEntity>(
        DbContext context,
        PropertyInfo parentIdProperty,
        object entityId, CancellationToken cancellationToken = default)
        where TEntity : class, IEntity
    {
        var path = new List<object>();
        var currentId = entityId;
        var visitedIds = new HashSet<object>();

        while (currentId != null)
        {
            // Prevent circular references
            if (!visitedIds.Add(currentId))
            {
                // If we can't add the ID because it's already in the set, we've found a circle
                TypedLogger.LogCircularHierarchyDetected(this.logger, Constants.LogKey, typeof(TEntity).Name, currentId.ToString());
                break;
            }

            var entity = await context.Set<TEntity>()
                .FirstOrDefaultAsync(e => e.Id.Equals(currentId), cancellationToken);

            if (entity == null)
            {
                break;
            }

            var parentId = parentIdProperty.GetValue(entity);
            if (parentId == null || parentId.Equals(default))
            {
                break;
            }

            path.Add(parentId);
            currentId = parentId;
        }

        return path;
    }

    public static partial class TypedLogger
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "{LogKey} permission provider - checking permission: {EntityType}/{EntityId}, permission={Permission}, user={UserId}")]
        public static partial void LogCheckingPermission(ILogger logger, string logKey, string entityType, string entityId, string permission, string userId);

        [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "{LogKey} permission provider - checking wildcard permission: {EntityType}, permission={Permission}, user={UserId}")]
        public static partial void LogCheckingWildcardPermission(ILogger logger, string logKey, string entityType, string permission, string userId);

        [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "{LogKey} permission provider - getting entity ids with permission: {EntityType}, permission={Permission}, user={UserId}")]
        public static partial void LogGettingEntityIds(ILogger logger, string logKey, string entityType, string permission, string userId);

        [LoggerMessage(EventId = 4, Level = LogLevel.Debug, Message = "{LogKey} permission provider - wildcard permission found: {EntityType}, permission={Permission}, user={UserId}")]
        public static partial void LogWildcardPermissionFound(ILogger logger, string logKey, string entityType, string permission, string userId);

        [LoggerMessage(EventId = 5, Level = LogLevel.Debug, Message = "{LogKey} permission provider - found {Count} entity ids with permission: {EntityType}, permission={Permission}, user={UserId}")]
        public static partial void LogFoundEntityIds(ILogger logger, string logKey, string entityType, string permission, string userId, int count);

        [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "{LogKey} permission provider - getting hierarchy path: {EntityType}/{EntityId}")]
        public static partial void LogGettingHierarchyPath(ILogger logger, string logKey, string entityType, string entityId);

        [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "{LogKey} permission provider - found hierarchy path: {EntityType}/{EntityId}, count={Count}")]
        public static partial void LogFoundHierarchyPath(ILogger logger, string logKey, string entityType, string entityId, int count);

        [LoggerMessage(EventId = 10, Level = LogLevel.Debug, Message = "{LogKey} permission provider - granting permission: {EntityType}/{EntityId}, permission={Permission}, user={UserId}")]
        public static partial void LogGrantingPermission(ILogger logger, string logKey, string entityType, string entityId, string permission, string userId);

        [LoggerMessage(EventId = 11, Level = LogLevel.Debug, Message = "{LogKey} permission provider - revoking permission: {EntityType}/{EntityId}, permission={Permission}, user={UserId}")]
        public static partial void LogRevokingPermission(ILogger logger, string logKey, string entityType, string entityId, string permission, string userId);

        [LoggerMessage(EventId = 12, Level = LogLevel.Debug, Message = "{LogKey} permission provider - granting role permission: {EntityType}/{EntityId}, permission={Permission}, role={RoleName}")]
        public static partial void LogGrantingRolePermission(ILogger logger, string logKey, string entityType, string entityId, string permission, string roleName);

        [LoggerMessage(EventId = 13, Level = LogLevel.Debug, Message = "{LogKey} permission provider - revoking role permission: {EntityType}/{EntityId}, permission={Permission}, role={RoleName}")]
        public static partial void LogRevokingRolePermission(ILogger logger, string logKey, string entityType, string entityId, string permission, string roleName);

        [LoggerMessage(EventId = 14, Level = LogLevel.Debug, Message = "{LogKey} permission provider - invalidating permission caches: {EntityType}/{EntityId}, user={UserId}")]
        public static partial void LogInvalidatingCaches(ILogger logger, string logKey, string entityType, string entityId, string userId);

        [LoggerMessage(EventId = 15, Level = LogLevel.Debug, Message = "{LogKey} permission provider - invalidating role permission caches: {EntityType}/{EntityId}, role={RoleName}")]
        public static partial void LogInvalidatingRoleCaches(ILogger logger, string logKey, string entityType, string entityId, string roleName);

        [LoggerMessage(EventId = 16, Level = LogLevel.Warning, Message = "{LogKey} permission provider - circular hierarchy detected for {EntityType} with id {EntityId}")]
        public static partial void LogCircularHierarchyDetected(ILogger logger, string logKey, string entityType, string entityId);
    }
}