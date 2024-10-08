﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.Presentation;

using Application;
using Common;
using DevKit.Domain.Repositories;
using DinnerFiesta.Application.Modules.Marketing;
using Domain;
using Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class MarketingModule : WebModuleBase
{
    public override IServiceCollection Register(
        IServiceCollection services,
        IConfiguration configuration = null,
        IWebHostEnvironment environment = null)
    {
        var moduleConfiguration =
            this.Configure<MarketingModuleConfiguration, MarketingModuleConfiguration.Validator>(services,
                configuration);

        services.AddStartupTasks()
            .WithTask<MarketingDomainSeederTask>(o => o
                .Enabled(environment?.IsDevelopment() == true)
                .StartupDelay(moduleConfiguration.SeederTaskStartupDelay));

        services.AddMessaging(configuration)
            .WithSubscription<EchoMessage, EchoMessageHandler>()
            .WithSubscription<UserCreatedMessage, UserCreatedMessageHandler>();

        services.AddSqlServerDbContext<MarketingDbContext>(o => o
                    .UseConnectionString(moduleConfiguration.ConnectionStrings["Default"])
                    //.UseLogger().UseSimpleLogger()
                    .UseCommandLogger(),
                //.UseIntercepter<ModuleScopeInterceptor>()
                //.UseIntercepter<CommandLoggerInterceptor>(),
                c => c
                    .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
                    .CommandTimeout(30))
            .WithHealthChecks()
            .WithDatabaseMigratorService(o => o
                .StartupDelay("00:00:05")
                .Enabled(environment.IsDevelopment())
                .PurgeOnStartup());
        //.WithDatabaseCreatorService(o => o
        //    .StartupDelay("00:00:05")
        //    .Enabled(environment?.IsDevelopment() == true));
        //.WithOutboxMessageService(o => o
        //    .ProcessingInterval("00:00:30").StartupDelay("00:00:15").PurgeOnStartup(false)) // << see AddMessaging().WithOutbox<CoreDbContext> in Program.cs
        //.WithOutboxDomainEventService(o => o //
        //    .Interval("00:00:10").StartupDelay("00:00:05").PurgeOnStartup());

        services.AddEntityFrameworkRepository<Customer, MarketingDbContext>()
            //.WithTransactions()
            // behaviors order: first..last
            .WithTransactions<NullRepositoryTransaction<Customer>>()
            .WithBehavior<RepositoryTracingBehavior<Customer>>()
            .WithBehavior<RepositoryLoggingBehavior<Customer>>()
            //.WithBehavior((inner) => new GenericRepositoryIncludeBehavior<Customer>(e => e.AuditState, inner));
            .WithBehavior<RepositoryDomainEventBehavior<Customer>>()
            .WithBehavior<RepositoryDomainEventMetricsBehavior<Customer>>()
            .WithBehavior<RepositoryDomainEventPublisherBehavior<Customer>>();
        //.WithBehavior<RepositoryOutboxDomainEventBehavior<Customer, CoreDbContext>>()

        return services;
    }

    public override IApplicationBuilder Use(
        IApplicationBuilder app,
        IConfiguration configuration = null,
        IWebHostEnvironment environment = null)
    {
        return app;
    }

    public override IEndpointRouteBuilder Map(
        IEndpointRouteBuilder app,
        IConfiguration configuration = null,
        IWebHostEnvironment environment = null)
    {
        //app.MapGet("/hw", () => "Hello World!"); // https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-8.0

        return app;
    }
}