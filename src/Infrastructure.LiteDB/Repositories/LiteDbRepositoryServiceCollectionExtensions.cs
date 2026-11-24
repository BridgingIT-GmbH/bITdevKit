// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using BridgingIT.DevKit.Infrastructure.LiteDb.Repositories;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Selects LiteDB repositories and explicit bulk inserters through the existing DevKit builders.</summary>
public static class LiteDbRepositoryServiceCollectionExtensions
{
    /// <summary>Registers a scoped repository, read-only alias, and awaited transaction adapter.</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="databaseName">The named database registration.</param>
    /// <param name="configure">Optional repository configuration.</param>
    /// <returns>The existing repository behavior builder.</returns>
    /// <example><code>services.AddLiteDbRepository&lt;LedgerEntry&gt;().WithBehavior&lt;RepositoryLoggingBehavior&lt;LedgerEntry&gt;&gt;();</code></example>
    public static RepositoryBuilderContext<TEntity> AddLiteDbRepository<TEntity>(this IServiceCollection services,
        string databaseName = "default", Action<LiteDbRepositoryOptions> configure = null) where TEntity : class, IEntity
    {
        var options = Configure<TEntity>(services, databaseName, configure);
        services.AddScoped<IGenericRepository<TEntity>>(sp => new LiteDbGenericRepository<TEntity>(WithSession(options, sp, databaseName)));
        services.AddScoped<IGenericReadOnlyRepository<TEntity>>(sp => sp.GetRequiredService<IGenericRepository<TEntity>>());
        services.TryAddScoped<IRepositoryTransaction<TEntity>>(sp => new LiteDbRepositoryTransaction<TEntity>(sp.GetRequiredKeyedService<LiteDbDatabaseSession>(databaseName)));
        return new RepositoryBuilderContext<TEntity>(services);
    }

    /// <summary>Registers an explicit atomic bulk inserter without adding auditing, concurrency, or event behaviors.</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="databaseName">The named database registration.</param>
    /// <param name="configure">Optional collection and row-limit configuration.</param>
    /// <returns>The existing explicit bulk-inserter behavior builder.</returns>
    public static EntityBulkInserterBuilderContext<TEntity> AddLiteDbEntityBulkInserter<TEntity>(this IServiceCollection services,
        string databaseName = "default", Action<LiteDbRepositoryOptions> configure = null) where TEntity : class, IEntity
    {
        var options = Configure<TEntity>(services, databaseName, configure);
        services.AddScoped<IEntityBulkInserter<TEntity>>(sp => new LiteDbEntityBulkInserter<TEntity>(WithSession(options, sp, databaseName)));
        return new EntityBulkInserterBuilderContext<TEntity>(services);
    }

    private static LiteDbRepositoryOptions Configure<TEntity>(IServiceCollection services, string databaseName, Action<LiteDbRepositoryOptions> configure)
        where TEntity : class, IEntity
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = new LiteDbRepositoryOptions();
        configure?.Invoke(options);
        var identity = new LiteDbEntityIdentity(typeof(TEntity).GetProperty("Id")!.PropertyType);
        options.CollectionName ??= "repo_" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(GetTypeIdentity(typeof(TEntity)))))[..32];
        if (!Regex.IsMatch(options.CollectionName, "^[A-Za-z_][A-Za-z0-9_]{0,59}$", RegexOptions.CultureInvariant)) { throw new ArgumentException("The collection name is invalid."); }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxMaterializedRows);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(options.MaxMaterializedRows, int.MaxValue);
        LiteDbDatabaseRegistry.GetOrAdd(services).Get(databaseName).AddEntity(typeof(TEntity), options.CollectionName, identity.Configure, "Repositories");
        return new LiteDbRepositoryOptions
        {
            CollectionName = options.CollectionName, MaxMaterializedRows = options.MaxMaterializedRows,
            EnableOptimisticConcurrency = options.EnableOptimisticConcurrency, Autosave = options.Autosave, Mapper = options.Mapper
        };
    }

    private static string GetTypeIdentity(Type type)
    {
        var name = type.IsArray ? $"{GetTypeIdentity(type.GetElementType()!)}[{new string(',', type.GetArrayRank() - 1)}]"
            : type.IsGenericType ? $"{type.GetGenericTypeDefinition().FullName}[{string.Join(',', type.GenericTypeArguments.Select(GetTypeIdentity))}]"
            : type.FullName;
        return $"{type.Assembly.GetName().Name}:{name}";
    }

    private static LiteDbRepositoryOptions WithSession(LiteDbRepositoryOptions options, IServiceProvider services, string databaseName) => new()
    {
        Session = services.GetRequiredKeyedService<LiteDbDatabaseSession>(databaseName), CollectionName = options.CollectionName,
        MaxMaterializedRows = options.MaxMaterializedRows, EnableOptimisticConcurrency = options.EnableOptimisticConcurrency,
        Autosave = options.Autosave, Mapper = options.Mapper
    };
}
