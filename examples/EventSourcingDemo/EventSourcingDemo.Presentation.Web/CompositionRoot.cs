// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.EventSourcingDemo.Presentation.Web;

using Application.Persons;
using Application.Projection;
using DevKit.Domain.EventSourcing.Registration;
using DevKit.Domain.Repositories;
using DevKit.Infrastructure.EntityFramework.EventSourcing.Models;
using DevKit.Infrastructure.EntityFramework.EventSourcing.SqlServer.Migrations;
using DevKit.Infrastructure.EventSourcing;
using DevKit.Infrastructure.EventSourcing.Publishing;
using DevKit.Infrastructure.Mapping;
using Domain.Model;
using Infrastructure;

public static class CompositionRoot
{
    /// <summary>
    ///     Add application registrations
    /// </summary>
    /// <param name="services">The services</param>
    /// <returns>Service Collection</returns>
    public static IServiceCollection AddModule(this IServiceCollection services, IConfiguration configuration)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a =>
                !a.GetName().Name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) &&
                !a.GetName().Name.StartsWith("System.", StringComparison.OrdinalIgnoreCase))
            .ToArray()
            // Since the assembly of PersonCreatedProjection won't be loaded automatically, we add it to ensure it's loaded.
            .Union([typeof(PersonService).Assembly, typeof(PersonCreatedNotificationProjection).Assembly])
            .ToArray();

        // application composition root
        // tag::MapperSetup[]
            services.AddMapping().WithMapster(); // finds all IRegister for mapster
        // end::MapperSetup[]

        // tag::EventStoreConfiguration[]
        // Event Store Configuration
        services.AddEventStoreContextSqlServer<EventStoreDbContext>(
            configuration.GetConnectionString("eventStoreConnection"),
            typeof(InitialCreate).Assembly.FullName,
            "dbo",
            EventStorePublishingModes.AddToOutbox |
            EventStorePublishingModes.SendProjectionRequestUsingMediator |
            EventStorePublishingModes.SendEventOccuredRequestUsingMediator,
            0,
            0); // <1>

        RegisterAggregates(services);

        //Event Store Outbox Worker
        services.AddEfOutboxWorker(); // <3>
        //end::EventStoreConfiguration[]

        services.AddMapping().WithMapster();

        services.AddTransient<IEntityMapper, MapsterEntityMapper>()
            .AddTransient<IRepositoryOptions, RepositoryOptions>();

        services.AddEventSourcingDemoDbContextSqlServer(
            configuration.GetConnectionString("EventSourcingDemoDbConnection"));
        services
            .AddSingleton<IRegistrationForEventStoreAggregatesAndEvents,
                RegistrationForEventStoreAggregatesAndEvents>();
        services.AddTransient<IPersonService, PersonService>();

        return services;
    }

    public static void RegisterAggregates(IServiceCollection services)
    {
        // tag::EventStoreConfiguration2[]
        // Event Store Projection
        services.RegisterAggregateAndProjectionRequestForEventStore<Person>(
            EventStorePublishingModes.SendProjectionRequestUsingMediator |
            EventStorePublishingModes.SendEventOccuredRequestUsingMediator,
            true); // <2>
        //end::EventStoreConfiguration2[]
    }
}