// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabaseCreatorService<TContext>(
        this IServiceCollection services,
        Builder<DatabaseCreatorOptionsBuilder, DatabaseCreatorOptions> optionsBuilder)
        where TContext : DbContext
    {
        return services
            .AddDatabaseCreatorService<TContext>(
                optionsBuilder(new DatabaseCreatorOptionsBuilder()).Build());
    }

    public static IServiceCollection AddDatabaseCreatorService<TContext>(
        this IServiceCollection services,
        DatabaseCreatorOptions options = null)
        where TContext : DbContext
    {
        services.AddHostedService(sp =>
            new DatabaseCreatorService<TContext>(
                sp.GetRequiredService<ILoggerFactory>(), sp,
                options));

        return services;
    }

    public static IServiceCollection AddDatabaseMigratorService<TContext>(
        this IServiceCollection services,
        Builder<DatabaseMigratorOptionsBuilder, DatabaseMigratorOptions> optionsBuilder)
        where TContext : DbContext
    {
        return services
            .AddDatabaseMigratorService<TContext>(
                optionsBuilder(new DatabaseMigratorOptionsBuilder()).Build());
    }

    public static IServiceCollection AddDatabaseMigratorService<TContext>(
        this IServiceCollection services,
        DatabaseMigratorOptions options = null)
        where TContext : DbContext
    {
        services.AddHostedService(sp =>
            new DatabaseMigratorService<TContext>(
                sp.GetRequiredService<ILoggerFactory>(), sp,
                options));

        return services;
    }
}
