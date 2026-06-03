// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Presentation.Web.Server.Modules.Core;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;
using BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core.Tasks;
using BridgingIT.DevKit.Presentation;
using BridgingIT.DevKit.Examples.WeatherFiesta.Infrastructure;
using BridgingIT.DevKit.Examples.WeatherFiesta.Infrastructure.OpenMeteo;
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

        // Startup tasks
        services.AddStartupTasks(o => o
                .Enabled()
                .HaltOnFailure())
            .WithTask<CoreSeederTask>(o => o.HaltOnFailure());

        // Orchestrations
        //services.AddOrchestrations()
        //    .WithOrchestration<TodoItemLifecycleOrchestration>()
        //    .WithBehavior<MetricsOrchestrationBehavior>()
        //    .WithEntityFramework<CoreDbContext>()
        //    .AddEndpoints();

        // Jobs
        services.AddJobScheduler(configuration)
            .StartupDelay(TimeSpan.FromSeconds(15))
            .WithJob<WeatherIngestionJob>("core_ingestion", job => job
                .Description("Ingests stale weather data for WeatherFiesta cities.")
                .UseLifetime(ServiceLifetime.Scoped)
                .WithConcurrency(1)
                .AddTrigger("cron", trigger => trigger
                    .Cron(moduleConfiguration.Jobs.IngestionCron)
                    .WithMissedOccurrencePolicy(JobMissedOccurrencePolicy.RunOnce))
                .AddTrigger("manual", trigger => trigger.Manual()))
            .WithJob<WeatherCleanupJob>("core_cleanup", job => job
                .Description("Deletes stale weather data for WeatherFiesta cities.")
                .UseLifetime(ServiceLifetime.Scoped)
                .WithConcurrency(1)
                .AddTrigger("cron", trigger => trigger
                    .Cron(moduleConfiguration.Jobs.CleanupCron)
                    .WithMissedOccurrencePolicy(JobMissedOccurrencePolicy.RunOnce))
                .AddTrigger("manual", trigger => trigger.Manual()))
            .WithEntityFramework<CoreDbContext>()
            .WithBehavior<JobMetricsBehavior>()
            .WithBehavior<ModuleScopeBehavior>()
            .AddEndpoints()
            .AddConsoleCommands();

        // Database configuration
        services.AddSqlServerDbContext<CoreDbContext>(o => o
                .UseConnectionString(moduleConfiguration.ConnectionStrings.GetValueOrDefault("Default"))
                .UseLogger()/*.UseSimpleLogger()*/)
            .WithHealthCheck()
            .WithDatabaseCreatorService(o => o
                .Enabled(environment.IsLocalDevelopment())
                .HaltOnFailure()
                .DeleteOnStartup(environment.IsLocalDevelopment()))
            .WithOutboxDomainEventService(o => o
                .AutoArchiveAfter(TimeSpan.FromHours(1))
                .ProcessingModeImmediate()
                .ProcessingInterval("00:00:30")
                .StartupDelay("00:00:15"));
                //.PurgeOnStartup());

        services.AddActiveEntities();

       // Open-Meteo client
        services.AddOpenMeteo(moduleConfiguration);

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
}
