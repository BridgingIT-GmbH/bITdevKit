// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.IntegrationTests;

using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;

/// <summary>
/// Static test data and seeding logic for WeatherFiesta integration tests.
/// All entities have deterministic IDs for reliable assertions.
/// </summary>
public static class TestData
{
    // ──────────────────────────────────────────────────────────────────────────────
    // User IDs
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>Standard test user ID (matches TestAuthenticationHandler).</summary>
    public const string TestUserId = "00000000-0000-0000-0000-000000000001";

    /// <summary>Admin test user ID for admin-scoped tests.</summary>
    public const string AdminUserId = "00000000-0000-0000-0000-000000000002";

    // ──────────────────────────────────────────────────────────────────────────────
    // City IDs
    // ──────────────────────────────────────────────────────────────────────────────

    public static readonly Guid LondonCityGuid = Guid.Parse("00000000-0000-0000-0000-000000000010");
    public static readonly Guid ParisCityGuid = Guid.Parse("00000000-0000-0000-0000-000000000020");
    public static readonly Guid BerlinCityGuid = Guid.Parse("00000000-0000-0000-0000-000000000030");

    /// <summary>London city entity (seeded).</summary>
    public static City CreateLondon() => City.Create(
        "London", "United Kingdom", "GB", "Europe/London",
        Location.Create(51.5074m, -0.1278m).Value, 2643743, 11m);

    /// <summary>Paris city entity (seeded).</summary>
    public static City CreateParis() => City.Create(
        "Paris", "France", "FR", "Europe/Paris",
        Location.Create(48.8566m, 2.3522m).Value, 2988507, 35m);

    /// <summary>Berlin city entity (not seeded — for create tests).</summary>
    public static City CreateBerlin() => City.Create(
        "Berlin", "Germany", "DE", "Europe/Berlin",
        Location.Create(52.52m, 13.405m).Value, 2950159, 34m);

    // ──────────────────────────────────────────────────────────────────────────────
    // UserCity IDs
    // ──────────────────────────────────────────────────────────────────────────────

    public static readonly Guid LondonSubscriptionGuid = Guid.Parse("00000000-0000-0000-0000-100000000001");

    // ──────────────────────────────────────────────────────────────────────────────
    // Seeding
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds the InMemory database with deterministic test data.
    /// Idempotent — uses try/catch to handle duplicate key scenarios with InMemory.
    /// </summary>
    public static async Task SeedAsync(CoreDbContext dbContext)
    {
        // Check using direct DB query — InMemory store is shared across scopes
        if (dbContext.Cities.IgnoreAutoIncludes().Any(c => c.Id == CityId.Create(LondonCityGuid)))
        {
            return; // Already seeded
        }

        // Cities
        var london = CreateLondon();
        london.Id = CityId.Create(LondonCityGuid);
        dbContext.Cities.Add(london);

        var paris = CreateParis();
        paris.Id = CityId.Create(ParisCityGuid);
        dbContext.Cities.Add(paris);

        // User city subscriptions (test user → London as primary)
        var londonSubscription = UserCity.Create(TestUserId, london.Id, isPrimary: true, displayOrder: 0);
        londonSubscription.Id = UserCityId.Create(LondonSubscriptionGuid);
        dbContext.UserCities.Add(londonSubscription);

        // Current weather for recommendation and dashboard scenarios.
        var londonWeather = CurrentWeather.Create(london.Id);
        londonWeather.Temperature = 16m;
        londonWeather.ApparentTemperature = 14m;
        londonWeather.Humidity = 82;
        londonWeather.WeatherCode = 51;
        londonWeather.WindSpeed = 18m;
        londonWeather.WindDirection = 225;
        londonWeather.WindGusts = 32m;
        londonWeather.Precipitation = 1.2m;
        londonWeather.CloudCover = 90;
        londonWeather.Pressure = 1008m;
        londonWeather.RetrievedAt = DateTime.UtcNow;
        dbContext.CurrentWeathers.Add(londonWeather);

        var londonForecast = WeatherForecast.Create(london.Id);
        londonForecast.ForecastDate = DateOnly.FromDateTime(DateTime.UtcNow);
        londonForecast.DayWeatherCode = 51;
        londonForecast.TemperatureMax = 17m;
        londonForecast.TemperatureMin = 11m;
        londonForecast.ApparentTemperatureMax = 15m;
        londonForecast.ApparentTemperatureMin = 9m;
        londonForecast.PrecipitationSum = 2.4m;
        londonForecast.PrecipitationProbabilityMax = 70;
        londonForecast.WindSpeedMax = 22m;
        londonForecast.WindGustsMax = 38m;
        londonForecast.DominantWindDirection = 225;
        londonForecast.UvIndexMax = 3m;
        londonForecast.SunshineDurationSeconds = 16200;
        londonForecast.DaylightDurationSeconds = 54600;
        londonForecast.Sunrise = DateTime.UtcNow.Date.AddHours(4).AddMinutes(50);
        londonForecast.Sunset = DateTime.UtcNow.Date.AddHours(21).AddMinutes(20);
        londonForecast.RetrievedAt = DateTime.UtcNow;
        dbContext.WeatherForecasts.Add(londonForecast);

        // User profile
        var profile = UserProfile.Create(TestUserId, "test@example.com", "Test User");
        dbContext.UserProfiles.Add(profile);

        // User subscription (Free plan)
        var subscription = UserSubscription.CreateFree(TestUserId);
        dbContext.UserSubscriptions.Add(subscription);

        await dbContext.SaveChangesAsync();
    }
}
