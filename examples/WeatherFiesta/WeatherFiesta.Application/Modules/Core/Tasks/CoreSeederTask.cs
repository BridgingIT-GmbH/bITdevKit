// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core.Tasks;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core;
using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Startup task that seeds comprehensive representative data: cities, user city subscriptions,
/// current weather, weather forecasts, user profiles, and user subscriptions.
/// Uses <see cref="FakeUsers.Starwars"/> to get deterministic user IDs matching the FakeIdentityProvider.
/// </summary>
public class CoreSeederTask(
    ILoggerFactory loggerFactory,
    IDatabaseReadyService databaseReadyService) : IStartupTask
{
    private readonly ILogger<CoreSeederTask> logger = loggerFactory?.CreateLogger<CoreSeederTask>() ?? NullLoggerFactory.Instance.CreateLogger<CoreSeederTask>();
    private readonly IDatabaseReadyService databaseReadyService = databaseReadyService;

    /// <inheritdoc />
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("{LogKey} seed cities, weather, profiles, and subscriptions (task={StartupTaskType})", "IFR", this.GetType().PrettyName());

        await this.databaseReadyService.WaitForReadyAsync(
            timeout: TimeSpan.FromMinutes(2),
            cancellationToken: cancellationToken);

        // 1. Seed cities
        var cityIds = await this.SeedCitiesAsync(cancellationToken);

        // 2. Seed user city subscriptions, profiles, and subscriptions
        var users = FakeUsers.Starwars;
        var subscriptionPlans = new[]
        {
            SubscriptionPlan.Enterprise,  // luke.skywalker - Administrators
            SubscriptionPlan.Pro,         // yoda - Administrators
            SubscriptionPlan.Basic,       // obi.wan - Users, Readers, Writers
            SubscriptionPlan.Free,        // han.solo - Administrators, Contributors
            SubscriptionPlan.Pro,         // darth.vader - Administrators, Users
            SubscriptionPlan.Free,        // anakin.skywalker - Users
        };

        for (var i = 0; i < users.Length; i++)
        {
            var user = users[i];
            var plan = i < subscriptionPlans.Length ? subscriptionPlans[i] : SubscriptionPlan.Free;

            await this.SeedUserCitiesAsync(user, cityIds, cancellationToken);
            await this.SeedProfileAsync(user, cancellationToken);
            await this.SeedSubscriptionAsync(user, plan, cancellationToken);
        }

        // 3. Seed current weather and forecasts for each city
        foreach (var (cityName, cityId) in cityIds)
        {
            await this.SeedCurrentWeatherAsync(cityName, cityId, cancellationToken);
            await this.SeedWeatherForecastsAsync(cityName, cityId, cancellationToken);
            await this.SeedWeatherReportsAsync(cityName, cityId, cancellationToken);
        }
    }

    private async Task<Dictionary<string, CityId>> SeedCitiesAsync(CancellationToken cancellationToken)
    {
        var cityIds = new Dictionary<string, CityId>();
        var cities = new[]
        {
            ("Amsterdam", "Netherlands", "NL", "Europe/Amsterdam", 52.3676m, 4.9041m, 2m, 2759794L),
            ("Berlin",    "Germany",     "DE", "Europe/Berlin",    52.5200m, 13.4050m, 34m, 2950159L),
            ("Paris",     "France",      "FR", "Europe/Paris",     48.8566m, 2.3522m, 35m, 2988507L),
            ("London",    "United Kingdom", "GB", "Europe/London", 51.5074m, -0.1278m, 11m, 2643743L),
        };

        foreach (var (name, country, code, tz, lat, lon, elevation, externalId) in cities)
        {
            var existingResult = await City
                .ExistsAsync(new CityByExternalIdSpecification(externalId), cancellationToken: cancellationToken);

            if (existingResult.IsFailure)
            {
                this.logger.LogWarning("{LogKey} failed to check existing city {CityName}: {Errors}", "IFR", name, string.Join(", ", existingResult.Errors.Select(e => e.Message)));
                continue;
            }

            if (existingResult.Value)
            {
                // City already exists — look up its ID for downstream seeding
                this.logger.LogInformation("{LogKey} city {CityName} already exists, skipping create", "IFR", name);
                // We need the ID; query via ActiveEntity FindOne would be complex, so we still track by convention
                // The downstream seeds will handle duplicates gracefully
                continue;
            }

            var locationResult = Location.Create(lat, lon);
            if (locationResult.IsFailure)
            {
                this.logger.LogWarning("{LogKey} failed to seed city {CityName}: {Errors}", "IFR", name, string.Join(", ", locationResult.Errors.Select(e => e.Message)));
                continue;
            }

            var city = City.Create(name, country, code, tz, locationResult.Value, externalId, elevation);
            city.AuditState.SetCreated("seed", nameof(CoreSeederTask));
            var insertResult = await city.InsertAsync(cancellationToken);

            if (insertResult.IsFailure)
            {
                this.logger.LogWarning("{LogKey} failed to seed city {CityName}: {Errors}", "IFR", name, string.Join(", ", insertResult.Errors.Select(e => e.Message)));
                continue;
            }

            cityIds[name] = city.Id;
            this.logger.LogInformation("{LogKey} seeded city {CityName} ({CountryCode}) lat={Lat}, lon={Lon}", "IFR", name, code, lat, lon);
        }

        return cityIds;
    }

    private async Task SeedUserCitiesAsync(FakeUser user, Dictionary<string, CityId> cityIds, CancellationToken cancellationToken)
    {
        // Define subscriptions per user: (cityName, isPrimary, displayOrder)
        var userSubscriptions = user.Email switch
        {
            "luke.skywalker@starwars.com" => [("Amsterdam", true, 0), ("Berlin", false, 1), ("Paris", false, 2)],
            "yoda@starwars.com" => [("London", true, 0), ("Paris", false, 1)],
            "obi.wan@starwars.com" => [("Amsterdam", true, 0), ("London", false, 1)],
            "han.solo@starwars.com" => [("Berlin", true, 0), ("Amsterdam", false, 1), ("London", false, 2), ("Paris", false, 3)],
            "darth.vader@starwars.com" => [("Paris", true, 0), ("Berlin", false, 1)],
            "anakin.skywalker@starwars.com" => [("London", true, 0)],
            _ => Array.Empty<(string, bool, int)>()
        };

        foreach (var (cityName, isPrimary, displayOrder) in userSubscriptions)
        {
            if (!cityIds.TryGetValue(cityName, out var cityId))
            {
                this.logger.LogWarning("{LogKey} city {CityName} not found in seeded cities, skipping user city for {UserId}", "IFR", cityName, user.Id);
                continue;
            }

            var existingResult = await UserCity
                .ExistsAsync(new UserCityByUserAndCitySpecification(user.Id, cityId), cancellationToken: cancellationToken);

            if (existingResult.IsFailure)
            {
                this.logger.LogWarning("{LogKey} failed to check existing user city for {UserId} / {CityName}: {Errors}", "IFR", user.Id, cityName, string.Join(", ", existingResult.Errors.Select(e => e.Message)));
                continue;
            }

            if (existingResult.Value)
            {
                continue;
            }

            var userCity = UserCity.Create(user.Id, cityId, isPrimary, displayOrder);
            userCity.AuditState.SetCreated("seed", nameof(CoreSeederTask));
            var insertResult = await userCity.InsertAsync(cancellationToken);

            if (insertResult.IsFailure)
            {
                this.logger.LogWarning("{LogKey} failed to seed user city for {UserId} / {CityName}: {Errors}", "IFR", user.Id, cityName, string.Join(", ", insertResult.Errors.Select(e => e.Message)));
                continue;
            }

            this.logger.LogInformation("{LogKey} seeded user city for {UserId} ({UserName}): {CityName} (primary={IsPrimary}, order={DisplayOrder})", "IFR", user.Id, user.Name, cityName, isPrimary, displayOrder);
        }
    }

    private async Task SeedCurrentWeatherAsync(string cityName, CityId cityId, CancellationToken cancellationToken)
    {
        var existingResult = await CurrentWeather
            .ExistsAsync(new CurrentWeatherByCitySpecification(cityId), cancellationToken: cancellationToken);

        if (existingResult.IsFailure)
        {
            this.logger.LogWarning("{LogKey} failed to check existing current weather for {CityName}: {Errors}", "IFR", cityName, string.Join(", ", existingResult.Errors.Select(e => e.Message)));
            return;
        }

        if (existingResult.Value)
        {
            return;
        }

        var weather = cityName switch
        {
            "Amsterdam" => CreateCurrentWeather(cityId, 14m, 12m, 78, 3, 22m, 45, 38m, 0.5m, 85, 1013m),
            "Berlin" => CreateCurrentWeather(cityId, 18m, 17m, 62, 2, 15m, 270, 28m, 0m, 40, 1018m),
            "Paris" => CreateCurrentWeather(cityId, 22m, 23m, 55, 1, 10m, 180, 18m, 0m, 15, 1020m),
            "London" => CreateCurrentWeather(cityId, 16m, 14m, 82, 51, 18m, 225, 32m, 1.2m, 90, 1008m),
            _ => null
        };

        if (weather is null)
        {
            return;
        }

        var insertResult = await weather.InsertAsync(cancellationToken);

        if (insertResult.IsFailure)
        {
            this.logger.LogWarning("{LogKey} failed to seed current weather for {CityName}: {Errors}", "IFR", cityName, string.Join(", ", insertResult.Errors.Select(e => e.Message)));
            return;
        }

        this.logger.LogInformation("{LogKey} seeded current weather for {CityName}: {Temp}°C, feels {Feels}°C, code {Code}", "IFR", cityName, weather.Temperature, weather.ApparentTemperature, weather.WeatherCode);
    }

    private async Task SeedWeatherForecastsAsync(string cityName, CityId cityId, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Define 3-day forecasts per city: (dayOffset, code, tempMax, tempMin, appMax, appMin, precipSum, precipProb, windMax, gustsMax, windDir, uv, sunSec, daySec, sunriseH, sunriseM, sunsetH, sunsetM)
        var forecastData = cityName switch
        {
            "Amsterdam" =>
            [
                (0, 3,  16m, 10m, 14m, 8m,  1.2m, 45, 25m, 42m, 45,  4m, 18000, 54000, 5, 20, 21, 45),
                (1, 2,  18m, 11m, 17m, 10m, 0.0m, 10, 18m, 30m, 200, 6m, 28800, 54600, 5, 19, 21, 46),
                (2, 61, 14m, 9m,  12m, 7m,  5.8m, 80, 30m, 48m, 220, 2m, 10800, 54000, 5, 21, 21, 44),
            ],
            "Berlin" =>
            [
                (0, 2,  20m, 13m, 19m, 12m, 0.0m, 15, 18m, 32m, 270, 6m, 30600, 55200, 4, 45, 21, 15),
                (1, 1,  23m, 14m, 24m, 13m, 0.0m, 5,  12m, 22m, 180, 7m, 34200, 55800, 4, 44, 21, 16),
                (2, 3,  19m, 12m, 18m, 11m, 0.4m, 35, 22m, 38m, 250, 4m, 21600, 55200, 4, 46, 21, 14),
            ],
            "Paris" =>
            [
                (0, 1,  24m, 15m, 25m, 14m, 0.0m, 5,  12m, 20m, 180, 7m, 34200, 55800, 5, 50, 21, 55),
                (1, 0,  26m, 16m, 27m, 15m, 0.0m, 0,  8m,  15m, 160, 8m, 37800, 56400, 5, 49, 21, 56),
                (2, 2,  22m, 14m, 22m, 13m, 0.2m, 25, 15m, 28m, 230, 5m, 27000, 55800, 5, 51, 21, 54),
            ],
            "London" =>
            [
                (0, 51, 17m, 11m, 15m, 9m,  2.4m, 70, 22m, 38m, 225, 3m, 16200, 54600, 4, 50, 21, 20),
                (1, 61, 15m, 10m, 13m, 8m,  6.2m, 85, 28m, 45m, 240, 2m, 10800, 54000, 4, 51, 21, 19),
                (2, 3,  18m, 12m, 17m, 11m, 0.8m, 40, 20m, 34m, 200, 5m, 23400, 54600, 4, 52, 21, 18),
            ],
            _ => Array.Empty<(int, int, decimal, decimal, decimal, decimal, decimal, int, decimal, decimal, int, decimal, int, int, int, int, int, int)>()
        };

        foreach (var (dayOffset, code, tempMax, tempMin, appMax, appMin, precipSum, precipProb, windMax, gustsMax, windDir, uv, sunSec, daySec, sunriseH, sunriseM, sunsetH, sunsetM) in forecastData)
        {
            var forecastDate = today.AddDays(dayOffset);

            // Check if this forecast already exists
            var existingResult = await WeatherForecast
                .ExistsAsync(new WeatherForecastByCityAndDateSpecification(cityId, forecastDate), cancellationToken: cancellationToken);

            if (existingResult.IsFailure)
            {
                this.logger.LogWarning("{LogKey} failed to check existing forecast for {CityName} on {Date}: {Errors}", "IFR", cityName, forecastDate, string.Join(", ", existingResult.Errors.Select(e => e.Message)));
                continue;
            }

            if (existingResult.Value)
            {
                continue;
            }

            var forecast = WeatherForecast.Create(cityId);
            forecast.ForecastDate = forecastDate;
            forecast.DayWeatherCode = code;
            forecast.TemperatureMax = tempMax;
            forecast.TemperatureMin = tempMin;
            forecast.ApparentTemperatureMax = appMax;
            forecast.ApparentTemperatureMin = appMin;
            forecast.PrecipitationSum = precipSum;
            forecast.PrecipitationProbabilityMax = precipProb;
            forecast.WindSpeedMax = windMax;
            forecast.WindGustsMax = gustsMax;
            forecast.DominantWindDirection = windDir;
            forecast.UvIndexMax = uv;
            forecast.SunshineDurationSeconds = sunSec;
            forecast.DaylightDurationSeconds = daySec;
            forecast.Sunrise = new DateTime(forecastDate.Year, forecastDate.Month, forecastDate.Day, sunriseH, sunriseM, 0, DateTimeKind.Utc);
            forecast.Sunset = new DateTime(forecastDate.Year, forecastDate.Month, forecastDate.Day, sunsetH, sunsetM, 0, DateTimeKind.Utc);
            forecast.RetrievedAt = DateTime.UtcNow;

            // Generate 24 hourly forecasts
            forecast.HourlyForecasts = GenerateHourlyForecasts(tempMin, tempMax, code, precipProb, windMax, windDir, gustsMax);

            var insertResult = await forecast.InsertAsync(cancellationToken);

            if (insertResult.IsFailure)
            {
                this.logger.LogWarning("{LogKey} failed to seed forecast for {CityName} on {Date}: {Errors}", "IFR", cityName, forecastDate, string.Join(", ", insertResult.Errors.Select(e => e.Message)));
                continue;
            }

            this.logger.LogInformation("{LogKey} seeded forecast for {CityName} on {Date}: {TempMin}–{TempMax}°C, code {Code}", "IFR", cityName, forecastDate, tempMin, tempMax, code);
        }
    }

    private async Task SeedWeatherReportsAsync(string cityName, CityId cityId, CancellationToken cancellationToken)
    {
        var timeZoneId = GetCityTimeZoneId(cityName);
        if (timeZoneId is null)
        {
            return;
        }

        var utcNow = DateTime.UtcNow;
        var localToday = GetLocalDate(utcNow, timeZoneId);
        var reports = new[]
        {
            (WeatherReportType.Current, CreateReportPeriod(localToday, localToday.AddDays(1), timeZoneId)),
            (WeatherReportType.Today, CreateReportPeriod(localToday, localToday.AddDays(1), timeZoneId)),
            (WeatherReportType.Tomorrow, CreateReportPeriod(localToday.AddDays(1), localToday.AddDays(2), timeZoneId)),
            (WeatherReportType.Week, CreateReportPeriod(localToday, localToday.AddDays(7), timeZoneId))
        };

        foreach (var (reportType, period) in reports)
        {
            var existingResult = await WeatherReport.FindOneAsync(
                new Specification<WeatherReport>(r =>
                    r.CityId == cityId &&
                    r.ReportType == reportType &&
                    r.PeriodStartUtc == period.PeriodStartUtc &&
                    r.PeriodEndUtc == period.PeriodEndUtc),
                null,
                cancellationToken);

            if (existingResult.IsSuccess)
            {
                continue;
            }

            if (existingResult.IsFailure && !existingResult.Errors.Any(e => e is NotFoundError))
            {
                this.logger.LogWarning("{LogKey} failed to check existing weather report for {CityName} ({ReportType}): {Errors}", "IFR", cityName, reportType, string.Join(", ", existingResult.Errors.Select(e => e.Message)));
                continue;
            }

            var report = WeatherReport.Create(
                cityId,
                reportType,
                period.PeriodStartUtc,
                period.PeriodEndUtc,
                period.ForecastDateStart,
                period.ForecastDateEndExclusive);
            report.SetContent("Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.");

            var insertResult = await report.InsertAsync(cancellationToken);
            if (insertResult.IsFailure)
            {
                this.logger.LogWarning("{LogKey} failed to seed weather report for {CityName} ({ReportType}): {Errors}", "IFR", cityName, reportType, string.Join(", ", insertResult.Errors.Select(e => e.Message)));
                continue;
            }

            this.logger.LogInformation("{LogKey} seeded weather report for {CityName} ({ReportType})", "IFR", cityName, reportType);
        }
    }

    private async Task SeedProfileAsync(FakeUser user, CancellationToken cancellationToken)
    {
        var existingResult = await UserProfile
            .ExistsAsync(new UserProfileByUserSpecification(user.Id), cancellationToken: cancellationToken);

        if (existingResult.IsFailure)
        {
            this.logger.LogWarning("{LogKey} failed to check existing profile for user {UserId}: {Errors}", "IFR", user.Id, string.Join(", ", existingResult.Errors.Select(e => e.Message)));
            return;
        }

        if (existingResult.Value)
        {
            return;
        }

        var profile = UserProfile.Create(user.Id, user.Email, user.Name);
        profile.AuditState.SetCreated("seed", nameof(CoreSeederTask));
        var insertResult = await profile.InsertAsync(cancellationToken);

        if (insertResult.IsFailure)
        {
            this.logger.LogWarning("{LogKey} failed to seed profile for user {UserId}: {Errors}", "IFR", user.Id, string.Join(", ", insertResult.Errors.Select(e => e.Message)));
            return;
        }

        this.logger.LogInformation("{LogKey} seeded profile for user {UserId} ({UserName})", "IFR", user.Id, user.Name);
    }

    private async Task SeedSubscriptionAsync(FakeUser user, SubscriptionPlan plan, CancellationToken cancellationToken)
    {
        var existingResult = await UserSubscription
            .ExistsAsync(new SubscriptionByUserSpecification(user.Id), cancellationToken: cancellationToken);

        if (existingResult.IsFailure)
        {
            this.logger.LogWarning("{LogKey} failed to check existing subscription for user {UserId}: {Errors}", "IFR", user.Id, string.Join(", ", existingResult.Errors.Select(e => e.Message)));
            return;
        }

        if (existingResult.Value)
        {
            return;
        }

        var subscription = plan == SubscriptionPlan.Free
            ? UserSubscription.CreateFree(user.Id)
            : CreateSubscription(user.Id, plan);

        subscription.AuditState.SetCreated("seed", nameof(CoreSeederTask));
        var insertResult = await subscription.InsertAsync(cancellationToken);

        if (insertResult.IsFailure)
        {
            this.logger.LogWarning("{LogKey} failed to seed subscription for user {UserId}: {Errors}", "IFR", user.Id, string.Join(", ", insertResult.Errors.Select(e => e.Message)));
            return;
        }

        this.logger.LogInformation("{LogKey} seeded subscription for user {UserId} ({UserName}) with plan {Plan}", "IFR", user.Id, user.Name, subscription.Plan.Value);
    }

    private static CurrentWeather CreateCurrentWeather(
        CityId cityId, decimal temp, decimal feels, int humidity, int code,
        decimal windSpeed, int windDir, decimal gusts, decimal precip, int cloud, decimal pressure)
    {
        var weather = CurrentWeather.Create(cityId);
        weather.Temperature = temp;
        weather.ApparentTemperature = feels;
        weather.Humidity = humidity;
        weather.WeatherCode = code;
        weather.WindSpeed = windSpeed;
        weather.WindDirection = windDir;
        weather.WindGusts = gusts;
        weather.Precipitation = precip;
        weather.CloudCover = cloud;
        weather.Pressure = pressure;
        weather.RetrievedAt = DateTime.UtcNow;
        return weather;
    }

    private static ICollection<HourlyForecast> GenerateHourlyForecasts(
        decimal tempMin, decimal tempMax, int dayCode, int precipProb,
        decimal windMax, int windDir, decimal gusts)
    {
        var forecasts = new List<HourlyForecast>(24);
        var tempRange = tempMax - tempMin;

        for (var hour = 0; hour < 24; hour++)
        {
            // Sinusoidal temperature curve: coldest at 5am, warmest at 3pm
            var phase = (hour - 5) / 24.0 * 2 * Math.PI;
            var tempFactor = (decimal)((Math.Sin(phase - Math.PI / 2) + 1) / 2);
            var temp = Math.Round(tempMin + tempRange * tempFactor, 1);
            var apparentTemp = temp - 1; // feels slightly cooler
            var humidity = (int)(85 - 30 * tempFactor); // higher when cooler
            var isDay = hour >= 6 && hour <= 20;
            var cloud = isDay ? (int)(60 - 30 * tempFactor) : (int)(70 - 20 * tempFactor);
            var wind = Math.Round(windMax * (0.6m + 0.4m * (decimal)Math.Abs(Math.Sin(phase))), 1);
            var gust = Math.Round(gusts * (0.5m + 0.5m * (decimal)Math.Abs(Math.Sin(phase + 0.5))), 1);
            var precipChanceRaw = isDay && dayCode >= 51 ? precipProb * (1m - tempFactor * 0.5m) : precipProb * 0.3m;
            var precipChance = (int)Math.Min(precipProb, precipChanceRaw);
            var precip = dayCode >= 51 && precipChance > 40 ? Math.Round(0.1m + 0.3m * tempFactor, 1) : 0m;

            forecasts.Add(new HourlyForecast
            {
                Hour = hour,
                Temperature = temp,
                RelativeHumidity = Math.Clamp(humidity, 30, 100),
                ApparentTemperature = apparentTemp,
                PrecipitationProbability = Math.Clamp(precipChance, 0, 100),
                Precipitation = precip,
                WeatherCode = isDay ? dayCode : (dayCode >= 51 ? dayCode : 0),
                WindSpeed = wind,
                WindDirection = windDir,
                WindGusts = gust,
                CloudCover = Math.Clamp(cloud, 0, 100),
                Visibility = isDay ? 20m : 10m,
                IsDay = isDay
            });
        }

        return forecasts;
    }

    private static WeatherReportPeriod CreateReportPeriod(DateOnly localStart, DateOnly localEndExclusive, string timeZoneId)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var localStartDateTime = localStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var localEndDateTime = localEndExclusive.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        return new WeatherReportPeriod(
            TimeZoneInfo.ConvertTimeToUtc(localStartDateTime, timeZone),
            TimeZoneInfo.ConvertTimeToUtc(localEndDateTime, timeZone),
            localStart,
            localEndExclusive,
            timeZoneId);
    }

    private static DateOnly GetLocalDate(DateTime utcNow, string timeZoneId)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var utc = utcNow.Kind == DateTimeKind.Utc ? utcNow : utcNow.ToUniversalTime();
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone);

        return DateOnly.FromDateTime(local);
    }

    private static string GetCityTimeZoneId(string cityName)
    {
        return cityName switch
        {
            "Amsterdam" => "Europe/Amsterdam",
            "Berlin" => "Europe/Berlin",
            "Paris" => "Europe/Paris",
            "London" => "Europe/London",
            _ => null
        };
    }

    private static UserSubscription CreateSubscription(string userId, SubscriptionPlan plan)
    {
        var subscription = UserSubscription.CreateFree(userId);
        var billingCycle = plan == SubscriptionPlan.Enterprise
            ? SubscriptionBillingCycle.Yearly
            : SubscriptionBillingCycle.Monthly;
        subscription.ChangePlan(plan, billingCycle);
        return subscription;
    }
}
