// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Registers shared LiteDB database ownership.</summary>
public static class LiteDbDatabaseServiceCollectionExtensions
{
    /// <summary>Registers the default database. Callers own any upstream development-risk acknowledgement.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The database settings.</param>
    /// <returns>The service collection.</returns>
    /// <example><code>services.AddLiteDbDatabase(o => o.ConnectionString = "/data/ledger.db");</code></example>
    public static IServiceCollection AddLiteDbDatabase(this IServiceCollection services, Action<LiteDbDatabaseOptions> configure) =>
        services.AddLiteDbDatabase("default", configure);

    /// <summary>Registers a named database with one native executor and scoped transaction sessions.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The unique registration name.</param>
    /// <param name="configure">The database settings.</param>
    /// <returns>The service collection.</returns>
    /// <example><code>services.AddLiteDbDatabase("archive", o => o.ConnectionString = "/data/archive.db");</code></example>
    public static IServiceCollection AddLiteDbDatabase(this IServiceCollection services, string name, Action<LiteDbDatabaseOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        var options = new LiteDbDatabaseOptions();
        configure(options);
        var registry = LiteDbDatabaseRegistry.GetOrAdd(services);
        registry.Add(name, options);
        services.AddKeyedSingleton(name, (sp, _) => new LiteDbDatabaseOwner(registry.Get(name)));
        services.AddKeyedScoped(name, (sp, _) => new LiteDbDatabaseSession(sp.GetRequiredKeyedService<LiteDbDatabaseOwner>(name),
            () => sp.GetKeyedServices<ILiteDbEntityMutationObserver>(name)));
        return services;
    }
}
