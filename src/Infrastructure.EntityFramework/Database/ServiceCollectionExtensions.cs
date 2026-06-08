// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Hosting;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabaseCreatorService<TContext>(
        this IServiceCollection services,
        Builder<DatabaseCreatorOptionsBuilder, DatabaseCreatorOptions> optionsBuilder)
        where TContext : DbContext
    {
        return services.AddDatabaseCreatorService<TContext>(
            optionsBuilder(new DatabaseCreatorOptionsBuilder()).Build());
    }

    public static IServiceCollection AddDatabaseCreatorService<TContext>(
        this IServiceCollection services,
        DatabaseCreatorOptions options = null)
        where TContext : DbContext
    {
        services.TryAddDatabaseReadyHealthCheck();
        if (!EnvironmentExtensions.IsBuildTimeOpenApiGeneration()) // avoid hosted service during build-time openapi generation
        {
            services.AddHostedService(sp =>
            new DatabaseCreatorService<TContext>(
                sp.GetRequiredService<ILoggerFactory>(),
                sp.GetRequiredService<IHostApplicationLifetime>(),
                sp,
                sp.GetService<IDatabaseReadyService>(),
                options));
            services.TryAddBackgroundServiceHealthCheck<DatabaseCreatorService<TContext>>(
                $"DatabaseCreatorService-{typeof(TContext).Name}",
                tags: ["background", "database", "entityframework"]);
        }

        return services;
    }

    public static IServiceCollection AddDatabaseMigratorService<TContext>(
        this IServiceCollection services,
        Builder<DatabaseMigratorOptionsBuilder, DatabaseMigratorOptions> optionsBuilder)
        where TContext : DbContext
    {
        return services.AddDatabaseMigratorService<TContext>(optionsBuilder(
            new DatabaseMigratorOptionsBuilder()).Build());
    }

    public static IServiceCollection AddDatabaseMigratorService<TContext>(
        this IServiceCollection services,
        DatabaseMigratorOptions options = null)
        where TContext : DbContext
    {
        services.TryAddDatabaseReadyHealthCheck();

        if (!EnvironmentExtensions.IsBuildTimeOpenApiGeneration()) // avoid hosted service during build-time openapi generation
        {
            services.AddHostedService(sp =>
            new DatabaseMigratorService<TContext>(
                sp.GetRequiredService<ILoggerFactory>(),
                sp.GetRequiredService<IHostApplicationLifetime>(),
                sp,
                sp.GetService<IDatabaseReadyService>(),
                options));
            services.TryAddBackgroundServiceHealthCheck<DatabaseMigratorService<TContext>>(
                $"DatabaseMigratorService-{typeof(TContext).Name}",
                tags: ["background", "database", "entityframework"]);
        }

        return services;
    }

    public static IServiceCollection AddDatabaseCheckerService<TContext>(
        this IServiceCollection services,
        DatabaseCheckerOptions options = null)
        where TContext : DbContext
    {
        services.TryAddDatabaseReadyHealthCheck();

        if (!EnvironmentExtensions.IsBuildTimeOpenApiGeneration()) // avoid hosted service during build-time openapi generation
        {
            services.AddHostedService(sp =>
            new DatabaseCheckerService<TContext>(
                sp.GetRequiredService<ILoggerFactory>(),
                sp.GetRequiredService<IHostApplicationLifetime>(),
                sp,
                sp.GetService<IDatabaseReadyService>(),
                options));
            services.TryAddBackgroundServiceHealthCheck<DatabaseCheckerService<TContext>>(
                $"DatabaseCheckerService-{typeof(TContext).Name}",
                tags: ["background", "database", "entityframework"]);
        }

        return services;
    }

    public static IServiceCollection AddDbContextRegistration<TContext>(
        this IServiceCollection services,
        Provider provider)
        where TContext : DbContext
    {
        services.AddSingleton(new DbContextRegistration(
            typeof(TContext),
            provider.ToString(),
            typeof(TContext).Name));

        return services;
    }
}
