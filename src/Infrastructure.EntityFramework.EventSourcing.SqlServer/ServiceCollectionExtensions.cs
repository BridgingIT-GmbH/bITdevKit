// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Infrastructure.EntityFramework.EventSourcing;
using BridgingIT.DevKit.Infrastructure.EntityFramework.EventSourcing.Models;
using BridgingIT.DevKit.Infrastructure.EventSourcing.Publishing;
using EntityFrameworkCore;
using EntityFrameworkCore.Diagnostics;

public static class ServiceCollectionExtensions
{
    [Obsolete("Bitte die Überladung benutzen.")]
    public static IServiceCollection AddEventStoreSqlServer(
        this IServiceCollection services,
        string connectionString,
        string defaultSchema,
        EventStorePublishingModes eventStorePublishingModes)
    {
        services.AddEfCoreEventStore(defaultSchema, eventStorePublishingModes)
            .AddDbContext<EventStoreDbContext>(options => { options.UseSqlServer(connectionString); });

        return services;
    }

    /// <summary>
    ///     Registriert den EventStore in einer SqlServer-Datenbank.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="connectionString"></param>
    /// <param name="nameOfMigrationsAssembly">
    ///     Name des Assemblies mit den Migrations. Empty, falls kein MigrationsAssembly
    ///     angegeben werden soll
    /// </param>
    /// <param name="defaultSchema">Default-Schema des Kontextes</param>
    /// <param name="eventStorePublishingModes"></param>
    /// <param name="maxRetryCount">Wenn der Parameter > 0 ist wird EnableRetryOnFailure aktiviert.</param>
    /// ///
    /// <param name="maxRetryDelaySeconds"></param>
    /// <param name="timeoutInSeconds">Timeout. Wenn null, dann wird der default von EFCore verwendet.</param>
    public static IServiceCollection AddEventStoreContextSqlServer<TContext>(
        this IServiceCollection services,
        string connectionString,
        string nameOfMigrationsAssembly,
        string defaultSchema,
        EventStorePublishingModes eventStorePublishingModes,
        int maxRetryCount,
        int maxRetryDelaySeconds,
        int? timeoutInSeconds = null)
        where TContext : EventStoreDbContext
    {
        services.AddEfCoreEventStore<TContext>(defaultSchema, eventStorePublishingModes)
            .AddDbContext<TContext>(options =>
            {
                options.UseSqlServer(connectionString,
                    b =>
                    {
                        if (timeoutInSeconds.HasValue)
                        {
                            b.CommandTimeout(timeoutInSeconds.Value);
                        }

                        if (!string.IsNullOrEmpty(nameOfMigrationsAssembly))
                        {
                            b.MigrationsAssembly(nameOfMigrationsAssembly);
                        }

                        if (maxRetryCount > 0)
                        {
                            b.EnableRetryOnFailure(maxRetryCount, TimeSpan.FromSeconds(maxRetryDelaySeconds), null);
                        }
                    });
            });

        return services;
    }

    /// <summary>
    ///     Registriert den EventStore in einer SqlServer-Datenbank ohne MARS-Warnings und über eine
    ///     NoSavepointsTransactionFactory.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="connectionString"></param>
    /// <param name="nameOfMigrationsAssembly">
    ///     Name des Assemblies mit den Migrations. Empty, falls kein MigrationsAssembly
    ///     angegeben werden soll
    /// </param>
    /// <param name="defaultSchema">Default-Schema des Kontextes</param>
    /// <param name="eventStorePublishingModes"></param>
    /// <param name="maxRetryCount">Wenn der Parameter > 0 ist wird EnableRetryOnFailure aktiviert.</param>
    /// <param name="maxRetryDelaySeconds"></param>
    /// <param name="timeoutInSeconds">Timeout. Wenn null, dann wird der default von EFCore verwendet.</param>
    public static IServiceCollection AddEventStoreContextSqlServerWithoutSnapshot<TContext>(
        this IServiceCollection services,
        string connectionString,
        string nameOfMigrationsAssembly,
        string defaultSchema,
        EventStorePublishingModes eventStorePublishingModes,
        int maxRetryCount,
        int maxRetryDelaySeconds,
        int? timeoutInSeconds = null)
        where TContext : EventStoreDbContext
    {
        services.AddEfCoreEventStore<TContext>(defaultSchema, eventStorePublishingModes)
            .AddDbContext<TContext>(options =>
            {
                options.UseSqlServer(connectionString,
                    b =>
                    {
                        if (timeoutInSeconds.HasValue)
                        {
                            b.CommandTimeout(timeoutInSeconds.Value);
                        }

                        if (!string.IsNullOrEmpty(nameOfMigrationsAssembly))
                        {
                            b.MigrationsAssembly(nameOfMigrationsAssembly);
                        }

                        if (maxRetryCount > 0)
                        {
                            b.EnableRetryOnFailure(maxRetryCount, TimeSpan.FromSeconds(maxRetryDelaySeconds), null);
                        }

                        options.ConfigureWarnings(w => w.Ignore(SqlServerEventId.SavepointsDisabledBecauseOfMARS));
                    });
            });

        return services;
    }

    [Obsolete("Bitte die generische Variante verwenden")]
    public static IServiceCollection AddEventStoreContextSqlServer(
        this IServiceCollection services,
        string connectionString,
        string nameOfMigrationsAssembly,
        string defaultSchema,
        EventStorePublishingModes eventStorePublishingModes,
        int maxRetryCount,
        int maxRetryDelaySeconds)
    {
        return AddEventStoreContextSqlServer<EventStoreDbContext>(services,
            connectionString,
            nameOfMigrationsAssembly,
            defaultSchema,
            eventStorePublishingModes,
            maxRetryCount,
            maxRetryDelaySeconds);
    }
}