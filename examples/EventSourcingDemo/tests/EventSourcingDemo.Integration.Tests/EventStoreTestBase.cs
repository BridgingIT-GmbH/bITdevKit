// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.EventSourcingDemo.IntegrationTests;

using System.Reflection;
using DevKit.Application.Commands;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.EventSourcing.Registration;
using BridgingIT.DevKit.Domain.EventSourcing.Store;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Outbox;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.EventSourcingDemo.Application.Handlers;
using BridgingIT.DevKit.Infrastructure.EntityFramework.EventSourcing;
using BridgingIT.DevKit.Infrastructure.EntityFramework.EventSourcing.Models;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Outbox;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;
using BridgingIT.DevKit.Infrastructure.EventSourcing;
using BridgingIT.DevKit.Infrastructure.EventSourcing.Publishing;
using BridgingIT.DevKit.Infrastructure.Mapping;
using Domain.Model;
using Domain.Repositories;
using EventSourcingDemo.Presentation.Web;

using Infrastructure.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

public class EventstoreTestBase : TestsBase
{
    protected EventstoreTestBase(ITestOutputHelper output, TestEnvironmentFixture fixture)
        : base(output, s =>
    {
        s.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(AppDomain.CurrentDomain.GetAssemblies().Where(a =>
        {
            var name = a.GetName().Name ?? string.Empty;
            return !name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase);
        }).ToArray()));

        s.AddMapping().WithAutoMapper();
        s.AddMediatR(new[] { typeof(PersonEventOccuredCommandHandler).Assembly });
        s.AddTransient(typeof(IPipelineBehavior<,>), typeof(DummyCommandBehavior<,>));

        var ctx = fixture.CreateSqlServerDbContext();
        var ctxEventStore = fixture.CreateSqlServerDbContext();
        s.AddScoped<DbContext>(f => ctx);
        s.AddScoped<IPersonOverviewRepository>(sp =>
            new PersonOverviewRepository(o => o
                .DbContext(ctx).Mapper(new AutoMapperEntityMapper(sp.GetService<global::AutoMapper.IMapper>()))));
        s.AddSingleton<IRegistrationForEventStoreAggregatesAndEvents,
            RegistrationForEventStoreAggregatesAndEvents>();

        s.AddSingleton<IEventStoreConfiguration>(new EventStoreConfiguration { DefaultSchema = "lvs" })
            .AddEventStore(EventStorePublishingModes.SendProjectionRequestUsingMediator)
            .AddTransient<IEventStoreRepository, EventStoreRepository>()
            .AddTransient<ISnapshotRepository>(sp => new SnapshotRepository(
                sp.GetService<IEventStoreAggregateRegistration>(),

                o => o.DbContext(ctxEventStore).Mapper(
                    new AutoMapperEntityMapper(sp.GetService<global::AutoMapper.IMapper>()))))
            .AddTransient<IAggregateEventRepository>(sp =>
                new AggregateEventRepository(sp.GetService<IEventStoreAggregateRegistration>(),
                    o => o.DbContext(ctxEventStore).Mapper(
                        new AutoMapperEntityMapper(sp.GetService<global::AutoMapper.IMapper>()))))
            .AddTransient<IOutboxMessageWriterRepository>(sp =>
                new OutboxMessageWriterRepository(
                    new EntityFrameworkRepositoryOptions(ctxEventStore,
                            sp.GetRequiredService<IEntityMapper>())
                    { Autosave = true }))
            .AddTransient<IOutboxMessageWorkerRepository>(sp =>
                new OutboxMessageWorkerRepository(
                    new EntityFrameworkRepositoryOptions(ctxEventStore,
                            sp.GetRequiredService<IEntityMapper>())
                    { Autosave = true }));

        s.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies().Where(a =>
        {
            var name = a.GetName().Name;
            return name != null &&
                   !name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase);
        }).Union(
            new[] { typeof(EventStoreProfile).Assembly }).ToArray()).RegisterAutomapperAsEntityMapper();

        s.AddSingleton<IEventTypeSelector, EventTypeSelectorNonMicrosoftAssemblies>();
        CompositionRoot.RegisterAggregates(s);
    })
    {
        var registrationForEventStoreAggregatesAndEvents = this.ServiceProvider.GetService<IRegistrationForEventStoreAggregatesAndEvents>();
        var assemblies = new Assembly[] { typeof(Person).Assembly };
        ((RegistrationForEventStoreAggregatesAndEvents)registrationForEventStoreAggregatesAndEvents)?.RegisterAggregatesAndEvents(assemblies);
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