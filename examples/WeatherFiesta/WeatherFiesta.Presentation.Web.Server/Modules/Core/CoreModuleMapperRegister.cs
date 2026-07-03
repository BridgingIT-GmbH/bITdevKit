// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Presentation.Web.Server.Modules.Core;

using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;
using Mapster;

/// <summary>
/// Mapster registration for Core module entity-to-model mappings.
/// </summary>
public class CoreModuleMapperRegister : IRegister
{
    /// <summary>
    /// Registers all entity-to-model mappings for the Core module.
    /// </summary>
    /// <param name="config">The TypeAdapterConfig to register mappings on.</param>
    public void Register(TypeAdapterConfig config)
    {
        // City → CityModel
        config.ForType<City, CityModel>()
            .Map(dest => dest.Id, src => src.Id.Value.ToString())
            .Map(dest => dest.Latitude, src => src.Location != null ? src.Location.Latitude : 0)
            .Map(dest => dest.Longitude, src => src.Location != null ? src.Location.Longitude : 0)
            .Map(dest => dest.ConcurrencyVersion, src => src.ConcurrencyVersion.ToString());

        // City → AdminCityModel
        config.ForType<City, AdminCityModel>()
            .Map(dest => dest.Id, src => src.Id.Value.ToString())
            .Map(dest => dest.Latitude, src => src.Location != null ? src.Location.Latitude : 0)
            .Map(dest => dest.Longitude, src => src.Location != null ? src.Location.Longitude : 0)
            .Map(dest => dest.ConcurrencyVersion, src => src.ConcurrencyVersion.ToString())
            .Map(dest => dest.SubscriptionCount, src => 0); // Populated separately in query handler

        // UserCity → UserCityModel
        config.ForType<UserCity, UserCityModel>()
            .Map(dest => dest.Id, src => src.Id.Value.ToString())
            .Map(dest => dest.UserId, src => src.UserId)
            .Map(dest => dest.CityId, src => src.CityId.Value.ToString())
            .Map(dest => dest.ConcurrencyVersion, src => src.ConcurrencyVersion.ToString());

        // CurrentWeather → CurrentWeatherModel
        config.ForType<CurrentWeather, CurrentWeatherModel>()
            .Map(dest => dest.Id, src => src.Id.Value.ToString())
            .Map(dest => dest.CityId, src => src.CityId.Value.ToString())
            .Map(dest => dest.WeatherDescription, src => GetWeatherDescription(src.WeatherCode))
            .Map(dest => dest.WeatherIcon, src => GetWeatherIcon(src.WeatherCode))
            .Map(dest => dest.LastUpdatedText, src => src.RetrievedAt.ToRelativeTimeText(DateTime.UtcNow, new RelativeTimeFormatOptions { MinimumUnit = RelativeTimeUnit.Minute }));

        // WeatherForecast → WeatherForecastModel
        config.ForType<WeatherForecast, WeatherForecastModel>()
            .Map(dest => dest.Id, src => src.Id.Value.ToString())
            .Map(dest => dest.CityId, src => src.CityId.Value.ToString())
            .Map(dest => dest.DayWeatherDescription, src => GetWeatherDescription(src.DayWeatherCode))
            .Map(dest => dest.DayWeatherIcon, src => GetWeatherIcon(src.DayWeatherCode))
            .Map(dest => dest.DaylightPeriod, src => src.DaylightPeriod.ToIsoRangeString())
            .Map(dest => dest.DaylightDurationText, src => src.DaylightPeriod.Duration.ToDurationText(new RelativeTimeFormatOptions { MinimumUnit = RelativeTimeUnit.Minute }))
            .Map(dest => dest.LastUpdatedText, src => src.RetrievedAt.ToRelativeTimeText(DateTime.UtcNow, new RelativeTimeFormatOptions { MinimumUnit = RelativeTimeUnit.Minute }));

        // HourlyForecast → HourlyForecastModel
        config.ForType<HourlyForecast, HourlyForecastModel>()
            .Map(dest => dest.WeatherDescription, src => GetWeatherDescription(src.WeatherCode));

        // UserProfile → UserProfileModel
        config.ForType<UserProfile, UserProfileModel>()
            .Map(dest => dest.Id, src => src.Id.Value.ToString())
            .Map(dest => dest.UserId, src => src.UserId)
            .Map(dest => dest.TemperatureUnit, src => src.TemperatureUnit.Value)
            .Map(dest => dest.WindSpeedUnit, src => src.WindSpeedUnit.Value)
            .Map(dest => dest.CreatedAt, src => src.AuditState.CreatedDate.DateTime)
            .Map(dest => dest.ConcurrencyVersion, src => src.ConcurrencyVersion.ToString());

        // WeatherAlert → WeatherAlertModel
        config.ForType<WeatherAlert, WeatherAlertModel>()
            .Map(dest => dest.Type, src => src.Type.Value)
            .Map(dest => dest.Severity, src => src.Severity.Value);

        // WeatherRecommendation → WeatherRecommendationModel
        config.ForType<WeatherRecommendation, WeatherRecommendationModel>()
            .Map(dest => dest.Category, src => src.Category.Value)
            .Map(dest => dest.Severity, src => src.Severity.Value);

        // UserSubscription → UserSubscriptionModel
        config.ForType<UserSubscription, UserSubscriptionModel>()
            .Map(dest => dest.Id, src => src.Id.Value.ToString())
            .Map(dest => dest.Plan, src => src.Plan.Value)
            .Map(dest => dest.PlanDescription, src => src.Plan.Description)
            .Map(dest => dest.MaxCities, src => src.Plan.Details.MaxCities)
            .Map(dest => dest.MaxForecastDays, src => src.Plan.Details.MaxForecastDays)
            .Map(dest => dest.AllowsComparison, src => src.Plan.Details.AllowsComparison)
            .Map(dest => dest.AllowsExport, src => src.Plan.Details.AllowsExport)
            .Map(dest => dest.Status, src => src.Status.Value)
            .Map(dest => dest.BillingCycle, src => src.BillingCycle.Value)
            .Map(dest => dest.ActivePeriod, src => src.ActivePeriod.ToIsoRangeString())
            .Map(dest => dest.IsActive, src => src.IsActive)
            .Map(dest => dest.ConcurrencyVersion, src => src.ConcurrencyVersion.ToString());

        // Enumeration converters
        RegisterConverter<TemperatureUnit>(config);
        RegisterConverter<WindSpeedUnit>(config);
        RegisterConverter<SubscriptionPlan>(config);
        RegisterConverter<SubscriptionStatus>(config);
        RegisterConverter<SubscriptionBillingCycle>(config);
    }

    private static string GetWeatherDescription(int weatherCode)
    {
        var condition = WeatherConditionCode.GetAll()
            .FirstOrDefault(c => c.Id == weatherCode);
        return condition?.Description ?? $"Unknown weather code {weatherCode}";
    }

    private static string GetWeatherIcon(int weatherCode)
    {
        var condition = WeatherConditionCode.GetAll()
            .FirstOrDefault(c => c.Id == weatherCode);
        return condition?.Icon ?? "❓";
    }

    private static void RegisterConverter<T>(TypeAdapterConfig config)
        where T : Enumeration
    {
        config.ForType<T, int>()
            .Map(dest => dest, src => src.Id);
    }
}
