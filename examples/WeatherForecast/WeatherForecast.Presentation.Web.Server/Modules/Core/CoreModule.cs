// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Presentation.Web.Server.Modules.Core;

using Application.Modules.Core;
using Common;
using DevKit.Application;
using DevKit.Application.JobScheduling;
using DevKit.Domain.Repositories;
using DevKit.Infrastructure.EntityFramework.Repositories;
using DevKit.Infrastructure.Mapping;
using Domain.Model;
using FluentValidation;
using Infrastructure;
using Infrastructure.EntityFramework;
using Microsoft.Extensions.Diagnostics.HealthChecks;

public class CoreModule : WebModuleBase
{
    //public CoreModule()
    //{
    //    this.Enabled = false;
    //}

    //tag::Part1[]
    public override IServiceCollection Register(
        IServiceCollection services,
        IConfiguration configuration = null,
        IWebHostEnvironment environment = null)
    {
        //var moduleConfiguration = services.Configure<CoreModuleConfiguration>(configuration, this); // = configuration.Get<CoreModuleConfiguration>(this);
        var moduleConfiguration =
            this.Configure<CoreModuleConfiguration, CoreModuleConfiguration.Validator>(services, configuration);
        var moduleConfiguration2 = this.Configure<CoreModuleConfiguration>(services,
            configuration,
            options =>
            {
                var validator = new InlineValidator<CoreModuleConfiguration>();
                validator
                    .RuleFor(applicationOptions => applicationOptions.OpenWeatherUrl)
                    .NotNull()
                    .NotEmpty()
                    .WithMessage("OpenWeatherUrl cannot be null or empty");
                validator
                    .RuleFor(applicationOptions => applicationOptions.OpenWeatherApiKey)
                    .NotNull()
                    .NotEmpty()
                    .WithMessage("OpenWeatherApiKey cannot be null or empty");

                return validator
                    .Validate(options, strategy => strategy.ThrowOnFailures())
                    .IsValid;
            });
        var moduleConfiguration3 =
            services.Configure<CoreModuleConfiguration, CoreModuleConfiguration.Validator>(configuration, this);

        services.AddCaching(configuration)
            //.UseEntityFrameworkDocumentStoreProvider<CoreDbContext>()
            //.WithAzureBlobDocumentStoreProvider()
            //.WithAzureTableDocumentStoreProvider()
            //.WithCosmosDocumentStoreProvider()
            .WithInMemoryProvider();

        // jobs
        services.AddJobScheduling()
            .WithScopedJob<ForecastImportJob>(CronExpressions.Every30Minutes)
            .WithScopedJob<
                EchoJob>(CronExpressions.Every5Minutes); // .WithSingletonJob<EchoJob>(CronExpressions.Every5Minutes)

        // messaging
        services.AddMessaging(configuration)
            .WithSubscription<EchoMessage, EchoMessageHandler>();

        // dbcontext
        services.AddSqlServerDbContext<CoreDbContext>(o => o
                .UseConnectionString(moduleConfiguration.ConnectionStrings["Default"])
                .UseLogger()
                .UseSimpleLogger())
            .WithHealthChecks()
            .WithDatabaseMigratorService();

        // City repository
        services.AddInMemoryRepository(new InMemoryContext<City>(new[]
            {
                // does not trigger repo insert > domain event dispatching
                City.Create("Berlin", "DE", 10.45, 54.033329), City.Create("Amsterdam", "NL", 4.88969, 52.374031),
                City.Create("Paris", "FR", 2.3486, 48.853401), City.Create("Madrid", "ES", -3.70256, 40.4165),
                City.Create("Rome", "IT", 12.4839, 41.894741)
            }.ForEach(c => c.DomainEvents.Clear())))
            .WithBehavior<RepositoryTracingBehavior<City>>()
            .WithBehavior<RepositoryLoggingBehavior<City>>()
            .WithBehavior<RepositoryNoTrackingBehavior<City>>()
            .WithBehavior<RepositoryDomainEventBehavior<City>>()
            .WithBehavior<RepositoryDomainEventPublisherBehavior<City>>();

        //tag::Part2[]
        // Forecast repository
        services.AddEntityFrameworkRepository<Forecast, CoreDbContext>()
            .WithTransactions()
            .WithBehavior<RepositoryTracingBehavior<Forecast>>()
            .WithBehavior<RepositoryLoggingBehavior<Forecast>>()
            .WithBehavior<RepositoryNoTrackingBehavior<Forecast>>()
            .WithBehavior(inner => new RepositoryIncludeBehavior<Forecast>(e => e.Type, inner))
            .WithBehavior<RepositoryDomainEventBehavior<Forecast>>()
            .WithBehavior<RepositoryDomainEventPublisherBehavior<Forecast>>();
        //end::Part2[]

        // Forecast Type repository
        services.AddEntityFrameworkRepository<ForecastType, CoreDbContext>()
            .WithBehavior<RepositoryTracingBehavior<ForecastType>>()
            .WithBehavior<RepositoryLoggingBehavior<ForecastType>>()
            .WithBehavior<RepositoryNoTrackingBehavior<ForecastType>>();

        services.AddScoped<IGenericRepository<ForecastType>>(sp =>
                new EntityFrameworkGenericRepository<ForecastType>(o => o
                    .LoggerFactory(sp.GetRequiredService<ILoggerFactory>())
                    .DbContext(sp.GetRequiredService<CoreDbContext>())))
            .Decorate<IGenericRepository<ForecastType>, RepositoryTracingBehavior<ForecastType>>() // first
            .Decorate<IGenericRepository<ForecastType>, RepositoryLoggingBehavior<ForecastType>>()
            .Decorate<IGenericRepository<ForecastType>, RepositoryNoTrackingBehavior<ForecastType>>();

        // UserAccount repository
        services.AddEntityFrameworkRepository<UserAccount, DbUserAccount, CoreDbContext>(
                new AutoMapperEntityMapper(MapperFactory.Create()))
            .WithBehavior<RepositoryTracingBehavior<UserAccount>>()
            .WithBehavior<RepositoryLoggingBehavior<UserAccount>>()
            .WithBehavior<RepositoryNoTrackingBehavior<UserAccount>>()
            .WithBehavior<RepositoryDomainEventBehavior<UserAccount>>()
            .WithBehavior<RepositoryDomainEventPublisherBehavior<UserAccount>>();

        // UserAccount specific repository, demonstrates how Capper (raw sql) can be used inside normal EF repos
        services.AddScoped(sp =>
            new UserAccountRepository(o => o
                .LoggerFactory(sp.GetRequiredService<ILoggerFactory>())
                .DbContext(sp.GetRequiredService<CoreDbContext>())
                .Mapper(new AutoMapperEntityMapper(MapperFactory.Create()))));
        //.Decorate<IGenericRepository<UserAccount>, RepositoryTracingDecorator<UserAccount>>() // first
        //.Decorate<IGenericRepository<UserAccount>, RepositoryLoggingDecorator<UserAccount>>()
        //.Decorate<IGenericRepository<UserAccount>, RepositoryNoTrackingDecorator<UserAccount>>()
        //.Decorate<IGenericRepository<UserAccount>, RepositoryDomainEventDecorator<UserAccount>>()
        //.Decorate<IGenericRepository<UserAccount>, RepositoryDomainEventPublisherDecorator<UserAccount>>();

        // TestGuidEntity repository
        services.AddEntityFrameworkRepository<TestGuidEntity, CoreDbContext>()
            .WithBehavior<RepositoryTracingBehavior<TestGuidEntity>>()
            .WithBehavior<RepositoryLoggingBehavior<TestGuidEntity>>()
            .WithBehavior<RepositoryNoTrackingBehavior<TestGuidEntity>>()
            .WithBehavior<RepositoryDomainEventBehavior<TestGuidEntity>>()
            .WithBehavior<RepositoryDomainEventPublisherBehavior<TestGuidEntity>>();

        // TestIntEntity repository
        services.AddEntityFrameworkRepository<TestIntEntity, CoreDbContext>()
            .WithBehavior<RepositoryTracingBehavior<TestIntEntity>>()
            .WithBehavior<RepositoryLoggingBehavior<TestIntEntity>>()
            .WithBehavior<RepositoryNoTrackingBehavior<TestIntEntity>>()
            .WithBehavior<RepositoryDomainEventBehavior<TestIntEntity>>()
            .WithBehavior<RepositoryDomainEventPublisherBehavior<TestIntEntity>>();

        // Weather data adapter (= anti corruption)
        services.AddScoped<IWeatherDataAdapter>(sp =>
                new OpenWeatherDataAdapter(sp.GetRequiredService<ILoggerFactory>(),
                    sp.GetRequiredService<IHttpClientFactory>(),
                    moduleConfiguration.OpenWeatherApiKey))
            .AddHttpClient("OpenWeatherClient",
                c =>
                {
                    c.BaseAddress = new Uri(moduleConfiguration.OpenWeatherUrl);
                    c.DefaultRequestHeaders.Add("Accept", "application/json");
                });

        // Application/Presentation Mapping
        services.AddSingleton<IMapper<CityQueryResponse, CityModel>, CityModelMapper>();
        services.AddSingleton<IMapper<ForecastQueryResponse, ForecastModel>, ForecastModelMapper>();

        // Infrastructure/Domain Mapping
        //services.AddSingleton<IMapper<Infrastructure.Daily, Domain.Model.Forecast>>(sp => // inline mapper
        //    new Mapper<Infrastructure.Daily, Domain.Model.Forecast>((s, t) =>
        //    {
        //        t.TemperatureMin = s.Temp.Min;
        //        t.TemperatureMax = s.Temp.Max;

        //        t.Country = s.City?.Country;
        //        t.Longitude = s.City?.Location?.Longitude;
        //    }));

        //tag::Partend[]
        services.AddHealthChecks()
            .AddCheck("self-core", () => HealthCheckResult.Healthy(), ["ready"]);

        return services;
    } //end::Part1[]

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
        //TODO: currently nswag does not generate swagger for minimal endpoints when versioning is also used https://github.com/RicoSuter/NSwag/pull/3814
        //      solution https://github.com/RicoSuter/NSwag/issues/3945 >> + https://github.com/JKamsker/Versioned-Minimal-and-Controller-AspNetCore
        //app.MapGet("/_system/correlationid", (HttpContext ctx) => ctx.TryGetCorrelationId());

        // WARN: causes RouteHandlerAnalyzer' threw an exception of type 'System.NullReferenceException' with message 'Object reference not set to an instance of an object.
        //       https://github.com/dotnet/aspnetcore/issues/41794
        //app.MapGet("/_system/hello", () => "Hello World!")
        //    .WithTags("General");

        //app.MapGet("/_system/sum/{a}/{b}", (Func<int, int, int>)((a, b) => a + b))
        //    .WithName("CalculateSum")
        //    .WithTags("Calculator");

        return app;
    }

    //end::Partend[]
}