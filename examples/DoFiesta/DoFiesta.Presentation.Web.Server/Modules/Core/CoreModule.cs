// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Server.Modules.Core;

using Application.Modules.Core;
using Application.Modules.Core.DataPorter;
using BridgingIT.DevKit.Application;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Application.DataPorter;
using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Examples.DoFiesta.Domain;
using BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Server.Modules.Core.DataPorter;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;
using BridgingIT.DevKit.Presentation;
using Common;
using DevKit.Domain.Repositories;
using Domain.Model;
using FluentValidation;
using Infrastructure;
using JobsCronExpressions = BridgingIT.DevKit.Application.Jobs.CronExpressions;

public class CoreModule : WebModuleBase
{
    public override IServiceCollection Register(
        IServiceCollection services,
        IConfiguration configuration = null,
        IWebHostEnvironment environment = null)
    {
        var moduleConfiguration = this.Configure<CoreModuleConfiguration, CoreModuleConfiguration.Validator>(services, configuration);

        // tasks
        services.AddStartupTasks(o => o
            .Enabled()
            .HaltOnFailure())
            .WithTask<CoreDomainSeederTask>(o => o.HaltOnFailure());
        //services.AddStartupTasks(o => o.StartupDelay(moduleConfiguration.SeederTaskStartupDelay))
        //    .WithTask<CoreDomainSeederTask>(o => o
        //        .Enabled(environment?.IsDevelopment() == true)
        //        .StartupDelay(moduleConfiguration.SeederTaskStartupDelay));

        // jobs
        services.AddJobScheduler(configuration)
            .StartupDelay(TimeSpan.TryParse(configuration["JobScheduler:StartupDelay"], out var startupDelay) ? startupDelay : TimeSpan.Zero)
            .WithJob<FileMonitoringLocationScanJob>("scan_inbound", job => job
                .Description("Scans the inbound file-monitoring location.")
                .WithConcurrency(1)
                .WithRetry(retry => retry.MaxAttempts(3).FixedDelay(TimeSpan.FromSeconds(1)))
                .AddTrigger("schedule", trigger => trigger
                    .Cron(JobsCronExpressions.Every5Minutes)
                    .Data(new FileMonitoringLocationScanJobData
                    {
                        LocationName = "inbound",
                        DelayPerFile = TimeSpan.FromMilliseconds(100),
                        FileFilter = "*.*",
                        FileBlackListFilter = ["*.tmp", "*.log"]
                    })))
            .WithJob<FileMonitoringLocationScanJob>("scan_documents", job => job
                .Description("Scans the documents file-monitoring location.")
                .WithConcurrency(1)
                .WithRetry(retry => retry.MaxAttempts(3).FixedDelay(TimeSpan.FromSeconds(1)))
                .AddTrigger("schedule", trigger => trigger
                    .Cron(JobsCronExpressions.EveryMinute)
                    .Data(new FileMonitoringLocationScanJobData
                    {
                        LocationName = "documents",
                        DelayPerFile = TimeSpan.FromMilliseconds(100),
                        FileFilter = "*.*",
                        FileBlackListFilter = ["*.tmp", "*.log"]
                    })))
            .WithJob<EchoJob>("firstecho", job => job
                .Description("Echoes a sample jobs message.")
                .WithRetry(retry => retry.MaxAttempts(3).FixedDelay(TimeSpan.FromSeconds(1)))
                .AddTrigger("schedule", trigger => trigger
                    .Cron(JobsCronExpressions.EveryMinute)
                    .Data(new EchoJobData { Message = "First echo" })))
            .AddEndpoints()
            .AddConsoleCommands();

        // filter
        SpecificationResolver.Register<TodoItem, TodoItemIsNotDeletedSpecification>("TodoItemIsNotDeleted");

        // messaging
        services.AddMessaging(configuration)
            .WithSubscription<TodoItemActivityMessage, TodoItemActivityMessageHandler>();

        // queueing
        services.AddQueueing(configuration)
            .WithBehavior(sp => (IQueueEnqueuerBehavior)new MetricsQueueEnqueuerBehavior(sp.GetService<IMetricsService>()))
            .WithBehavior(sp => (IQueueHandlerBehavior)new MetricsQueueHandlerBehavior(sp.GetService<IMetricsService>()))
            .WithSubscription<TodoItemEchoQueueMessage, TodoItemEchoQueueMessageHandler>();

        // dbcontext
        services.AddSqlServerDbContext<CoreDbContext>(o => o
                .UseConnectionString(moduleConfiguration.ConnectionStrings.GetValueOrDefault("Default"))
                .UseLogger()/*.UseSimpleLogger()*/)
            .WithSequenceNumberGenerator()
            .WithHealthCheck()
            .WithDatabaseCreatorService(o => o
                .Enabled(environment.IsLocalDevelopment())
                .HaltOnFailure().DeleteOnStartup().PurgeOnStartup())
            //.DeleteOnStartup(environment.IsLocalDevelopment()))
            .WithOutboxDomainEventService(o => o
                .AutoArchiveAfter(TimeSpan.FromHours(1))
                .ProcessingModeImmediate()
                .ProcessingInterval("00:00:30")
                .StartupDelay("00:00:15"));
        //.PurgeOnStartup());

        services.AddChangeHistory(options =>
        {
            options.UseOversizedValuePolicy(
                ChangeHistoryOversizedValuePolicy.Truncate,
                maxStoredValueLength: 4000);

            options.Track<TodoItem>()
                .CaptureChanges()
                .CaptureBulkInserts()
                .CaptureCollection<TodoStep, TodoStepId>(e => e.Steps, e => e.Id)
                .HashOnly(e => e.UserId)
                .Redact(e => e.Assignee)
                .Exclude(e => e.Properties)
                .UseRestoreAuthorizer<TodoItemChangeHistoryRestoreAuthorizer>()
                .AllowRestoreUsingValidatedSetters(e => new
                {
                    e.Title,
                    e.Description,
                    e.Category,
                    e.Priority,
                    e.DueDate,
                    e.OrderIndex
                })
                .AllowRestore(e => e.Status).UseDomainMethod((todoItem, value) =>
                {
                    todoItem.SetStatus(value);

                    return Result.Success();
                });

            options.Track<Subscription>()
                .CaptureChanges()
                .HashOnly(e => e.UserId)
                .UseRestoreAuthorizer<SubscriptionChangeHistoryRestoreAuthorizer>()
                .AllowRestoreUsingValidatedSetters(e => new
                {
                    e.Plan,
                    e.Status,
                    e.BillingCycle,
                    e.StartDate,
                    e.EndDate
                });
        })
            .WithReadAuthorizer<CoreDbContext, CoreChangeHistoryReadAuthorizer>()
            .WithRestoreRequestAuthorizer<TodoItem, CoreDbContext, TodoItemChangeHistoryRestoreRequestAuthorizer>()
            .WithRestoreRequestAuthorizer<Subscription, CoreDbContext, SubscriptionChangeHistoryRestoreRequestAuthorizer>();

        services.AddOrchestrations()
            .WithOrchestration<TodoItemLifecycleOrchestration>()
            .WithBehavior<MetricsOrchestrationBehavior>()
            .WithEntityFramework<CoreDbContext>()
            .AddEndpoints();

        // services.AddOrchestrationEndpoints(options => options.RequireAuthorization());

        services.AddScoped<ITodoItemOrchestrationCoordinator, TodoItemOrchestrationCoordinator>();

        services.AddFileStorage(factory => factory
            .RegisterProvider("documents", storage => storage
                .UseEntityFramework<CoreDbContext>(
                    "documents",
                    "Entity Framework backed operational file storage",
                    options => options
                        .PageSize(200)
                        .MaximumBufferedContentSize(8 * 1024 * 1024))
                .WithLifetime(ServiceLifetime.Singleton))
            .RegisterProvider("attachments", storage => storage
                .UseEntityFramework<CoreDbContext>(
                    "attachments",
                    "Entity Framework backed attachment and import file storage",
                    options => options
                        .PageSize(200)
                        .MaximumBufferedContentSize(8 * 1024 * 1024))
                .WithLifetime(ServiceLifetime.Singleton)))
            .AddEndpoints(options => options.RequireAuthorization());

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
                                                                                                             //.AddDefaultPermissions<TodoItem>(Permission.Read, Permission.List) // default permissions if user/group has no grants
                    .UseDefaultPermissionProvider<TodoItem>();

                o.AddEntity<Subscription>(Permission.Read, Permission.Write, Permission.List, Permission.Delete) // allowed permissions -> auth policies
                    .AddDefaultPermissions<Subscription>(Permission.Read, Permission.List) // default permissions if user/group has no grants
                    .UseDefaultPermissionProvider<Subscription>();
            });

            //o.EnableEvaluationEndpoints(o => o.RequireAuthorization = false);
            //o.EnableManagementEndpoints(o => o.RequireAuthorization = false/*o => o.RequireRoles = [Role.Administrators]*/);
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
            b.UseProvider("documents", "documents", o =>
            {
                o.UseOnDemandOnly = true;
                o.RateLimit = RateLimitOptions.MediumSpeed;
                o.FileFilter = "*.*";
                o.FileBlackListFilter = ["*.tmp", "*.log"];
                o.UseProcessor<FileLoggerProcessor>();
            });
            b.UseProvider("attachments", "attachments", o =>
            {
                o.UseOnDemandOnly = true;
                o.RateLimit = RateLimitOptions.MediumSpeed;
                o.FileFilter = "*.*";
                o.FileBlackListFilter = ["*.tmp", "*.log"];
                o.UseProcessor<FileLoggerProcessor>();
            });
        }).WithEntityFrameworkStore<CoreDbContext>();

        // repositories
        services.AddEntityFrameworkRepository<TodoItem, CoreDbContext>()
            .WithTransactions()
            .WithBehavior<RepositoryMetricsBehavior<TodoItem>>()
            .WithBehavior<RepositoryTracingBehavior<TodoItem>>()
            .WithBehavior<RepositoryLoggingBehavior<TodoItem>>()
            .WithBehavior<RepositoryAuditStateBehavior<TodoItem>>()
            .WithBehavior<RepositoryChangeHistoryBehavior<TodoItem, CoreDbContext>>()
            .WithBehavior<RepositoryOutboxDomainEventBehavior<TodoItem, CoreDbContext>>();
        //.WithBehavior<RepositoryDomainEventPublisherBehavior<TodoItem>>();

        services.AddEntityFrameworkBulkInserter<TodoItem, CoreDbContext>()
            .WithBehavior<EntityBulkInserterCancellationBehavior<TodoItem>>()
            .WithBehavior<EntityBulkInserterTracingBehavior<TodoItem>>()
            .WithBehavior<EntityBulkInserterLoggingBehavior<TodoItem>>()
            .WithBehavior<EntityBulkInserterMetricsBehavior<TodoItem>>()
            .WithBehavior<EntityBulkInserterOutboxDomainEventBehavior<TodoItem, CoreDbContext>>()
            .WithBehavior<EntityBulkInserterChangeHistoryBehavior<TodoItem, CoreDbContext>>()
            .WithBehavior<EntityBulkInserterAuditStateBehavior<TodoItem>>()
            .WithBehavior<EntityBulkInserterConcurrencyBehavior<TodoItem>>()
            .WithBehavior<EntityBulkInserterDomainEventBehavior<TodoItem>>()
            .WithBehavior<EntityBulkInserterDomainEventMetricsBehavior<TodoItem>>();

        services.AddEntityFrameworkRepository<Subscription, CoreDbContext>()
            .WithTransactions()
            .WithBehavior<RepositoryMetricsBehavior<Subscription>>()
            .WithBehavior<RepositoryTracingBehavior<Subscription>>()
            .WithBehavior<RepositoryLoggingBehavior<Subscription>>()
            .WithBehavior<RepositoryAuditStateBehavior<Subscription>>()
            .WithBehavior<RepositoryChangeHistoryBehavior<Subscription, CoreDbContext>>()
            .WithBehavior<RepositoryOutboxDomainEventBehavior<Subscription, CoreDbContext>>();
        //.WithBehavior<RepositoryDomainEventPublisherBehavior<Subscription>>();

        // dataporter - register export/import capabilities
        services.AddDataPorter(configuration)
            .WithExcel(c =>
            {
                c.UseTableFormatting = true;
                c.DefaultTableStyleName = "TableStyleMedium2";
                c.AutoFitColumns = true;
                c.FreezeHeaderRow = true;
            })
            .WithCsv(c =>
            {
                c.Delimiter = ",";
                c.IncludeHeader = true;
                c.TrimFields = true;
            })
            .WithJson(c =>
            {
                c.WriteIndented = true;
                c.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                // c.SerializerOptions = new System.Text.Json.JsonSerializerOptions
                // {
                //     WriteIndented = true,
                //     PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                //     DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                // };
            })
            .WithXml(c =>
            {
                c.RootElementName = "TodoItems";
                c.ItemElementName = "TodoItem";
                c.WriteIndented = true;
            })
            .WithPdf(c =>
            {
                c.PageSize = PdfPageSize.A4;
                c.Orientation = PdfPageOrientation.Landscape;
                c.Title = "DoFiesta Todo Items";
                c.HeaderText = "DoFiesta Todo Items Export";
                c.ShowPageNumbers = true;
                c.ShowGenerationDate = true;
            })
            .AddExportProfile<TodoItemExportProfile>()
            .AddImportProfile<TodoItemImportProfile>()
            .AddImportRowInterceptor<TodoItemBulkImportPersistenceInterceptor>();

        // endpoints
        services.AddEndpoints<CoreTodoItemEndpoints>();
        services.AddEndpoints<CoreEnumerationEndpoints>();
        services.AddEndpoints<CoreDataPorterEndpoints>();
        services.AddChangeHistoryEndpoints<TodoItem, CoreDbContext>(options => options
            .GroupPath("api/core/todoitems/history")
            .GroupTag("Core.TodoItem.ChangeHistory")
            .RouteNamePrefix("Core.TodoItem.ChangeHistory")
            .RequireAuthorization()
            .IncludeValues());
        services.AddChangeHistoryEndpoints<Subscription, CoreDbContext>(options => options
            .GroupPath("api/core/subscriptions/history")
            .GroupTag("Core.Subscription.ChangeHistory")
            .RouteNamePrefix("Core.Subscription.ChangeHistory")
            .RequireAuthorization()
            .IncludeValues());

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