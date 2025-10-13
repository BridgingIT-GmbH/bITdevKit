// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.JobScheduling;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using EntityFrameworkCore;
using EntityFrameworkCore.Database.Command;
using EntityFrameworkCore.Diagnostics;
using Extensions;
using Logging;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Quartz;

public static class ServiceCollectionExtensions
{
    public static PostgresDbContextBuilderContext<TContext> AddPostgresDbContext<TContext>(
        this IServiceCollection services,
        Builder<PostgresOptionsBuilder, PostgresOptions> optionsBuilder,
        Action<NpgsqlDbContextOptionsBuilder> postgresOptionsBuilder = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TContext : DbContext
    {
        return services.AddPostgresDbContext<TContext>(
            optionsBuilder(new PostgresOptionsBuilder()).Build(), postgresOptionsBuilder, lifetime);
    }

    public static PostgresDbContextBuilderContext<TContext> AddPostgresDbContext<TContext>(
        this IServiceCollection services,
        PostgresOptions options,
        Action<NpgsqlDbContextOptionsBuilder> postgresOptionsBuilder = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TContext : DbContext
    {
        EnsureArg.IsNotNull(options, nameof(options));
        EnsureArg.IsNotNullOrEmpty(options.ConnectionString, nameof(options.ConnectionString));
        postgresOptionsBuilder ??= c => { };

        if (options.CommandLoggerEnabled)
        {
            options.InterceptorTypes.Add(typeof(CommandLoggerInterceptor));
        }

        RegisterInterceptors(services, options, lifetime);

        var connectionString = options.ConnectionString;
        if (!string.IsNullOrEmpty(options.SearchPath))
        {
            var builder = new NpgsqlConnectionStringBuilder(options.ConnectionString);
            builder.SearchPath = options.SearchPath;
            connectionString = builder.ConnectionString;
        }

        services.AddDbContext<TContext>(ConfigureDbContext(services, options, connectionString, postgresOptionsBuilder), lifetime);

        return new PostgresDbContextBuilderContext<TContext>(services,
            lifetime,
            connectionString: connectionString,
            provider: Provider.Postgres);

        static Action<IServiceProvider, DbContextOptionsBuilder> ConfigureDbContext(
            IServiceCollection services,
            PostgresOptions options,
            string connectionString,
            Action<NpgsqlDbContextOptionsBuilder> postgresOptionsBuilder)
        {
            return (sp, o) =>
            {
                if (options.InterceptorTypes.SafeAny())
                {
                    o.AddInterceptors(options.InterceptorTypes.Select(i => sp.GetRequiredService(i) as IInterceptor));
                }

                if (options.MigrationsEnabled)
                {
                    var npgBuilder = new NpgsqlDbContextOptionsBuilder(o)
                        .MigrationsAssembly(options.MigrationsAssemblyName ?? typeof(TContext).Assembly.GetName().Name);
                    postgresOptionsBuilder(npgBuilder);

                    if (options.MigrationsSchemaEnabled)
                    {
                        var schema = options.MigrationsSchemaName;
                        if (string.IsNullOrEmpty(schema))
                        {
                            schema = typeof(TContext).Name.ToLowerInvariant()
                                .Replace("dbcontext", string.Empty, StringComparison.OrdinalIgnoreCase);
                        }

                        if (!string.IsNullOrEmpty(schema))
                        {
                            if (schema.EndsWith("module", StringComparison.OrdinalIgnoreCase) &&
                                !schema.Equals("module", StringComparison.OrdinalIgnoreCase))
                            {
                                schema = schema.Replace("module", string.Empty, StringComparison.OrdinalIgnoreCase);
                            }
                            npgBuilder.MigrationsHistoryTable("__MigrationsHistory", schema);
                        }
                    }
                }

                o.UseNpgsql(connectionString, npg =>
                {
                    postgresOptionsBuilder(npg);
                    //if (options.MaxPoolSize.HasValue) npg.MaxPoolSize(options.MaxPoolSize.Value);
                    //if (options.ConnectionIdleLifetime.HasValue) npg.ConnectionIdleLifetime(options.ConnectionIdleLifetime.Value);
                });

                if (options.LoggerEnabled)
                {
                    o.UseLoggerFactory(sp.GetRequiredService<ILoggerFactory>());
                    o.EnableSensitiveDataLogging(options.SensitiveDataLoggingEnabled);
                }

                if (options.SimpleLoggerEnabled)
                {
                    o.LogTo(Console.WriteLine,
                        [DbLoggerCategory.Database.Command.Name],
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

    public static PostgresDbContextBuilderContext<TContext> AddPostgresDbContext<TContext>(
        this IServiceCollection services,
        string connectionString,
        Action<NpgsqlDbContextOptionsBuilder> postgresOptionsBuilder = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TContext : DbContext
    {
        EnsureArg.IsNotNullOrEmpty(connectionString, nameof(connectionString));

        services.AddDbContext<TContext>(o => o.UseNpgsql(connectionString, postgresOptionsBuilder), lifetime);

        return new PostgresDbContextBuilderContext<TContext>(services,
            lifetime,
            connectionString: connectionString,
            provider: Provider.Postgres);
    }

    public static PostgresDbContextBuilderContext<TContext> WithSequenceNumberGenerator<TContext>(
        this PostgresDbContextBuilderContext<TContext> context,
        SequenceNumberGeneratorOptions options = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    where TContext : DbContext
    {
        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                context.Services.AddSingleton<ISequenceNumberGenerator>(sp =>
                    new PostgresSequenceNumberGenerator<TContext>(
                        sp.GetRequiredService<ILoggerFactory>(),
                        sp.GetRequiredService<IServiceProvider>(),
                        options));
                break;

            case ServiceLifetime.Transient:
                context.Services.AddTransient<ISequenceNumberGenerator>(sp =>
                    new PostgresSequenceNumberGenerator<TContext>(
                        sp.GetRequiredService<ILoggerFactory>(),
                        sp.GetRequiredService<IServiceProvider>(),
                        options));
                break;

            default:
                context.Services.AddScoped<ISequenceNumberGenerator>(sp =>
                    new PostgresSequenceNumberGenerator<TContext>(
                        sp.GetRequiredService<ILoggerFactory>(),
                        sp.GetRequiredService<IServiceProvider>(),
                        options));
                break;
        }

        return context;
    }

    /// <summary>
    /// Configures the job scheduling to use PostgreSQL persistence with the specified connection string and table prefix.
    /// </summary>
    /// <param name="context">The job scheduling builder context from AddJobScheduling.</param>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    /// <param name="tablePrefix">The table prefix for Quartz tables (default: "[public].[QRTZ_").</param>
    /// <returns>The updated job scheduling builder context.</returns>
    public static JobSchedulingBuilderContext WithPostgresStore(
        this JobSchedulingBuilderContext context,
        string connectionString,
        string tablePrefix = "[public].[QRTZ_")
    {
        context.Services.AddSingleton<IJobStoreProvider>(sp => new PostgresJobStoreProvider(
            sp.GetService<ILoggerFactory>(),
            connectionString,
            tablePrefix));
        context.Services.AddSingleton<IJobService>(sp => new JobService(
            sp.GetService<ILoggerFactory>(),
            sp.GetRequiredService<ISchedulerFactory>(),
            sp.GetRequiredService<IJobStoreProvider>()));

        return context;
    }

    private static void RegisterInterceptors(IServiceCollection services, PostgresOptions options, ServiceLifetime lifetime)
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