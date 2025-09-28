// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Server.Modules.Core;

using Application.Modules.Core;
using BridgingIT.DevKit.Application;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Examples.DoFiesta.Domain;
using BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Server.Modules.Core.Controllers;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using BridgingIT.DevKit.Presentation;
using Common;
using DevKit.Domain.Repositories;
using Domain.Model;
using FluentValidation;
using Infrastructure;
using static BridgingIT.DevKit.Application.Storage.FileMonitoringLocationScanJob;

public class CoreModule : WebModuleBase
{
    public override IServiceCollection Register(
        IServiceCollection services,
        IConfiguration configuration = null,
        IWebHostEnvironment environment = null)
    {
        var moduleConfiguration = this.Configure<CoreModuleConfiguration, CoreModuleConfiguration.Validator>(services, configuration);

        // tasks
        services.AddStartupTasks(o => o.StartupDelay(moduleConfiguration.SeederTaskStartupDelay))
            .WithTask<CoreDomainSeederTask>(o => o
                //.Enabled(environment?.IsDevelopment() == true)
                .StartupDelay(moduleConfiguration.SeederTaskStartupDelay));
        // jobs
        services.AddJobScheduling(o => o
            .Enabled().StartupDelay(configuration["JobScheduling:StartupDelay"]), configuration)
            .WithSqlServerStore(configuration["Modules:Core:ConnectionStrings:Default"])
            .WithJob<FileMonitoringLocationScanJob>()
                .Cron(CronExpressions.Every5Minutes)
                .Named("scan_inbound")
                .WithData(DataKeys.LocationName, "inbound")
                .WithData(DataKeys.DelayPerFile, "00:00:00:100")
                .WithData(DataKeys.FileFilter, "*.*")
                .WithData(DataKeys.FileBlackListFilter, "*.tmp;*.log")
                .RegisterScoped()
            //.WithJob<EchoJob>()
            //    .Cron(CronExpressions.EveryMinute)
            //    .Named("firstecho")
            //    .WithData("message", "First echo")
            //    .RegisterScoped()
            //.WithJob<EchoJob>()
            //    .Cron(CronExpressions.Every5Seconds)
            //    .Named("secondecho")
            //    .WithData("message", "Second echo")
            //    .Enabled(environment?.IsDevelopment() == true)
            //    .RegisterScoped()
            //.WithJob<EchoJob>()
            //    .Cron(b => b.DayOfMonth(1).AtTime(23, 59).Build()) // "0 59 23 1 * ?"
            //    .Named("thirdecho")
            //    .WithData("message", "Third echo")
            //    .Enabled(environment?.IsDevelopment() == true)
            //    .RegisterScoped()
            .AddEndpoints();

        // filter
        SpecificationResolver.Register<TodoItem, TodoItemIsNotDeletedSpecification>("TodoItemIsNotDeleted");

        // dbcontext
        services.AddSqlServerDbContext<CoreDbContext>(o => o
                .UseConnectionString(moduleConfiguration.ConnectionStrings["Default"])
                .UseLogger()/*.UseSimpleLogger()*/)
            //.WithDatabaseCheckerService()
            .WithDatabaseCreatorService(o => o
                .Enabled(environment.IsLocalDevelopment())
                .DeleteOnStartup(environment.IsLocalDevelopment()));

        //services.AddInMemoryDbContext<CoreDbContext>()
        //    .WithDatabaseCreatorService(o => o
        //        .Enabled(environment?.IsDevelopment() == true));

        // permissions
        services.AddEntityAuthorization(o =>
        {
            o.WithEntityPermissions<CoreDbContext>(o =>
            {
                // Register entities that need permission checks + auth policies
                o.AddEntity<TodoItem>(Permission.Read, Permission.Write, Permission.List, Permission.Delete) // allowed permissions -> auth policies
                    .AddDefaultPermissions<TodoItem>(Permission.Read, Permission.List) // default permissions if user/group has no grants
                    .UseDefaultPermissionProvider<TodoItem>();

                o.AddEntity<Subscription>(Permission.Read, Permission.Write, Permission.List, Permission.Delete) // allowed permissions -> auth policies
                    .AddDefaultPermissions<Subscription>(Permission.Read, Permission.List) // default permissions if user/group has no grants
                    .UseDefaultPermissionProvider<Subscription>();
            });

            o.EnableEvaluationEndpoints(o => o.RequireAuthorization = false);
            o.EnableManagementEndpoints(o => o.RequireAuthorization = false/*o => o.RequireRoles = [Role.Administrators]*/);
        });

        // file monitoring
        services.AddFileMonitoring(b =>
        {
            b.UseLocal("inbound", Path.Combine(Path.GetTempPath(), "DoFiesta-inbound"), o =>
            {
                o.UseOnDemandOnly = true; // On-demand only
                o.RateLimit = RateLimitOptions.MediumSpeed;
                o.FileFilter = "*.*";
                o.FileBlackListFilter = ["*.tmp"];
                o.UseProcessor<FileLoggerProcessor>();
            });
        }).WithEntityFrameworkStore<CoreDbContext>();

        // repositories
        services.AddEntityFrameworkRepository<TodoItem, CoreDbContext>()
            .WithBehavior<RepositoryTracingBehavior<TodoItem>>()
            .WithBehavior<RepositoryLoggingBehavior<TodoItem>>()
            .WithBehavior<RepositoryAuditStateBehavior<TodoItem>>();

        services.AddEntityFrameworkRepository<Subscription, CoreDbContext>()
            .WithBehavior<RepositoryTracingBehavior<Subscription>>()
            .WithBehavior<RepositoryLoggingBehavior<Subscription>>()
            .WithBehavior<RepositoryAuditStateBehavior<Subscription>>();

        // endpoints
        services.AddEndpoints<CoreTodoItemEndpoints>();
        services.AddEndpoints<CoreEnumerationEndpoints>();

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
        // TODO: map the endpoints here (replaces TodoItemController)

        return app;
    }
}