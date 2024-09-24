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
    public static SqliteDbContextBuilderContext<TContext> AddSqliteDbContext<TContext>(
        this IServiceCollection services,
        Builder<SqliteOptionsBuilder, SqliteOptions> optionsBuilder,
        Action<SqliteDbContextOptionsBuilder> sqliteOptionsBuilder = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TContext : DbContext
    {
        return services.AddSqliteDbContext<TContext>(optionsBuilder(new SqliteOptionsBuilder()).Build(),
            sqliteOptionsBuilder,
            lifetime);
    }

    public static SqliteDbContextBuilderContext<TContext> AddSqliteDbContext<TContext>(
        this IServiceCollection services,
        SqliteOptions options,
        Action<SqliteDbContextOptionsBuilder> sqliteOptionsBuilder = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TContext : DbContext
    {
        EnsureArg.IsNotNull(options, nameof(options));
        EnsureArg.IsNotNullOrEmpty(options.ConnectionString, nameof(options.ConnectionString));

        if (options.CommandLoggerEnabled)
        {
            options.InterceptorTypes.Add(typeof(CommandLoggerInterceptor));
        }

        RegisterInterceptors(services, options, lifetime);

        services.AddDbContext<TContext>(ConfigureDbContext(services, options, sqliteOptionsBuilder), lifetime);

        return new SqliteDbContextBuilderContext<TContext>(services,
            lifetime,
            connectionString: options.ConnectionString,
            provider: Provider.Sqlite);

        static Action<IServiceProvider, DbContextOptionsBuilder> ConfigureDbContext(
            IServiceCollection services,
            SqliteOptions options,
            Action<SqliteDbContextOptionsBuilder> sqliteOptionsBuilder)
        {
            return (sp, o) =>
            {
                if (options.InterceptorTypes.SafeAny())
                {
                    o.AddInterceptors(options.InterceptorTypes.Select(i => sp.GetRequiredService(i) as IInterceptor));
                }

                if (options.MigrationsEnabled)
                {
                    sqliteOptionsBuilder(new SqliteDbContextOptionsBuilder(o).MigrationsAssembly(
                        options.MigrationsAssemblyName ?? typeof(TContext).Assembly.GetName().Name));
                    if (options.MigrationsSchemaEnabled)
                    {
                        var schema = options.MigrationsSchemaName ??
                            typeof(TContext).Name.ToLowerInvariant()
                                .Replace("dbcontext", string.Empty, StringComparison.OrdinalIgnoreCase);
                        if (!string.IsNullOrEmpty(schema))
                        {
                            if (schema.EndsWith("module", StringComparison.OrdinalIgnoreCase) &&
                                !schema.Equals("module", StringComparison.OrdinalIgnoreCase))
                            {
                                schema = schema.Replace("module", string.Empty, StringComparison.OrdinalIgnoreCase);
                            }

                            sqliteOptionsBuilder(new SqliteDbContextOptionsBuilder(o).MigrationsHistoryTable(
                                "__MigrationsHistory",
                                schema));
                        }
                    }
                }

                o.UseSqlite(options.ConnectionString, sqliteOptionsBuilder);
                //o.UseExceptionProcessor();

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
            };
        }
    }

    public static SqliteDbContextBuilderContext<TContext> AddSqliteDbContext<TContext>(
        this IServiceCollection services,
        string connectionString,
        Action<SqliteDbContextOptionsBuilder> sqliteOptionsBuilder = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TContext : DbContext
    {
        EnsureArg.IsNotNullOrEmpty(connectionString, nameof(connectionString));

        services.AddDbContext<TContext>(o => o.UseSqlite(connectionString, sqliteOptionsBuilder), lifetime);

        return new SqliteDbContextBuilderContext<TContext>(services,
            lifetime,
            connectionString: connectionString,
            provider: Provider.Sqlite);
    }

    private static void RegisterInterceptors(
        IServiceCollection services,
        SqliteOptions options,
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