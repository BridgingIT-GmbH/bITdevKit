// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Server.Modules.Core;

using System.Globalization;
using Application.Modules.Core;
using Application.Modules.Core.DataPorter;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Application.DataPorter;
using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Examples.DoFiesta.Domain;
using BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Server.Modules.Core.DataPorter;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;
using BridgingIT.DevKit.Presentation;
using Common;
using DevKit.Domain.Repositories;
using Domain.Model;
using Infrastructure;
using JobsCronExpressions = BridgingIT.DevKit.Application.Jobs.CronExpressions;

public class CoreModule : WebModuleBase
{
    private static readonly object SpecificationRegistrationLock = new();

    public override IServiceCollection Register(
        IServiceCollection services,
        IConfiguration configuration = null,
        IWebHostEnvironment environment = null)
    {
        var moduleConfiguration = this.Configure<CoreModuleConfiguration, CoreModuleConfiguration.Validator>(services, configuration);

        services.AddSingleton<EnumerationCatalog>();
        services.AddSingleton<TaskLookupCatalog>();

        // tasks
        services.AddStartupTasks(o => o
                .Enabled()
                .HaltOnFailure())
            .WithTask<CoreDomainSeederTask>(o => o.HaltOnFailure())
            .WithTask<CoreCacheSeederTask>(o => o.HaltOnFailure())
            .WithTask<TaskLookupSeederTask>(o => o.HaltOnFailure());
        //services.AddStartupTasks(o => o.StartupDelay(moduleConfiguration.SeederTaskStartupDelay))
        //    .WithTask<CoreDomainSeederTask>(o => o
        //        .Enabled(environment?.IsDevelopment() == true)
        //        .StartupDelay(moduleConfiguration.SeederTaskStartupDelay));

        // jobs
        services.AddJobScheduler(configuration)
            .WithAuditing()
            .StartupDelay(TimeSpan.TryParse(configuration["JobScheduler:StartupDelay"], CultureInfo.InvariantCulture, out var startupDelay) ? startupDelay : TimeSpan.Zero)
            .WithJob<FileMonitoringLocationScanJob>("scan_inbound",
                c => c
                    .Description("Scans the inbound file-monitoring location.")
                    .WithConcurrency(1)
                    .WithRetry(r => r.MaxAttempts(3).FixedDelay(TimeSpan.FromSeconds(1)))
                    .AddTrigger("schedule",
                        t => t
                            .Cron(JobsCronExpressions.Every5Minutes)
                            .Data(new FileMonitoringLocationScanJobData
                            {
                                LocationName = "inbound",
                                DelayPerFile = TimeSpan.FromMilliseconds(100),
                                FileFilter = "*.*",
                                FileBlackListFilter = ["*.tmp", "*.log"]
                            })))
            .WithJob<FileMonitoringLocationScanJob>("scan_documents",
                c => c
                    .Description("Scans the documents file-monitoring location.")
                    .WithConcurrency(1)
                    .WithRetry(r => r.MaxAttempts(3).FixedDelay(TimeSpan.FromSeconds(1)))
                    .AddTrigger("schedule",
                        t => t
                            .Cron(JobsCronExpressions.EveryMinute)
                            .Data(new FileMonitoringLocationScanJobData
                            {
                                LocationName = "documents",
                                DelayPerFile = TimeSpan.FromMilliseconds(100),
                                FileFilter = "*.*",
                                FileBlackListFilter = ["*.tmp", "*.log"]
                            })))
            .WithJob<EchoJob>("firstecho",
                c => c
                    .Description("Echoes a sample jobs message.")
                    .WithRetry(r => r.MaxAttempts(3).FixedDelay(TimeSpan.FromSeconds(1)))
                    .AddTrigger("schedule",
                        t => t
                            .Cron(JobsCronExpressions.EveryMinute)
                            .Data(new EchoJobData { Message = "First echo" })))
            .AddConsoleCommands();

        // The specification registry is shared by all hosts in the process.
        const string specificationName = "TodoItemIsNotDeleted";
        lock (SpecificationRegistrationLock)
        {
            if (!SpecificationResolver.IsRegistered(specificationName) ||
                SpecificationResolver.GetType(specificationName) != typeof(TodoItemIsNotDeletedSpecification))
            {
                SpecificationResolver.Register<TodoItem, TodoItemIsNotDeletedSpecification>(specificationName);
            }
        }

        // messaging
        services.AddMessaging(configuration)
            .WithSubscription<TodoItemActivityMessage, TodoItemActivityMessageHandler>();

        // queueing
        services.AddQueueing(configuration)
            .WithBehavior(sp => new MetricsQueueEnqueuerBehavior(sp.GetService<IMetricsService>()))
            .WithBehavior(sp => new MetricsQueueHandlerBehavior(sp.GetService<IMetricsService>()))
            .WithSubscription<TodoItemEchoQueueMessage, TodoItemEchoQueueMessageHandler>();

        // dbcontext
        services.AddSqlServerDbContext<CoreDbContext>(o => o
                .UseConnectionString(moduleConfiguration.ConnectionStrings.GetValueOrDefault("Default"))
                .UseLogger() /*.UseSimpleLogger()*/)
            .WithDbContextFactory()
            .WithSequenceNumberGenerator()
            .WithHealthCheck()
            .WithDatabaseCreatorService(o => o
                .Enabled(environment.IsLocalDevelopment())
                .HaltOnFailure()
                .DeleteOnStartup(environment.IsLocalDevelopment())) // drop and recreate the database on a local dev box
            .WithOutboxDomainEventService(o => o
                .AutoArchiveAfter(TimeSpan.FromHours(1))
                .ProcessingModeImmediate()
                .ProcessingInterval("00:00:30")
                .StartupDelay("00:00:15"));
        //.PurgeOnStartup());

        // application caching
        services.AddApplicationCaching(c => c
            .Namespace("dofiesta")
            .Diagnostics(d => d
                .HistoryRetention(TimeSpan.FromHours(1)))
            .WithEntityFrameworkProvider<CoreDbContext>("core", p => p
                    .DatabaseReadyAlias(nameof(CoreDbContext)).MaxStorageBytes(ByteSize.Megabytes(128)).MaxEntries(2048))
            .WithInMemoryProvider("demo-memory", p => p
                    .Capacity(ByteSize.Megabytes(32)).MaxEntries(128))
            .WithInMemoryProvider("lookups-memory", p => p
                    .Capacity(ByteSize.Megabytes(8)).MaxEntries(256))
            .WithCollection<string>(EnumerationCatalog.CollectionName, c => c
                    .UseProvider("core")
                    .ExpireAfter(TimeSpan.FromHours(1))
                    .ShareLoadsAcrossNodes()
                    .Limits(l => l
                        .MaxStorageBytes(ByteSize.Megabytes(16)).MaxEntries(128).MaxPayloadBytes(ByteSize.Kilobytes(64))))
            .WithCollection<string>(CoreCacheSeederTask.CollectionName, c => c
                    .UseProvider("demo-memory").ExpireAfter(TimeSpan.FromHours(1))
                    .Limits(l => l
                        .MaxPayloadBytes(ByteSize.Megabytes(2)))
                    .Dimensions(d => d
                        .Scalar<string>("category")
                        .Scalar<long>("payloadBytes")
                        .Scalar<bool>("active")
                        .Set<string>("tags")
                        .Scalar<long>("sequence")))
            .WithCollection<TaskLookupModel>(TaskLookupCatalog.CollectionName, c => c
                    .UseProvider("lookups-memory").ExpireAfter(TimeSpan.FromHours(1))
                    .Limits(l => l
                        .MaxStorageBytes(ByteSize.Megabytes(4)).MaxPayloadBytes(ByteSize.Kilobytes(64)).MaxDecodedBytes(ByteSize.Kilobytes(256)))
                    .Dimensions(d => d
                        .Scalar<string>("category")
                        .Scalar<string>("technology")
                        .Scalar<string>("language")))
            .WithMetrics()
            .WithDashboard(o => o.WithSensitiveMetadata()));

        services.AddChangeHistory(o =>
            {
                o.UseOversizedValuePolicy(
                    ChangeHistoryOversizedValuePolicy.Truncate,
                    maxStoredValueLength: 4000);

                o.Track<TodoItem>()
                    .CaptureChanges()
                    .CaptureBulkInserts()
                    .CaptureCollection(e => e.Steps, e => e.Id)
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

                o.Track<Subscription>()
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
            .WithAuditing<CoreDbContext>()
            .WithReadAuthorizer<CoreDbContext, CoreChangeHistoryReadAuthorizer>()
            .WithRestoreRequestAuthorizer<TodoItem, CoreDbContext, TodoItemChangeHistoryRestoreRequestAuthorizer>()
            .WithRestoreRequestAuthorizer<Subscription, CoreDbContext, SubscriptionChangeHistoryRestoreRequestAuthorizer>();

        services.AddOrchestrations()
            .WithAuditingBehavior()
            .WithOrchestration<TodoItemLifecycleOrchestration>()
            .WithBehavior<MetricsOrchestrationBehavior>()
            .WithEntityFramework<CoreDbContext>()
            .AddMcpHandlers();

        services.AddScoped<ITodoItemOrchestrationCoordinator, TodoItemOrchestrationCoordinator>();

        services.AddFileStorage(f => f
            .RegisterProvider("documents",
                c => c
                    .UseEntityFramework<CoreDbContext>(
                        "documents",
                        "Entity Framework backed operational file storage",
                        o => o
                            .PageSize(200)
                            .MaximumBufferedContentSize(8 * 1024 * 1024))
                    .WithAuditing()
                    .WithLifetime(ServiceLifetime.Singleton))
            .RegisterProvider("attachments",
                c => c
                    .UseEntityFramework<CoreDbContext>(
                        "attachments",
                        "Entity Framework backed attachment and import file storage",
                        o => o
                            .PageSize(200)
                            .MaximumBufferedContentSize(8 * 1024 * 1024))
                    .WithAuditing()
                    .WithLifetime(ServiceLifetime.Singleton)));

        //services.AddInMemoryDbContext<CoreDbContext>()
        //    .WithDatabaseCreatorService(o => o
        //        .Enabled(environment?.IsDevelopment() == true));

        // permissions
        services.AddEntityAuthorization(c =>
        {
            c.WithEntityPermissions<CoreDbContext>(o =>
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
        services.AddFileMonitoring(c =>
        {
            c.WithAuditing();
            c.UseLocal("inbound",
                Path.Combine(Path.GetTempPath(), "DoFiesta-inbound"),
                o =>
                {
                    o.UseOnDemandOnly = true; // On-demand only
                    o.RateLimit = RateLimitOptions.MediumSpeed;
                    o.FileFilter = "*.*";
                    o.FileBlackListFilter = ["*.tmp"];
                    o.UseProcessor<FileLoggerProcessor>();
                });
            c.UseProvider("documents",
                "documents",
                o =>
                {
                    o.UseOnDemandOnly = true;
                    o.RateLimit = RateLimitOptions.MediumSpeed;
                    o.FileFilter = "*.*";
                    o.FileBlackListFilter = ["*.tmp", "*.log"];
                    o.UseProcessor<FileLoggerProcessor>();
                });
            c.UseProvider("attachments",
                "attachments",
                o =>
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
            .WithAuditing()
            .WithBehavior<RepositoryMetricsBehavior<TodoItem>>()
            .WithBehavior<RepositoryTracingBehavior<TodoItem>>()
            .WithBehavior<RepositoryLoggingBehavior<TodoItem>>()
            .WithBehavior<RepositoryAuditStateBehavior<TodoItem>>()
            .WithBehavior<RepositoryChangeHistoryBehavior<TodoItem, CoreDbContext>>()
            .WithBehavior<RepositoryOutboxDomainEventBehavior<TodoItem, CoreDbContext>>();
        //.WithBehavior<RepositoryDomainEventPublisherBehavior<TodoItem>>();

        services.AddEntityFrameworkBulkInserter<TodoItem, CoreDbContext>()
            .WithAuditing()
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
            .WithAuditing()
            .WithBehavior<RepositoryMetricsBehavior<Subscription>>()
            .WithBehavior<RepositoryTracingBehavior<Subscription>>()
            .WithBehavior<RepositoryLoggingBehavior<Subscription>>()
            .WithBehavior<RepositoryAuditStateBehavior<Subscription>>()
            .WithBehavior<RepositoryChangeHistoryBehavior<Subscription, CoreDbContext>>()
            .WithBehavior<RepositoryOutboxDomainEventBehavior<Subscription, CoreDbContext>>();
        //.WithBehavior<RepositoryDomainEventPublisherBehavior<Subscription>>();

        // dataporter - register export/import capabilities
        services.AddDataPorter(configuration)
            .WithAuditing()
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
        services.AddChangeHistoryDashboard<TodoItem, CoreDbContext>(o => o.IncludeValues = true);
        services.AddChangeHistoryDashboard<Subscription, CoreDbContext>(o => o.IncludeValues = true);
        services.AddEndpoints<CoreTodoItemEndpoints>();
        services.AddEndpoints<CoreEnumerationEndpoints>();
        services.AddEndpoints<CoreTaskLookupEndpoints>();
        services.AddEndpoints<CoreDataPorterEndpoints>();
        services.AddChangeHistoryEndpoints<TodoItem, CoreDbContext>(o => o
            .GroupPath("api/core/todoitems/history")
            .GroupTag("Core.TodoItem.ChangeHistory")
            .RouteNamePrefix("Core.TodoItem.ChangeHistory")
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
