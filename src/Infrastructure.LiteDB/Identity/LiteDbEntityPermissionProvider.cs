// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using System.Text;
using System.Text.Json;
using BridgingIT.DevKit.Application.Identity;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Infrastructure.LiteDb.Repositories;

/// <summary>Persists exact user and role grants while keeping authorization evaluation in the existing Identity feature.</summary>
/// <param name="database">The named native permission namespace.</param>
/// <param name="options">The existing entity and evaluator configuration.</param>
/// <param name="collection">Resolves the selected database's registered entity collection.</param>
/// <param name="session">The caller's scoped native session, including an active entity transaction.</param>
/// <param name="services">The caller's scoped services.</param>
/// <param name="cache">Optional existing permission-result cache.</param>
/// <example><code>services.AddLiteDbEntityPermissions(options => options.AddEntity&lt;Ledger&gt;(Permission.Read));</code></example>
public sealed class LiteDbEntityPermissionProvider(LiteDbFeatureDatabase database, EntityPermissionOptions options,
    Func<Type, string> collection, LiteDbDatabaseSession session, IServiceProvider services, ICacheProvider cache = null) : IEntityPermissionProvider
{
    private readonly LiteDbKeyedCollection<string, EntityPermissionInfo> grants = database.Collection<string, EntityPermissionInfo>("grants");

    /// <inheritdoc />
    public async Task<bool> HasPermissionAsync(string userId, string[] roles, string entityType, object entityId, string permission, CancellationToken cancellationToken = default) =>
        (await this.ReadAsync(entityType, row => row.Permission == permission && (row.EntityId is null || row.EntityId == Id(entityId)) && Principal(row, userId, roles), cancellationToken).ConfigureAwait(false)).Length > 0;

    /// <inheritdoc />
    public async Task<bool> HasPermissionAsync(string userId, string[] roles, string entityType, string permission, CancellationToken cancellationToken = default) =>
        (await this.ReadAsync(entityType, row => row.EntityId is null && row.Permission == permission && Principal(row, userId, roles), cancellationToken).ConfigureAwait(false)).Length > 0;

    /// <inheritdoc />
    public async Task<IEnumerable<string>> GetEntityIdsWithPermissionAsync(string userId, string[] roles, string entityType, string permission, CancellationToken cancellationToken = default)
    {
        var selected = await this.ReadAsync(entityType, row => row.Permission == permission && Principal(row, userId, roles), cancellationToken).ConfigureAwait(false);
        return selected.Any(row => row.EntityId is null) ? [] : selected.Select(row => row.EntityId).Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <inheritdoc />
    public Task<IEnumerable<object>> GetHierarchyPathAsync(Type entityType, object entityId, CancellationToken cancellationToken = default)
    {
        var configuration = options.GetEntityTypeConfiguration(entityType, false);
        if (!options.EnableHierarchicalPermissions || configuration?.ParentIdProperty is null || entityId is null) return Task.FromResult<IEnumerable<object>>([]);
        var identity = new LiteDbEntityIdentity(configuration.IdType);
        return this.InspectAsync<IEnumerable<object>>(db =>
        {
            var entries = db.GetCollection<BsonDocument>(collection(entityType));
            var path = new List<object>();
            var visited = new HashSet<BsonValue>();
            var current = entityId;
            while (current is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var nativeId = identity.ToBson(current);
                if (!visited.Add(nativeId)) break;
                if (path.Count >= database.Options.MaxMaterializedRows) throw new LiteDbProviderException(new InvalidOperationError("The permission hierarchy exceeds its traversal bound."));
                var row = entries.FindById(nativeId);
                if (row is null) break;
                var parent = configuration.ParentIdProperty.GetValue(db.Mapper.ToObject(entityType, row));
                if (parent is null || visited.Contains(identity.ToBson(parent))) break;
                path.Add(parent);
                current = parent;
            }

            return path.ToArray();
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task GrantUserPermissionAsync(string userId, string entityType, object entityId, string permission, CancellationToken cancellationToken = default) => this.ChangeAsync(userId, null, entityType, entityId, permission, false, cancellationToken);

    /// <inheritdoc />
    public Task GrantRolePermissionAsync(string role, string entityType, object entityId, string permission, CancellationToken cancellationToken = default) => this.ChangeAsync(null, role, entityType, entityId, permission, false, cancellationToken);

    /// <inheritdoc />
    public Task RevokeUserPermissionAsync(string userId, string entityType, object entityId, string permission, CancellationToken cancellationToken = default) => this.ChangeAsync(userId, null, entityType, entityId, permission, true, cancellationToken);

    /// <inheritdoc />
    public Task RevokeRolePermissionAsync(string role, string entityType, object entityId, string permission, CancellationToken cancellationToken = default) => this.ChangeAsync(null, role, entityType, entityId, permission, true, cancellationToken);

    /// <inheritdoc />
    public Task RevokeUserPermissionsAsync(string userId, CancellationToken cancellationToken = default) => this.RevokeAllAsync(userId, null, cancellationToken);

    /// <inheritdoc />
    public Task RevokeRolePermissionsAsync(string role, CancellationToken cancellationToken = default) => this.RevokeAllAsync(null, role, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<EntityPermissionInfo>> GetPermissionsAsync(string entityType, object entityId, CancellationToken cancellationToken = default) =>
        await this.ReadAsync(entityType, row => entityId is null || row.EntityId is null || row.EntityId == Id(entityId), cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<EntityPermissionInfo>> GetUsersPermissionsAsync(string entityType, object entityId, CancellationToken cancellationToken = default) =>
        await this.ReadAsync(entityType, row => row.UserId is not null && (row.EntityId is null || row.EntityId == Id(entityId)), cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<EntityPermissionInfo>> GetRolesPermissionsAsync(string entityType, object entityId, CancellationToken cancellationToken = default) =>
        await this.ReadAsync(entityType, row => row.RoleName is not null && (row.EntityId is null || row.EntityId == Id(entityId)), cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<string>> GetUserPermissionsAsync(string userId, string entityType, object entityId, CancellationToken cancellationToken = default) =>
        (await this.ReadAsync(entityType, row => row.UserId == userId && (row.EntityId is null || row.EntityId == Id(entityId)), cancellationToken).ConfigureAwait(false)).Select(row => row.Permission).Distinct(StringComparer.Ordinal).ToArray();

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<string>> GetRolePermissionsAsync(string role, string entityType, object entityId, CancellationToken cancellationToken = default) =>
        (await this.ReadAsync(entityType, row => row.RoleName == role && (row.EntityId is null || row.EntityId == Id(entityId)), cancellationToken).ConfigureAwait(false)).Select(row => row.Permission).Distinct(StringComparer.Ordinal).ToArray();

    private async Task ChangeAsync(string user, string role, string type, object id, string permission, bool remove, CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(user ?? role);
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        type = this.TypeName(type);
        await this.ExecuteAsync(() =>
        {
            var row = new EntityPermissionInfo { UserId = user, RoleName = role, EntityType = type, EntityId = Id(id), Permission = permission };
            var key = JsonSerializer.Serialize(new[] { type, row.EntityId, user, role, permission });
            if (remove) this.grants.Remove(key);
            else this.grants[key] = row;
            return Task.CompletedTask;
        }, token).ConfigureAwait(false);
        await this.InvalidateCacheAsync().ConfigureAwait(false);
    }

    private async Task RevokeAllAsync(string user, string role, CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(user ?? role);
        await this.ExecuteAsync(() =>
        {
            foreach (var row in this.grants.Where(pair => user is not null ? pair.Value.UserId == user : pair.Value.RoleName == role).ToArray()) this.grants.Remove(row.Key);
            return Task.CompletedTask;
        }, token).ConfigureAwait(false);
        await this.InvalidateCacheAsync().ConfigureAwait(false);
    }

    private Task<EntityPermissionInfo[]> ReadAsync(string entityType, Func<EntityPermissionInfo, bool> predicate, CancellationToken token) => this.ExecuteAsync(() =>
    {
        var type = this.TypeName(entityType);
        return Task.FromResult(this.grants.Values.Where(row => row.EntityType == type && predicate(row)).ToArray());
    }, token);

    private Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken token) => database.ExecuteInSessionAsync(session, services, action, token);

    private Task ExecuteAsync(Func<Task> action, CancellationToken token) => this.ExecuteAsync(() =>
    {
        action().GetAwaiter().GetResult();
        return Task.FromResult(true);
    }, token);

    private async Task<T> InspectAsync<T>(Func<LiteDatabase, T> action, CancellationToken token)
    {
        var result = await session.ExecuteAsync(async () => Result<T>.Success(await session.NativeAsync(action, token).ConfigureAwait(false)), false, token).ConfigureAwait(false);
        if (result.IsFailure) throw new LiteDbProviderException(result);
        return result.Value;
    }

    // Successful native mutations must finish cache eviction even if the caller cancels after the write.
    private Task InvalidateCacheAsync() => !options.EnableCaching ? Task.CompletedTask :
        cache?.RemoveStartsWithAsync(EntityPermissionCacheKeys.Prefix + ":user:", CancellationToken.None) ?? Task.CompletedTask;

    private string TypeName(string value) { ArgumentException.ThrowIfNullOrWhiteSpace(value); return options.GetEntityTypeConfiguration(value, false)?.EntityType.FullName ?? value; }

    private static string Id(object value) => string.IsNullOrEmpty(value?.ToString()) ? null : value.ToString();

    private static bool Principal(EntityPermissionInfo row, string user, string[] roles) => row.UserId is not null && row.UserId == user || row.RoleName is not null && roles?.Contains(row.RoleName, StringComparer.Ordinal) == true;
}
