// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Presentation.Web.Server.Modules.Core;

using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;
using BridgingIT.DevKit.Examples.WeatherFiesta.Infrastructure;
using BridgingIT.DevKit.Examples.WeatherFiesta.Infrastructure.OpenMeteo;

/// <summary>
/// Extension methods for registering Core module services, including OpenMeteo client and ActiveEntity configurations.
/// </summary>
public static class CoreModuleExtensions
{
    public static IServiceCollection AddOpenMeteo(this IServiceCollection services, CoreModuleConfiguration moduleConfiguration)
    {
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
        services.AddScoped<IWeatherGeocodingClient, OpenMeteoGeocodingClient>();

        return services;
    }

    /// <summary>
    /// Adds ActiveEntity registrations for all persisted WeatherFiesta Core aggregate roots.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddActiveEntities(this IServiceCollection services)
    {
        return services.AddActiveEntity(cfg =>
        {
            cfg.For<City, CityId>()
                .UseEntityFrameworkProvider(o => o.Context<CoreDbContext>())
                .AddLoggingBehavior()
                .AddMetricsBehavior()
                .AddAuditStateBehavior(o => o.SoftDeleteEnabled = true)
                .AddDomainEventPublishingBehavior(new ActiveEntityDomainEventPublishingBehaviorOptions { PublishBefore = false });

            cfg.For<UserCity, UserCityId>()
                .UseEntityFrameworkProvider(o => o.Context<CoreDbContext>())
                .AddLoggingBehavior()
                .AddMetricsBehavior()
                .AddAuditStateBehavior(o => o.SoftDeleteEnabled = true);

            cfg.For<CurrentWeather, CurrentWeatherId>()
                .UseEntityFrameworkProvider(o => o.Context<CoreDbContext>())
                .AddLoggingBehavior()
                .AddMetricsBehavior();

            cfg.For<WeatherForecast, WeatherForecastId>()
                .UseEntityFrameworkProvider(o => o
                    .Context<CoreDbContext>()
                    .Options<CoreDbContext>(options => options.GenericMergeStrategy()))
                .AddLoggingBehavior()
                .AddMetricsBehavior();

            cfg.For<UserProfile, UserProfileId>()
                .UseEntityFrameworkProvider(o => o.Context<CoreDbContext>())
                .AddLoggingBehavior()
                .AddMetricsBehavior()
                .AddAuditStateBehavior(o => o.SoftDeleteEnabled = true);

            cfg.For<UserSubscription, UserSubscriptionId>()
                .UseEntityFrameworkProvider(o => o.Context<CoreDbContext>())
                .AddLoggingBehavior()
                .AddMetricsBehavior()
                .AddAuditStateBehavior(o => o.SoftDeleteEnabled = true)
                .AddDomainEventPublishingBehavior(new ActiveEntityDomainEventPublishingBehaviorOptions { PublishBefore = false });
        });
    }
}
