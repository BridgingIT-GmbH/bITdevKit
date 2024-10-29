// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.EventSourcingDemo.IntegrationTests;

using Application.Handlers;
using AutoMapper;
using DevKit.Application.Commands;
using DevKit.Domain.EventSourcing.Registration;
using DevKit.Domain.EventSourcing.Store;
using DevKit.Domain.Model;
using DevKit.Domain.Outbox;
using DevKit.Domain.Repositories;
using DevKit.Infrastructure.EntityFramework.EventSourcing;
using DevKit.Infrastructure.EntityFramework.EventSourcing.Models;
using DevKit.Infrastructure.EntityFramework.Outbox;
using DevKit.Infrastructure.EntityFramework.Repositories;
using DevKit.Infrastructure.EventSourcing;
using DevKit.Infrastructure.EventSourcing.Publishing;
using DevKit.Infrastructure.Mapping;
using Domain.Model;
using Domain.Repositories;
using Infrastructure.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Presentation.Web;

public class EventstoreTestBase : TestsBase
{
    protected EventstoreTestBase(ITestOutputHelper output, TestEnvironmentFixture fixture)
        : base(output,
            s =>
            {
                s.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a =>
                    {
                        var name = a.GetName().Name ?? string.Empty;

                        return !name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase);
                    })
                    .ToArray()));

                s.AddMapping().WithAutoMapper();
                s.AddMediatR([typeof(PersonEventOccuredCommandHandler).Assembly]);
                s.AddTransient(typeof(IPipelineBehavior<,>), typeof(DummyCommandBehavior<,>));

                var ctx = fixture.CreateSqlServerDbContext();
                var ctxEventStore = fixture.CreateSqlServerDbContext();
                s.AddScoped<DbContext>(f => ctx);
                s.AddScoped<IPersonOverviewRepository>(sp =>
                    new PersonOverviewRepository(o => o
                        .DbContext(ctx)
                        .Mapper(new AutoMapperEntityMapper(sp.GetService<IMapper>()))));
                s.AddSingleton<IRegistrationForEventStoreAggregatesAndEvents,
                    RegistrationForEventStoreAggregatesAndEvents>();

                s.AddSingleton<IEventStoreConfiguration>(new EventStoreConfiguration { DefaultSchema = "lvs" })
                    .AddEventStore(EventStorePublishingModes.SendProjectionRequestUsingMediator)
                    .AddTransient<IEventStoreRepository, EventStoreRepository>()
                    .AddTransient<ISnapshotRepository>(sp => new SnapshotRepository(
                        sp.GetService<IEventStoreAggregateRegistration>(),
                        o => o.DbContext(ctxEventStore).Mapper(new AutoMapperEntityMapper(sp.GetService<IMapper>()))))
                    .AddTransient<IAggregateEventRepository>(sp =>
                        new AggregateEventRepository(sp.GetService<IEventStoreAggregateRegistration>(),
                            o => o.DbContext(ctxEventStore)
                                .Mapper(new AutoMapperEntityMapper(sp.GetService<IMapper>()))))
                    .AddTransient<IOutboxMessageWriterRepository>(sp =>
                        new OutboxMessageWriterRepository(new EntityFrameworkRepositoryOptions(ctxEventStore,
                            sp.GetRequiredService<IEntityMapper>()) { Autosave = true }))
                    .AddTransient<IOutboxMessageWorkerRepository>(sp =>
                        new OutboxMessageWorkerRepository(new EntityFrameworkRepositoryOptions(ctxEventStore,
                            sp.GetRequiredService<IEntityMapper>()) { Autosave = true }));

                s.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies()
                        .Where(a =>
                        {
                            var name = a.GetName().Name;

                            return name != null &&
                                !name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase);
                        })
                        .Union([typeof(EventStoreProfile).Assembly])
                        .ToArray())
                    .RegisterAutomapperAsEntityMapper();

                s.AddSingleton<IEventTypeSelector, EventTypeSelectorNonMicrosoftAssemblies>();
                CompositionRoot.RegisterAggregates(s);
            })
    {
        var registrationForEventStoreAggregatesAndEvents =
            this.ServiceProvider.GetService<IRegistrationForEventStoreAggregatesAndEvents>();
        var assemblies = new[] { typeof(Person).Assembly };
        ((RegistrationForEventStoreAggregatesAndEvents)registrationForEventStoreAggregatesAndEvents)
            ?.RegisterAggregatesAndEvents(assemblies);
    }

    private static void RegisterRepository<TEntity>(IServiceCollection s, EventSourcingDemoDbContext ctx)
        where TEntity : class, IEntity
    {
        s.AddScoped<IGenericRepository<TEntity>>(r => new EntityFrameworkGenericRepository<TEntity>(op =>
        {
            op.DbContext(ctx);

            return op;
        }));
    }
}