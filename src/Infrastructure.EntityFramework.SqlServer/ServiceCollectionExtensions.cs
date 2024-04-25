// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

public static class ServiceCollectionExtensions
{
    public static SqlServerDbContextBuilderContext<TContext> AddSqlServerDbContext<TContext>(
        this IServiceCollection services,
        Builder<SqlServerOptionsBuilder, SqlServerOptions> optionsBuilder,
        Action<SqlServerDbContextOptionsBuilder> sqlServerOptionsBuilder = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TContext : DbContext
    {
        return services
            .AddSqlServerDbContext<TContext>(optionsBuilder(new SqlServerOptionsBuilder()).Build(), sqlServerOptionsBuilder, lifetime);
    }

    public static SqlServerDbContextBuilderContext<TContext> AddSqlServerDbContext<TContext>(
        this IServiceCollection services,
        SqlServerOptions options,
        Action<SqlServerDbContextOptionsBuilder> sqlServerOptionsBuilder = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TContext : DbContext
    {
        EnsureArg.IsNotNull(options, nameof(options));
        EnsureArg.IsNotNullOrEmpty(options.ConnectionString, nameof(options.ConnectionString));
        sqlServerOptionsBuilder ??= c => { };

        if (options.CommandLoggerEnabled)
        {
            options.InterceptorTypes.Add(typeof(CommandLoggerInterceptor));
        }

        RegisterInterceptors(services, options, lifetime);

        services
            .AddDbContext<TContext>((sp, o) =>
            {
                if (options.InterceptorTypes.SafeAny())
                {
                    o.AddInterceptors(options.InterceptorTypes.Select(i => sp.GetRequiredService(i) as IInterceptor));
                }

                if (options.MigrationsEnabled)
                {
                    sqlServerOptionsBuilder(new SqlServerDbContextOptionsBuilder(o).MigrationsAssembly(options.MigrationsAssemblyName ?? typeof(TContext).Assembly.GetName().Name));
                    if (options.MigrationsSchemaEnabled)
                    {
                        var schema = options.MigrationsSchemaName ?? typeof(TContext).Name.ToLowerInvariant().Replace("dbcontext", string.Empty, StringComparison.OrdinalIgnoreCase);
                        if (!string.IsNullOrEmpty(schema))
                        {
                            if (schema.EndsWith("module", StringComparison.OrdinalIgnoreCase) && !schema.Equals("module", StringComparison.OrdinalIgnoreCase))
                            {
                                schema = schema.Replace("module", string.Empty, StringComparison.OrdinalIgnoreCase);
                            }

                            sqlServerOptionsBuilder(new SqlServerDbContextOptionsBuilder(o).MigrationsHistoryTable("__MigrationsHistory", schema));
                        }
                    }
                }

                o.UseSqlServer(options.ConnectionString, sqlServerOptionsBuilder);
                //o.UseExceptionProcessor();

                if (options.LoggerEnabled)
                {
                    o.UseLoggerFactory(services.BuildServiceProvider().GetRequiredService<ILoggerFactory>());
                    o.EnableSensitiveDataLogging(options.SensitiveDataLoggingEnabled);
                }

                if (options.SimpleLoggerEnabled)
                {
                    // https://learn.microsoft.com/en-us/ef/core/logging-events-diagnostics/simple-logging
                    o.LogTo(
                        Console.WriteLine,
                        new[] { DbLoggerCategory.Database.Command.Name },
                        options.SimpleLoggerLevel,
                        DbContextLoggerOptions.SingleLine | DbContextLoggerOptions.Level);
                }

                o.EnableDetailedErrors(options.DetailedErrorsEnabled);

                if (options.MemoryCache is not null)
                {
                    o.UseMemoryCache(options.MemoryCache);
                }

                o.ConfigureWarnings(o => o.Ignore(SqlServerEventId.SavepointsDisabledBecauseOfMARS));
            }, lifetime);

        return new SqlServerDbContextBuilderContext<TContext>(
            services,
            lifetime,
            connectionString: options.ConnectionString,
            provider: Provider.SqlServer);
    }

    public static SqlServerDbContextBuilderContext<TContext> AddSqlServerDbContext<TContext>(
        this IServiceCollection services,
        string connectionString,
        Action<SqlServerDbContextOptionsBuilder> sqlServerOptionsBuilder = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TContext : DbContext
    {
        EnsureArg.IsNotNullOrEmpty(connectionString, nameof(connectionString));

        services.AddDbContext<TContext>(o => o
                .UseSqlServer(connectionString, sqlServerOptionsBuilder), lifetime);

        return new SqlServerDbContextBuilderContext<TContext>(
            services,
            lifetime,
            connectionString: connectionString,
            provider: Provider.SqlServer);
    }

    private static void RegisterInterceptors(IServiceCollection services, SqlServerOptions options, ServiceLifetime lifetime)
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