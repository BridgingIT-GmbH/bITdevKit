// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>Selects LiteDB persistence through the existing ActiveEntity configurator.</summary>
public static class ActiveEntityProviderConfiguratorExtensions
{
    /// <summary>Registers a scoped provider sharing the selected database registration.</summary>
    /// <typeparam name="TEntity">The stored entity type.</typeparam>
    /// <typeparam name="TId">The entity identifier type.</typeparam>
    /// <param name="configurator">The existing entity configurator.</param>
    /// <param name="configure">Optional provider settings, frozen at registration.</param>
    /// <returns>The configurator for adding existing behaviors.</returns>
    /// <example><code>configuration.For&lt;LedgerEntry, Guid&gt;().UseLiteDbProvider(o => o.CollectionName = "ledger");</code></example>
    public static ActiveEntityConfigurator<TEntity, TId> UseLiteDbProvider<TEntity, TId>(
        this ActiveEntityConfigurator<TEntity, TId> configurator,
        Action<LiteDbActiveEntityProviderOptions<TEntity>> configure = null)
        where TEntity : class, IEntity
    {
        ArgumentNullException.ThrowIfNull(configurator);
        var options = new LiteDbActiveEntityProviderOptions<TEntity>();
        configure?.Invoke(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.DatabaseName);
        ArgumentNullException.ThrowIfNull(options.VersionGenerator);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxMaterializedRows);
        if (options.MaxMaterializedRows == int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(configure), "The row limit must leave room for an overflow probe.");
        }

        var identity = GetTypeIdentity(typeof(TEntity));
        options.CollectionName ??= "ae_" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..32];
        if (!Regex.IsMatch(options.CollectionName, "^[A-Za-z_][A-Za-z0-9_]{0,59}$", RegexOptions.CultureInvariant))
        {
            throw new ArgumentException("A collection name must contain at most 60 letters, digits, or underscores and start with a letter or underscore.");
        }

        var registry = LiteDbDatabaseRegistry.GetOrAdd(configurator.Services);
        registry.Get(options.DatabaseName).AddEntity(typeof(TEntity), options.CollectionName, LiteDbActiveEntityMapping<TId>.Configure);
        // Snapshot settings so retaining the configure callback's instance cannot mutate a live provider.
        var frozen = new LiteDbActiveEntityProviderOptions<TEntity>
        {
            DatabaseName = options.DatabaseName, CollectionName = options.CollectionName,
            IdGenerator = options.IdGenerator, EnableOptimisticConcurrency = options.EnableOptimisticConcurrency,
            VersionGenerator = options.VersionGenerator, MaxMaterializedRows = options.MaxMaterializedRows
        };
        return configurator.UseProviderFactory(sp => new LiteDbActiveEntityProvider<TEntity, TId>(
            sp.GetRequiredKeyedService<LiteDbDatabaseSession>(frozen.DatabaseName), frozen,
            sp.GetService<ILogger<LiteDbActiveEntityProvider<TEntity, TId>>>()));
    }

    private static string GetTypeIdentity(Type type)
    {
        // CLR FullName includes assembly versions inside closed generic arguments.
        var name = type.IsArray ? $"{GetTypeIdentity(type.GetElementType()!)}[{new string(',', type.GetArrayRank() - 1)}]"
            : type.IsGenericType ? $"{type.GetGenericTypeDefinition().FullName}[{string.Join(',', type.GenericTypeArguments.Select(GetTypeIdentity))}]"
            : type.FullName;
        return $"{type.Assembly.GetName().Name}:{name}";
    }
}
