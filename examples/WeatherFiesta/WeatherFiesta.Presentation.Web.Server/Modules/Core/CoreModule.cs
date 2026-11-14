// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Presentation.Web.Server.Modules.Core;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Application.Orchestrations;
using BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;
using BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core.Orchestrations;
using BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core.Tasks;
using BridgingIT.DevKit.Examples.WeatherFiesta.Infrastructure;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;
using BridgingIT.DevKit.Presentation;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Core module for WeatherFiesta registering DbContext, repositories, jobs, and endpoints.
/// </summary>
public class CoreModule : WebModuleBase
{
    /// <inheritdoc />
    public override IServiceCollection Register(
        IServiceCollection services,
        IConfiguration configuration = null,
        IWebHostEnvironment environment = null)
    {
        // Configuration
        var moduleConfiguration = this.Configure<CoreModuleConfiguration, CoreModuleConfiguration.Validator>(services, configuration);

        // Orchestrations
        services.AddOrchestrations()
            .WithAuditingBehavior()
            .WithOrchestration<WeatherHelloWorldOrchestration>()
            .WithProfilingBehavior()
            .WithBehavior<MetricsOrchestrationBehavior>()
            .WithEntityFramework<CoreDbContext>()
            .AddEndpoints();

        // Jobs
        services.AddJobScheduler(configuration)
            .WithAuditing()
            .StartupDelay(TimeSpan.FromSeconds(30))
            .WorkerPool(p => p
                .MaxConcurrency(16)
                .BatchSize(32)
                .PollInterval(TimeSpan.FromSeconds(5)))
            .WithJob<WeatherIngestionJob>("core_ingestion", c => c
                    .Description("Ingests stale weather data for WeatherFiesta cities.")
                    .UseLifetime(ServiceLifetime.Scoped)
                    .WithConcurrency(1)
                    .AddTrigger("cron", t => t
                            .Cron(moduleConfiguration.Jobs.IngestionCron)
                            .WithMissedOccurrencePolicy(JobMissedOccurrencePolicy.RunOnce))
                    .AddTrigger("manual", t => t.Manual()))
            .WithJob<WeatherCleanupJob>("core_cleanup", c => c
                    .Description("Deletes stale weather data for WeatherFiesta cities.")
                    .UseLifetime(ServiceLifetime.Scoped)
                    .WithConcurrency(1)
                    .AddTrigger("cron", t => t
                            .Cron(moduleConfiguration.Jobs.CleanupCron)
                            .WithMissedOccurrencePolicy(JobMissedOccurrencePolicy.RunOnce))
                    .AddTrigger("manual", t => t.Manual()))
            .WithJob<WeatherErrorSimulationJob>("core_error_simulation", c => c
                    .Description("Generates synthetic WeatherFiesta errors for dashboard validation.")
                    .UseLifetime(ServiceLifetime.Scoped)
                    .WithConcurrency(1)
                    .AddTrigger("manual", t => t.Manual()))
            .WithJob<WeatherProfilingStressJob>("core_profiling_stress", c => c
                    .Description("Generates bounded CPU, allocation, heap, and GC pressure for local Profiling validation.")
                    .Enabled(environment?.IsDevelopment() == true)
                    .UseLifetime(ServiceLifetime.Scoped)
                    .WithConcurrency(1)
                    .WithTimeout(TimeSpan.FromSeconds(30))
                    .AddTrigger("manual", t => t.Manual()))
            .WithOrchestrationExecuteJob<Unit, WeatherHelloWorldOrchestration, WeatherHelloWorldOrchestrationData>("core_hello_world_orchestration", c => c
                    .WithDescription("Starts the WeatherFiesta hello-world orchestration.")
                    .WithConcurrency(1)
                    .WithInput(ctx => new WeatherHelloWorldOrchestrationData
                    {
                        Greeting = "Hello WeatherFiesta",
                        Source = ctx.JobName,
                        RequestedUtc = DateTimeOffset.UtcNow,
                    })
                    .Dispatch()
                    .AddTrigger("cron", t => t
                            .Cron(CronExpressions.Every5Minutes)
                            .WithMissedOccurrencePolicy(JobMissedOccurrencePolicy.RunOnce))
                    .AddTrigger("manual", t => t.Manual()))
            .WithEntityFramework<CoreDbContext>()
            .WithBehavior<JobProfilingBehavior>()
            .WithBehavior<JobMetricsBehavior>()
            .WithBehavior<ModuleScopeBehavior>()
            .AddEndpoints()
            .AddConsoleCommands();

        // Database configuration
        services.AddSqlServerDbContext<CoreDbContext>(o => o
                .UseConnectionString(moduleConfiguration.ConnectionStrings.GetValueOrDefault("Default"))
                .UseLogger() /*.UseSimpleLogger()*/)
            .WithDbContextFactory()
            .WithHealthCheck()
            .WithDatabaseCreatorService(o => o
                .Enabled(environment.IsLocalDevelopment())
                .HaltOnFailure()
                .DeleteOnStartup(environment
                    .IsLocalDevelopment())) // Warning: This will delete the database on startup in local development environment so it can be recreated with fresh schema and seed data
            .WithOutboxDomainEventService(o => o
                .AutoArchiveAfter(TimeSpan.FromHours(1))
                .ProcessingModeImmediate()
                .ProcessingInterval("00:00:30")
                .StartupDelay("00:00:15"));
                //.PurgeOnStartup());

        services.AddActiveEntities();

        // application caching
        services.AddApplicationCaching(c => c
            .Namespace("weatherfiesta")
            .Diagnostics(d => d.HistoryRetention(TimeSpan.FromHours(1)))
            .WithEntityFrameworkProvider<CoreDbContext>("core", p => p.DatabaseReadyAlias(nameof(CoreDbContext)))
            .WithCollection<CitySuggestionModel[]>(CitySuggestionService.CollectionName,
                c => c
                    .UseProvider("core").ShareLoadsAcrossNodes().ExpireAfter(TimeSpan.FromHours(6))
                    .Limits(l => l.MaxStorageBytes(ByteSize.Megabytes(16)).MaxEntries(1000)
                        .MaxPayloadBytes(ByteSize.Kilobytes(128)).MaxDecodedBytes(ByteSize.Megabytes(1)))
                    .WithMetrics())
            .WithDashboard(o => o.WithSensitiveMetadata()));
        services.AddScoped<CitySuggestionService>();

        services.AddBusinessCalendars(c => c
            .SetDefault(CreateBusinessCalendar())
            .RegisterCountry("NL", CreateBusinessCalendar())
            .RegisterCountry("DE", CreateBusinessCalendar())
            .RegisterCountry("FR", CreateBusinessCalendar())
            .RegisterCountry("GB", CreateBusinessCalendar()));

        services.AddDataPorter(configuration)
            .WithAuditing()
            .WithCsv(c =>
            {
                c.Delimiter = ",";
                c.IncludeHeader = true;
                c.TrimFields = true;
            })
            .AddExportProfile<WeatherForecastExportProfile>();

        // Startup tasks
        services.AddStartupTasks(o => o
                .Enabled().HaltOnFailure())
            .WithBehavior<BridgingIT.DevKit.Application.Utilities.AuditingStartupTaskBehavior>()
            .WithTask<BlobStorageSeederTask>(o => o.HaltOnFailure())
            .WithTask<CoreSeederTask>(o => o.HaltOnFailure());

        // Open-Meteo client
        services.AddOpenMeteo(moduleConfiguration);

        // Weather report text generation
        services.AddScoped<WeatherCityReviewService>();
        services.AddPipelines()
            .WithPipeline<WeatherIngestionPipeline>();
        services.AddWeatherReportTextGeneration(configuration);

        // Application console commands
        services.AddTransient<IConsoleCommand, CityListConsoleCommand>();
        services.AddTransient<IConsoleCommand, CityCurrentConsoleCommand>();
        services.AddTransient<IConsoleCommand, CityForecastConsoleCommand>();
        services.AddTransient<IConsoleCommand, CityCreateConsoleCommand>();
        services.AddTransient<IConsoleCommand, CityDeleteConsoleCommand>();
        services.AddTransient<IConsoleCommand, CityResetConsoleCommand>();

        // Endpoints
        services.AddEndpoints<CityEndpoints>();
        services.AddEndpoints<WeatherEndpoints>();
        services.AddEndpoints<WeatherProfilingEndpoints>();
        services.AddEndpoints<UserEndpoints>();
        services.AddEndpoints<DashboardEndpoints>();
        services.AddEndpoints<AdminEndpoints>();
        services.AddEndpoints<SubscriptionEndpoints>();

        return services;
    }

    /// <inheritdoc />
    public override IApplicationBuilder Use(
        IApplicationBuilder app,
        IConfiguration configuration = null,
        IWebHostEnvironment environment = null)
    {
        return app;
    }

    /// <inheritdoc />
    public override IEndpointRouteBuilder Map(
        IEndpointRouteBuilder app,
        IConfiguration configuration = null,
        IWebHostEnvironment environment = null)
    {
        return app;
    }

    private static IBusinessCalendar CreateBusinessCalendar()
    {
        return new DynamicBusinessCalendar(
            new CalculatedHolidayProvider([
                new CalculatedHoliday("New Year's Day", year => new DateOnly(year, 1, 1)),
                new CalculatedHoliday("Good Friday", year => HolidayCalculations.GregorianEasterSunday(year).AddDays(-2)),
                new CalculatedHoliday("Easter Monday", year => HolidayCalculations.GregorianEasterSunday(year).AddDays(1)),
                new CalculatedHoliday("Labour Day", year => new DateOnly(year, 5, 1)),
                new CalculatedHoliday("Christmas Day", year => new DateOnly(year, 12, 25)),
                new CalculatedHoliday("Boxing Day", year => new DateOnly(year, 12, 26))
            ]));
    }
}