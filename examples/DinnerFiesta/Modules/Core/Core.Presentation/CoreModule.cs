// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Presentation;

using BridgingIT.DevKit.Application;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.DinnerFiesta.Application.Modules.Core;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application.Jobs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Routing;

public class CoreModule : WebModuleBase
{
    public override IServiceCollection Register(IServiceCollection services, IConfiguration configuration = null, IWebHostEnvironment environment = null)
    {
        var moduleConfiguration = this.Configure<CoreModuleConfiguration, CoreModuleConfiguration.Validator>(services, configuration);

        services.AddJobScheduling()
            .WithJob<EchoMessageJob>(CronExpressions.Every5Minutes)
            .WithJob<DinnerSnapshotExportJob>(CronExpressions.EveryHour);
            //.WithJob<HealthCheckJob>(CronExpressions.EveryMinute);

        services.AddStartupTasks()
            .WithTask<CoreDomainSeederTask>(o => o
                .Enabled(environment?.IsDevelopment() == true)
                .StartupDelay(moduleConfiguration.SeederTaskStartupDelay));

        services.AddMessaging()
            .WithSubscription<EchoMessage, EchoMessageHandler>();

        // TODO: work in progress
        //services.AddCosmosDbContext<CoreDbContext>(o => o
        //        .UseConnectionString(moduleConfiguration.ConnectionStrings["Default"])
        //        .UseDatabase(this.Name)
        //        .UseLogger()
        //        .UseIntercepter<QueryLoggingInterceptor>(), c =>
        //        {
        //            c.ConnectionMode(ConnectionMode.Direct);
        //            c.HttpClientFactory(() => // ignore certificate validation
        //            {
        //                return new HttpClient(new HttpClientHandler
        //                {
        //                    ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        //                });
        //            });
        //        })
        //    .WithHealthChecks();

        services.AddSqlServerDbContext<CoreDbContext>(o => o
                .UseConnectionString(moduleConfiguration.ConnectionStrings["Default"])
                //.UseLogger()
                //.UseSimpleLogger()
                .UseCommandLogger(),
                //.UseIntercepter<ModuleScopeInterceptor>()
                //.UseIntercepter<CommandLoggerInterceptor>(),
                c =>
                {
                    c.CommandTimeout(30);
                    //c.UseQuerySplittingBehavior(Microsoft.EntityFrameworkCore.QuerySplittingBehavior.SplitQuery);
                })
            .WithHealthChecks()
            .WithDatabaseMigratorService(o => o
                .Enabled(environment.IsDevelopment()))
            //.WithDatabaseCreatorService(o => o.DeleteOnStartup())
            //.WithOutboxMessageService(o => o
            //    .ProcessingInterval("00:00:30").StartupDelay("00:00:15").PurgeOnStartup(false)) // << see AddMessaging().WithOutbox<CoreDbContext> in Program.cs
            .WithOutboxDomainEventService(o => o
                .ProcessingInterval("00:00:30").StartupDelay("00:00:15").PurgeOnStartup().ProcessingModeImmediate());

        //services.AddCosmosClient(o => o // WARN: register this once/global in the Program.cs
        //    .UseConnectionString())
        //    .WithHealthChecks();

        //services.AddAzureBlobServiceClient(o => o // WARN: register this once/global in the Program.cs
        //    .UseConnectionString("connectionstring"))
        //    .WithHealthChecks();

        //services.AddAzureTableServiceClient(o => o // WARN: register this once/global in the Program.cs
        //    .UseConnectionString("connectionstring"))
        //    .WithHealthChecks();

        services.AddEntityFrameworkDocumentStoreClient<DinnerSnapshotDocument, CoreDbContext>() // no need to setup the client+provider (sql)
            .WithBehavior<LoggingDocumentStoreClientBehavior<DinnerSnapshotDocument>>()
            .WithBehavior((inner, sp) =>
                new TimeoutDocumentStoreClientBehavior<DinnerSnapshotDocument>(sp.GetRequiredService<ILoggerFactory>(), inner, new TimeoutDocumentStoreClientBehaviorOptions { Timeout = 30.Seconds() }));

        //services.AddAzureBlobDocumentStoreClient<DinnerSnapshotDocument>()
        //    .WithBehavior<LoggingDocumentStoreClientBehavior<DinnerSnapshotDocument>>()
        //    .WithBehavior((inner, sp) =>
        //        new TimeoutDocumentStoreClientBehavior<DinnerSnapshotDocument>(sp.GetRequiredService<ILoggerFactory>(), inner, new TimeoutDocumentStoreClientBehaviorOptions { Timeout = 30.Seconds() }));

        //services.AddAzureTableDocumentStoreClient<DinnerSnapshotDocument>()
        //    .WithBehavior<LoggingDocumentStoreClientBehavior<DinnerSnapshotDocument>>()
        //    .WithBehavior((inner, sp) =>
        //        new TimeoutDocumentStoreClientBehavior<DinnerSnapshotDocument>(sp.GetRequiredService<ILoggerFactory>(), inner, new TimeoutDocumentStoreClientBehaviorOptions { Timeout = 30.Seconds() }));

        //services.AddCosmosDocumentStoreClient<DinnerSnapshotDocument>(o => o.Database(this.Name))
        //    .WithBehavior<LoggingDocumentStoreClientBehavior<DinnerSnapshotDocument>>()
        //    .WithBehavior((inner, sp) =>
        //        new TimeoutDocumentStoreClientBehavior<DinnerSnapshotDocument>(sp.GetRequiredService<ILoggerFactory>(), inner, new TimeoutDocumentStoreClientBehaviorOptions { Timeout = 30.Seconds() }));

        //services.AddCosmosSqlRepository<Dinner>(o => o.Database(this.Name).PartitionKey(e => e.Location.Country))
        //    .WithBehavior<RepositoryTracingBehavior<Dinner>>()
        //    .WithBehavior<RepositoryLoggingBehavior<Dinner>>()
        //    //.WithBehavior((inner) => new RepositoryIncludeBehavior<Dinner>(e => e.AuditState, inner));
        //    .WithBehavior<RepositoryDomainEventBehavior<Dinner>>()
        //    .WithBehavior<RepositoryDomainEventPublisherBehavior<Dinner>>();
        //     //.WithBehavior<RepositoryOutboxDomainEventBehavior<Dinner, CoreDbContext>>()

        services.AddEntityFrameworkRepository<Bill, CoreDbContext>()
            .WithTransactions<NullRepositoryTransaction<Bill>>()
            .WithBehavior<RepositoryTracingBehavior<Bill>>()
            .WithBehavior<RepositoryLoggingBehavior<Bill>>()
            //.WithBehavior((inner) => new RepositoryIncludeBehavior<Bill>(e => e.AuditState, inner));
            .WithBehavior<RepositoryAuditStateBehavior<Bill>>()
            .WithBehavior<RepositoryDomainEventBehavior<Bill>>()
            .WithBehavior<RepositoryDomainEventMetricsBehavior<Bill>>()
            .WithBehavior<RepositoryDomainEventPublisherBehavior<Bill>>();
            //.WithBehavior<RepositoryOutboxDomainEventBehavior<Bill, CoreDbContext>>()

        services.AddEntityFrameworkRepository<Dinner, CoreDbContext>()
            .WithTransactions<NullRepositoryTransaction<Dinner>>()
            .WithBehavior<RepositoryTracingBehavior<Dinner>>()
            .WithBehavior<RepositoryLoggingBehavior<Dinner>>()
            //.WithBehavior((inner) => new RepositoryIncludeBehavior<Dinner>(e => e.AuditState, inner));
            .WithBehavior<RepositoryAuditStateBehavior<Dinner>>()
            .WithBehavior<RepositoryDomainEventBehavior<Dinner>>()
            .WithBehavior<RepositoryDomainEventMetricsBehavior<Dinner>>()
            .WithBehavior<RepositoryDomainEventPublisherBehavior<Dinner>>();
            //.WithBehavior<RepositoryOutboxDomainEventBehavior<Dinner, CoreDbContext>>()

        services.AddEntityFrameworkRepository<Guest, CoreDbContext>()
            .WithTransactions<NullRepositoryTransaction<Guest>>()
            .WithBehavior<RepositoryTracingBehavior<Guest>>()
            .WithBehavior<RepositoryLoggingBehavior<Guest>>()
            //.WithBehavior((inner) => new RepositoryIncludeBehavior<Guest>(e => e.AuditState, inner));
            .WithBehavior<RepositoryAuditStateBehavior<Guest>>()
            .WithBehavior<RepositoryDomainEventBehavior<Guest>>()
            .WithBehavior<RepositoryDomainEventMetricsBehavior<Guest>>()
            .WithBehavior<RepositoryDomainEventPublisherBehavior<Guest>>();
            //.WithBehavior<RepositoryOutboxDomainEventBehavior<Guest, CoreDbContext>>()

        services.AddEntityFrameworkRepository<Domain.Host, CoreDbContext>()
            .WithTransactions<NullRepositoryTransaction<Domain.Host>>()
            .WithBehavior<RepositoryTracingBehavior<Domain.Host>>()
            .WithBehavior<RepositoryLoggingBehavior<Domain.Host>>()
            //.WithBehavior((inner) => new RepositoryIncludeBehavior<Domain.Host>(e => e.AuditState, inner));
            .WithBehavior<RepositoryAuditStateBehavior<Domain.Host>>()
            .WithBehavior<RepositoryDomainEventBehavior<Domain.Host>>()
            .WithBehavior<RepositoryDomainEventMetricsBehavior<Domain.Host>>()
            .WithBehavior<RepositoryDomainEventPublisherBehavior<Domain.Host>>();
            //.WithBehavior<RepositoryOutboxDomainEventBehavior<Domain.Host, CoreDbContext>>()

        services.AddEntityFrameworkRepository<Menu, CoreDbContext>()
            .WithTransactions<NullRepositoryTransaction<Menu>>()
            .WithBehavior<RepositoryTracingBehavior<Menu>>()
            .WithBehavior<RepositoryLoggingBehavior<Menu>>()
            //.WithBehavior((inner) => new RepositoryIncludeBehavior<Menu>(e => e.AuditState, inner));
            .WithBehavior<RepositoryAuditStateBehavior<Menu>>()
            .WithBehavior<RepositoryDomainEventBehavior<Menu>>()
            .WithBehavior<RepositoryDomainEventMetricsBehavior<Menu>>()
            .WithBehavior<RepositoryDomainEventPublisherBehavior<Menu>>();
            //.WithBehavior<RepositoryOutboxDomainEventBehavior<Menu, CoreDbContext>>()

        services.AddEntityFrameworkRepository<MenuReview, CoreDbContext>()
            .WithTransactions<NullRepositoryTransaction<MenuReview>>()
            .WithBehavior<RepositoryTracingBehavior<MenuReview>>()
            .WithBehavior<RepositoryLoggingBehavior<MenuReview>>()
            //.WithBehavior((inner) => new RepositoryIncludeBehavior<MenuReview>(e => e.AuditState, inner));
            .WithBehavior<RepositoryAuditStateBehavior<MenuReview>>()
            .WithBehavior<RepositoryDomainEventBehavior<MenuReview>>()
            .WithBehavior<RepositoryDomainEventMetricsBehavior<MenuReview>>()
            .WithBehavior<RepositoryDomainEventPublisherBehavior<MenuReview>>();
            //.WithBehavior<RepositoryOutboxDomainEventBehavior<MenuReview, CoreDbContext>>()

        services.AddEntityFrameworkRepository<User, CoreDbContext>()
            .WithTransactions<NullRepositoryTransaction<User>>()
            .WithBehavior<RepositoryTracingBehavior<User>>()
            .WithBehavior<RepositoryLoggingBehavior<User>>()
            //.WithBehavior((inner) => new RepositoryIncludeBehavior<Domain.User>(e => e.AuditState, inner));
            .WithBehavior<RepositoryAuditStateBehavior<User>>()
            .WithBehavior<RepositoryDomainEventBehavior<User>>()
            .WithBehavior<RepositoryDomainEventMetricsBehavior<User>>()
            .WithBehavior<RepositoryDomainEventPublisherBehavior<User>>();
            //.WithBehavior<RepositoryOutboxDomainEventBehavior<Domain.User, CoreDbContext>>()

        return services;
    }

    public override IApplicationBuilder Use(IApplicationBuilder app, IConfiguration configuration = null, IWebHostEnvironment environment = null)
    {
        return app;
    }

    public override IEndpointRouteBuilder Map(IEndpointRouteBuilder app, IConfiguration configuration = null, IWebHostEnvironment environment = null)
    {
        //app.MapGet("/hw", () => "Hello World!"); // https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-8.0

        return app;
    }
}