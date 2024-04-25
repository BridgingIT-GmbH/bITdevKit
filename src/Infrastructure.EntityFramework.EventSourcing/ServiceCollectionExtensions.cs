// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.EventSourcing;

using System;
using BridgingIT.DevKit.Domain.EventSourcing.Registration;
using BridgingIT.DevKit.Domain.EventSourcing.Store;
using BridgingIT.DevKit.Infrastructure.EntityFramework.EventSourcing.Models;
using BridgingIT.DevKit.Infrastructure.EventSourcing;
using BridgingIT.DevKit.Infrastructure.EventSourcing.Publishing;
using Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registrier den als generisches Argument übergebenen DbContext (eine Ableitung von EventStoreDbContext) für den EventStore.
    /// Dies empfiehlt sich z.B. wenn die Projektion in die gleiche Datenbank stattfinden soll.
    /// </summary>
    public static IServiceCollection AddEfCoreEventStore<TContext>(this IServiceCollection services,
        string defaultSchema, EventStorePublishingModes eventStorePublishingModes)
        where TContext : EventStoreDbContext
    {
        return services
            .AddSingleton<IEventStoreConfiguration>(new EventStoreConfiguration() { DefaultSchema = defaultSchema })
            .AddEventStore(eventStorePublishingModes).AddTransient<IEventStoreRepository, EventStoreRepository>()
            .AddTransient<ISnapshotRepository>(sp => new SnapshotRepository(sp.GetRequiredService<IEventStoreAggregateRegistration>(),
                    o => o.DbContext(sp.GetRequiredService<TContext>()).Mapper(
                        sp.GetRequiredService<IEntityMapper>())))
        .AddEfOutbox<TContext>().AddTransient<IAggregateEventRepository>(sp =>
                new AggregateEventRepository(sp.GetRequiredService<IEventStoreAggregateRegistration>(),
                    o => o.DbContext(sp.GetRequiredService<TContext>()).Mapper(
                        sp.GetRequiredService<IEntityMapper>())));
    }

    /// <summary>
    /// Registriert den DbContext EventStoreDbContext für den EventStore. Bitte nutzen Sie die generische Variante der Methode,
    /// um eine Ableitung von EventStoreDbContext zu registrieren. Dies empfiehlt sich z.B. wenn die Projektion in die gleiche Datenbank stattfinden soll.
    /// </summary>
    [Obsolete("Bitte die generische Variante verwenden")]
    public static IServiceCollection AddEfCoreEventStore(this IServiceCollection services, string defaultSchema,
        EventStorePublishingModes eventStorePublishingModes)
    {
        return AddEfCoreEventStore<EventStoreDbContext>(services, defaultSchema, eventStorePublishingModes);
    }
}