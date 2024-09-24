// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using EntityFrameworkCore;
using EntityFrameworkCore.Database.Command;
using EntityFrameworkCore.Diagnostics;
using EntityFrameworkCore.Infrastructure;
using Extensions;
using Logging;

public static class ServiceCollectionExtensions
{
    public static CosmosDbContextBuilderContext<TContext> AddCosmosDbContext<TContext>(
        this IServiceCollection services,
        Builder<CosmosOptionsBuilder, CosmosOptions> optionsBuilder,
        Action<CosmosDbContextOptionsBuilder> cosmosOptionsBuilder = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TContext : DbContext
    {
        return services.AddCosmosDbContext<TContext>(optionsBuilder(new CosmosOptionsBuilder()).Build(),
            cosmosOptionsBuilder,
            lifetime);
    }

    public static CosmosDbContextBuilderContext<TContext> AddCosmosDbContext<TContext>(
        this IServiceCollection services,
        CosmosOptions options,
        Action<CosmosDbContextOptionsBuilder> cosmosOptionsBuilder = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TContext : DbContext
    {
        EnsureArg.IsNotNull(options, nameof(options));
        EnsureArg.IsNotNullOrEmpty(options.ConnectionString, nameof(options.ConnectionString));
        EnsureArg.IsNotNullOrEmpty(options.Database, nameof(options.Database));

        if (options.CommandLoggerEnabled)
        {
            options.InterceptorTypes.Add(typeof(CommandLoggerInterceptor));
        }

        RegisterInterceptors(services, options, lifetime);

        services.AddDbContext<TContext>((sp, o) =>
            {
                if (options.InterceptorTypes.SafeAny())
                {
                    o.AddInterceptors(options.InterceptorTypes.Select(i => sp.GetRequiredService(i) as IInterceptor));
                }

                o.UseCosmos(options.ConnectionString, options.Database, cosmosOptionsBuilder);

                if (options.LoggerEnabled)
                {
                    o.UseLoggerFactory(services.BuildServiceProvider().GetRequiredService<ILoggerFactory>());
                    o.EnableSensitiveDataLogging(options.SensitiveDataLoggingEnabled);
                }

                if (options.SimpleLoggerEnabled)
                {
                    // https://learn.microsoft.com/en-us/ef/core/logging-events-diagnostics/simple-logging
                    o.LogTo(Console.WriteLine,
                        new[] { DbLoggerCategory.Database.Command.Name },
                        options.SimpleLoggerLevel,
                        DbContextLoggerOptions.SingleLine | DbContextLoggerOptions.Level);
                }

                o.EnableDetailedErrors(options.DetailedErrorsEnabled);

                if (options.MemoryCache is not null)
                {
                    o.UseMemoryCache(options.MemoryCache);
                }
            },
            lifetime);

        return new CosmosDbContextBuilderContext<TContext>(services,
            lifetime,
            connectionString: options.ConnectionString,
            provider: Provider.SqlServer);
    }

    public static CosmosDbContextBuilderContext<TContext> AddCosmosDbContext<TContext>(
        this IServiceCollection services,
        string connectionString,
        string database = "master",
        Action<CosmosDbContextOptionsBuilder> cosmosOptionsBuilder = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TContext : DbContext
    {
        EnsureArg.IsNotNullOrEmpty(connectionString, nameof(connectionString));
        EnsureArg.IsNotNullOrEmpty(database, nameof(database));

        services.AddDbContext<TContext>((sp, o) => o.UseCosmos(connectionString, database, cosmosOptionsBuilder),
            lifetime);

        return new CosmosDbContextBuilderContext<TContext>(services,
            lifetime,
            connectionString: connectionString,
            provider: Provider.SqlServer);
    }

    private static void RegisterInterceptors(
        IServiceCollection services,
        CosmosOptions options,
        ServiceLifetime lifetime)
    {
        foreach (var interceptorType in options.InterceptorTypes.SafeNull())
        {
            switch (lifetime)
            {
                case ServiceLifetime.Singleton:
                    services.TryAddSingleton(interceptorType);
                    break;
                case ServiceLifetime.Transient:
                    services.TryAddTransient(interceptorType);
                    break;
                default:
                    services.TryAddScoped(interceptorType);
                    break;
            }
        }
    }
}