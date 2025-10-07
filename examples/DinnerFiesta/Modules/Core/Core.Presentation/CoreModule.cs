// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Presentation;

using Application;
using Application.Jobs;
using Common;
using DevKit.Application;
using DevKit.Application.JobScheduling;
using DevKit.Application.Storage;
using DevKit.Domain.Repositories;
using DevKit.Infrastructure.EntityFramework;
using DinnerFiesta.Application.Modules.Core;
using Domain;
using Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Host = Domain.Host;

public class CoreModule : WebModuleBase
{
    public override IServiceCollection Register(
        IServiceCollection services,
        IConfiguration configuration = null,
        IWebHostEnvironment environment = null)
    {
        var moduleConfiguration =
            this.Configure<CoreModuleConfiguration, CoreModuleConfiguration.Validator>(services, configuration);

        services.AddJobScheduling()
            .WithJob<EchoJob>(CronExpressions.Every5Minutes) // .WithSingletonJob<EchoJob>(CronExpressions.Every5Minutes)
            .WithJob<EchoMessageJob>(CronExpressions.Every30Minutes)
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
                    //.UseLogger().UseSimpleLogger()
                    .UseCommandLogger(),
                //.UseIntercepter<ModuleScopeInterceptor>()
                //.UseIntercepter<CommandLoggerInterceptor>(),
                c => c
                    .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
                    .CommandTimeout(30))
            .WithDatabaseMigratorService(o => o
                .Enabled(environment.IsDevelopment())
                .PurgeOnStartup())
            //.WithDatabaseCreatorService(o => o
            //    .Enabled(environment?.IsDevelopment() == true)
            //    .DeleteOnStartup())
            //.WithOutboxMessageService(o => o
            //    .ProcessingInterval("00:00:30")
            //    .StartupDelay("00:00:30")
            //    .PurgeOnStartup(false)) // << see AddMessaging().WithOutbox<CoreDbContext> in Program.cs
            .WithOutboxDomainEventService(o => o
                .ProcessingInterval("00:00:30")
                .ProcessingModeImmediate() // forwards the outbox event, through a queue, to the outbox worker
                .StartupDelay("00:00:15")
                .PurgeOnStartup());

        services.AddEntityAuthorization(o =>
        {
            o.WithEntityPermissions<CoreDbContext>(e =>
            {
                // Register entities that need permission checks + auth policies
                e.AddEntity<Host>(Permission.Read, Permission.Write, Permission.List) // allowed permissions
                    .AddDefaultPermissions<Host>(Permission.Read, Permission.List) // default permissions if user has none
                    .UseDefaultPermissionProvider<Host>();
                //.AddDefaultPermissionProvider<Host, HostDefaultPermissionProvider>();

                e.AddEntity<Dinner>(Permission.Read, Permission.Write, Permission.List) // allowed permissions
                    .AddDefaultPermissions<Dinner>(Permission.Read, Permission.List) // default permissions if user has none
                    .UseDefaultPermissionProvider<Dinner>();
                //.AddDefaultPermissionProvider<Dinner, DinnerDefaultPermissionProvider>();

                //e.AddHierarchicalEntity<Asset>(e => e.ParentId)
                //    .AddDefaultPermissions<Asset>(EntityPermissions.Read, EntityPermissions.List);
                //    .UseDefaultPermissionProvider<Asset>();

                e.EnableCaching(false)
                 .WithCacheLifetime(TimeSpan.FromMinutes(15));
            });

            o.EnableEvaluationEndpoints();
            o.EnableManagementEndpoints();
        });

        //services.AddCosmosClient(o => o // WARN: register this once/global in the Program.cs
        //    .UseConnectionString())
        //    .WithHealthChecks();

        //services.AddAzureBlobServiceClient(o => o // WARN: register this once/global in the Program.cs
        //    .UseConnectionString("connectionstring"))
        //    .WithHealthChecks();

        //services.AddAzureTableServiceClient(o => o // WARN: register this once/global in the Program.cs
        //    .UseConnectionString("connectionstring"))
        //    .WithHealthChecks();

        services
            .AddEntityFrameworkDocumentStoreClient<DinnerSnapshotDocument,
                CoreDbContext>() // no need to setup the client+provider (sql)
            .WithBehavior<LoggingDocumentStoreClientBehavior<DinnerSnapshotDocument>>()
            .WithBehavior((inner, sp) =>
                new TimeoutDocumentStoreClientBehavior<DinnerSnapshotDocument>(sp.GetRequiredService<ILoggerFactory>(),
                    inner,
                    new TimeoutDocumentStoreClientBehaviorOptions { Timeout = 30.Seconds() }));

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
            //.WithBehavior<RepositoryDomainEventBehavior<Bill>>()
            .WithBehavior<RepositoryDomainEventMetricsBehavior<Bill>>()
            //.WithBehavior<RepositoryDomainEventPublisherBehavior<Bill>>();
            .WithBehavior<RepositoryOutboxDomainEventBehavior<Bill, CoreDbContext>>();

        services.AddEntityFrameworkRepository<Dinner, CoreDbContext>()
            .WithTransactions<NullRepositoryTransaction<Dinner>>()
            .WithBehavior<RepositoryTracingBehavior<Dinner>>()
            .WithBehavior<RepositoryLoggingBehavior<Dinner>>()
            //.WithBehavior((inner) => new RepositoryIncludeBehavior<Dinner>(e => e.AuditState, inner));
            .WithBehavior<RepositoryAuditStateBehavior<Dinner>>()
            //.WithBehavior<RepositoryDomainEventBehavior<Dinner>>()
            .WithBehavior<RepositoryDomainEventMetricsBehavior<Dinner>>()
            //.WithBehavior<RepositoryDomainEventPublisherBehavior<Dinner>>();
            .WithBehavior<RepositoryOutboxDomainEventBehavior<Dinner, CoreDbContext>>();

        services.AddEntityFrameworkRepository<Guest, CoreDbContext>()
            .WithTransactions<NullRepositoryTransaction<Guest>>()
            .WithBehavior<RepositoryTracingBehavior<Guest>>()
            .WithBehavior<RepositoryLoggingBehavior<Guest>>()
            //.WithBehavior((inner) => new RepositoryIncludeBehavior<Guest>(e => e.AuditState, inner));
            .WithBehavior<RepositoryAuditStateBehavior<Guest>>()
            //.WithBehavior<RepositoryDomainEventBehavior<Guest>>()
            .WithBehavior<RepositoryDomainEventMetricsBehavior<Guest>>()
            //.WithBehavior<RepositoryDomainEventPublisherBehavior<Guest>>();
            .WithBehavior<RepositoryOutboxDomainEventBehavior<Guest, CoreDbContext>>();

        services.AddEntityFrameworkRepository<Host, CoreDbContext>()
            .WithTransactions<NullRepositoryTransaction<Host>>()
            .WithBehavior<RepositoryTracingBehavior<Host>>()
            .WithBehavior<RepositoryLoggingBehavior<Host>>()
            //.WithBehavior((inner) => new RepositoryIncludeBehavior<Domain.Host>(e => e.AuditState, inner));
            .WithBehavior<RepositoryAuditStateBehavior<Host>>()
            //.WithBehavior<RepositoryDomainEventBehavior<Domain.Host>>()
            .WithBehavior<RepositoryDomainEventMetricsBehavior<Host>>()
            //.WithBehavior<RepositoryDomainEventPublisherBehavior<Domain.Host>>();
            .WithBehavior<RepositoryOutboxDomainEventBehavior<Host, CoreDbContext>>();

        services.AddEntityFrameworkRepository<Menu, CoreDbContext>()
            .WithTransactions<NullRepositoryTransaction<Menu>>()
            .WithBehavior<RepositoryTracingBehavior<Menu>>()
            .WithBehavior<RepositoryLoggingBehavior<Menu>>()
            //.WithBehavior((inner) => new RepositoryIncludeBehavior<Menu>(e => e.AuditState, inner));
            .WithBehavior<RepositoryAuditStateBehavior<Menu>>()
            //.WithBehavior<RepositoryDomainEventBehavior<Menu>>()
            .WithBehavior<RepositoryDomainEventMetricsBehavior<Menu>>()
            //.WithBehavior<RepositoryDomainEventPublisherBehavior<Menu>>();
            .WithBehavior<RepositoryOutboxDomainEventBehavior<Menu, CoreDbContext>>();

        services.AddEntityFrameworkRepository<MenuReview, CoreDbContext>()
            .WithTransactions<NullRepositoryTransaction<MenuReview>>()
            .WithBehavior<RepositoryTracingBehavior<MenuReview>>()
            .WithBehavior<RepositoryLoggingBehavior<MenuReview>>()
            //.WithBehavior((inner) => new RepositoryIncludeBehavior<MenuReview>(e => e.AuditState, inner));
            .WithBehavior<RepositoryAuditStateBehavior<MenuReview>>()
            //.WithBehavior<RepositoryDomainEventBehavior<MenuReview>>()
            .WithBehavior<RepositoryDomainEventMetricsBehavior<MenuReview>>()
            //.WithBehavior<RepositoryDomainEventPublisherBehavior<MenuReview>>();
            .WithBehavior<RepositoryOutboxDomainEventBehavior<MenuReview, CoreDbContext>>();

        services.AddEntityFrameworkRepository<User, CoreDbContext>()
            .WithTransactions<NullRepositoryTransaction<User>>()
            .WithBehavior<RepositoryTracingBehavior<User>>()
            .WithBehavior<RepositoryLoggingBehavior<User>>()
            //.WithBehavior((inner) => new RepositoryIncludeBehavior<Domain.User>(e => e.AuditState, inner));
            .WithBehavior<RepositoryAuditStateBehavior<User>>()
            //.WithBehavior<RepositoryDomainEventBehavior<User>>()
            .WithBehavior<RepositoryDomainEventMetricsBehavior<User>>()
            //.WithBehavior<RepositoryDomainEventPublisherBehavior<User>>();
            .WithBehavior<RepositoryOutboxDomainEventBehavior<User, CoreDbContext>>();

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

//public class DinnerDefaultPermissionProvider : IDefaultEntityPermissionProvider<Dinner>
//{
//    public HashSet<string> GetDefaultPermissions()
//    {
//        return
//        [
//            EntityPermissions.Read,
//        ];
//    }
//}

//public class MenuDefaultPermissionProvider : IDefaultEntityPermissionProvider<Menu>
//{
//    public HashSet<string> GetDefaultPermissions()
//    {
//        return [
//            EntityPermissions.Read,
//        ];
//    }
//}