// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Presentation.Web.Server.Modules.Core;

using BridgingIT.DevKit.Application.JobScheduling;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core.Abstractions;
using BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core.ConsoleCommands;
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
        var moduleConfiguration = this.Configure<CoreModuleConfiguration, CoreModuleConfiguration.Validator>(services, configuration);

        // DbContext
        services.AddSqlServerDbContext<CoreDbContext>(o => o
                .UseConnectionString(moduleConfiguration.ConnectionStrings.GetValueOrDefault("Default"))
                .UseLogger()/*.UseSimpleLogger()*/)
            .WithHealthCheck()
            //.WithDatabaseMigratorService(o => o
            .WithDatabaseCreatorService(o => o
                .Enabled()
                .DeleteOnStartup()
                .HaltOnFailure())
            .WithOutboxDomainEventService(o => o
                .AutoArchiveAfter(TimeSpan.FromHours(1))
                .ProcessingModeImmediate()
                .ProcessingInterval("00:00:30")
                .StartupDelay("00:00:15"));

        // ActiveEntity registrations
        services.AddActiveEntity(cfg =>
        {
            cfg.For<City, CityId>()
                .UseEntityFrameworkProvider(o => o.Context<CoreDbContext>())
                .AddLoggingBehavior()
                .AddAuditStateBehavior(o => o.SoftDeleteEnabled = true)
                .AddDomainEventPublishingBehavior(new ActiveEntityDomainEventPublishingBehaviorOptions { PublishBefore = false });

            cfg.For<UserCity, UserCityId>()
                .UseEntityFrameworkProvider(o => o.Context<CoreDbContext>())
                .AddLoggingBehavior()
                .AddAuditStateBehavior(o => o.SoftDeleteEnabled = true);

            cfg.For<CurrentWeather, CurrentWeatherId>()
                .UseEntityFrameworkProvider(o => o.Context<CoreDbContext>())
                .AddLoggingBehavior();

            cfg.For<WeatherForecast, WeatherForecastId>()
                .UseEntityFrameworkProvider(o => o.Context<CoreDbContext>())
                .AddLoggingBehavior();

            cfg.For<UserProfile, UserProfileId>()
                .UseEntityFrameworkProvider(o => o.Context<CoreDbContext>())
                .AddLoggingBehavior()
                .AddAuditStateBehavior(o => o.SoftDeleteEnabled = true);

            cfg.For<UserSubscription, UserSubscriptionId>()
                .UseEntityFrameworkProvider(o => o.Context<CoreDbContext>())
                .AddLoggingBehavior()
                .AddAuditStateBehavior(o => o.SoftDeleteEnabled = true)
                .AddDomainEventPublishingBehavior(new ActiveEntityDomainEventPublishingBehaviorOptions { PublishBefore = false });
        });

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

        // Open-Meteo client
        services.AddHttpClient<IOpenMeteoClient, OpenMeteoClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(moduleConfiguration.OpenMeteo.TimeoutSeconds);
        });
        services.Configure<OpenMeteoClientOptions>(options =>
        {
            options.GeocodingBaseUrl = moduleConfiguration.OpenMeteo.GeocodingBaseUrl;
            options.ForecastBaseUrl = moduleConfiguration.OpenMeteo.ForecastBaseUrl;
            options.LookupBaseUrl = moduleConfiguration.OpenMeteo.LookupBaseUrl;
            options.TimeoutSeconds = moduleConfiguration.OpenMeteo.TimeoutSeconds;
            options.RetryCount = moduleConfiguration.OpenMeteo.RetryCount;
            options.RetryDelayMs = moduleConfiguration.OpenMeteo.RetryDelayMs;
            options.InterCallDelayMs = moduleConfiguration.OpenMeteo.InterCallDelayMs;
        });

        // Weather agent (Application-level abstraction)
        services.AddScoped<IWeatherAgent, OpenMeteoWeatherAgent>();

        // Geocoding client adapter (Application-level abstraction)
        services.AddScoped<IWeatherGeocodingClient, WeatherGeocodingClientAdapter>();

        // Job scheduling
        services.AddJobScheduling(o => o
                .Enabled()
                .StartupDelay("00:00:30"), configuration)
            .WithSqlServerStore(moduleConfiguration.ConnectionStrings.GetValueOrDefault("Default"))
            .WithBehavior<MetricsJobSchedulingBehavior>()
            .WithJob<WeatherIngestionJob>()
                .Cron(moduleConfiguration.IngestionCron)
                .Named("core_ingestion")
                .RegisterScoped()
            .AddEndpoints()
            .AddConsoleCommands();

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
