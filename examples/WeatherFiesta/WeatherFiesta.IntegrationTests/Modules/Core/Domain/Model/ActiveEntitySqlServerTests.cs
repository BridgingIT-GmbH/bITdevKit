// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.IntegrationTests.Modules.Core.Domain.Model;

using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core;
using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;
using BridgingIT.DevKit.Examples.WeatherFiesta.IntegrationTests;
using BridgingIT.DevKit.Examples.WeatherFiesta.Presentation.Web.Server.Modules.Core;
using Microsoft.Data.SqlClient;

/// <summary>
/// SQL Server-backed integration tests for ActiveEntity persistence across WeatherFiesta domain entities.
/// </summary>
[Collection(WeatherFiestaSqlServerCollection.Name)]
public sealed class ActiveEntitySqlServerTests(WeatherFiestaSqlServerFixture fixture)
{
    [Fact]
    public async Task City_InsertFindAndUpdate_PersistsScalarAndValueObjectState()
    {
        // Arrange
        await using var serviceProvider = await CreateServiceProviderAsync();
        var city = CreateCity("London", "United Kingdom", "GB", "Europe/London", 2643743, 11m);

        // Act - insert and find by specification
        var insertResult = await city.InsertAsync();
        var foundResult = await City.FindOneAsync(new CityByExternalIdSpecification(2643743));

        // Assert - inserted state persisted
        insertResult.IsSuccess.ShouldBeTrue(string.Join("; ", insertResult.Errors.Select(e => e.Message)));
        foundResult.IsSuccess.ShouldBeTrue(string.Join("; ", foundResult.Errors.Select(e => e.Message)));
        foundResult.Value.Name.ShouldBe("London");
        foundResult.Value.Location.Latitude.ShouldBe(51.5074m);
        foundResult.Value.Elevation.ShouldBe(11m);

        // Act - update detached entity returned by ActiveEntity
        foundResult.Value.ChangeDetails(
            "London City",
            "United Kingdom",
            "GB",
            "Europe/London",
            Location.Create(51.5m, -0.12m).Value,
            15m);
        var updateResult = await foundResult.Value.UpdateAsync();

        // Assert - updated state persisted in SQL Server
        updateResult.IsSuccess.ShouldBeTrue(string.Join("; ", updateResult.Errors.Select(e => e.Message)));
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var persisted = await dbContext.Cities.SingleAsync(c => c.Id == city.Id);
        persisted.Name.ShouldBe("London City");
        persisted.Location.Longitude.ShouldBe(-0.12m);
        persisted.Elevation.ShouldBe(15m);
    }

    [Fact]
    public async Task UserCity_InsertUpdateSoftDeleteAndReactivate_PersistsAuditAndOrderingState()
    {
        // Arrange
        await using var serviceProvider = await CreateServiceProviderAsync();
        var city = await SeedCityAsync(serviceProvider);
        var userCity = UserCity.Create(TestData.TestUserId, city.Id, isPrimary: true, displayOrder: 0);

        // Act - insert and update ordering flags
        var insertResult = await userCity.InsertAsync();
        var foundResult = await UserCity.FindOneAsync(new UserCityByUserAndCitySpecification(TestData.TestUserId, city.Id));
        foundResult.Value.SetPrimary(false);
        foundResult.Value.SetDisplayOrder(5);
        var updateResult = await foundResult.Value.UpdateAsync();

        // Assert - update persisted
        insertResult.IsSuccess.ShouldBeTrue(string.Join("; ", insertResult.Errors.Select(e => e.Message)));
        updateResult.IsSuccess.ShouldBeTrue(string.Join("; ", updateResult.Errors.Select(e => e.Message)));
        using (var scope = serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            var persisted = await dbContext.UserCities.SingleAsync(uc => uc.Id == userCity.Id);
            persisted.IsPrimary.ShouldBeFalse();
            persisted.DisplayOrder.ShouldBe(5);
        }

        // Act - soft delete filters active spec, including-deleted spec still finds it
        foundResult.Value.SoftDelete("test");
        var softDeleteResult = await foundResult.Value.UpdateAsync();
        var activeSubscriptions = await UserCity.FindAllAsync(new UserCitiesByUserSpecification(TestData.TestUserId));
        var deletedResult = await UserCity.FindOneAsync(new UserCityByUserAndCityIncludingDeletedSpecification(TestData.TestUserId, city.Id));

        // Assert - soft delete persisted and query filters respect it
        softDeleteResult.IsSuccess.ShouldBeTrue(string.Join("; ", softDeleteResult.Errors.Select(e => e.Message)));
        activeSubscriptions.Value.ShouldBeEmpty();
        deletedResult.Value.AuditState.IsDeleted().ShouldBeTrue();

        // Act - reactivate and persist again
        deletedResult.Value.Reactivate();
        deletedResult.Value.SetPrimary(true);
        var reactivateResult = await deletedResult.Value.UpdateAsync();

        // Assert
        reactivateResult.IsSuccess.ShouldBeTrue(string.Join("; ", reactivateResult.Errors.Select(e => e.Message)));
        var reactivated = await UserCity.FindOneAsync(new UserCityByUserAndCitySpecification(TestData.TestUserId, city.Id));
        reactivated.Value.AuditState.IsDeleted().ShouldBeFalse();
        reactivated.Value.IsPrimary.ShouldBeTrue();
    }

    [Fact]
    public async Task CurrentWeather_UpsertInsertAndUpdate_PersistsWeatherMeasurements()
    {
        // Arrange
        await using var serviceProvider = await CreateServiceProviderAsync();
        var city = await SeedCityAsync(serviceProvider);
        var weather = CurrentWeather.Create(city.Id);
        weather.Temperature = 18.5m;
        weather.ApparentTemperature = 17.0m;
        weather.Humidity = 65;
        weather.WeatherCode = 2;
        weather.WindSpeed = 12.5m;
        weather.WindDirection = 220;
        weather.WindGusts = 21.5m;
        weather.Precipitation = 0.3m;
        weather.CloudCover = 40;
        weather.Pressure = 1012.4m;
        weather.RetrievedAt = DateTime.UtcNow.AddMinutes(-5);

        // Act - insert through upsert
        var insertResult = await weather.UpsertAsync();
        var foundResult = await CurrentWeather.FindOneAsync(new CurrentWeatherByCitySpecification(city.Id));

        // Assert - inserted state persisted
        insertResult.IsSuccess.ShouldBeTrue(string.Join("; ", insertResult.Errors.Select(e => e.Message)));
        insertResult.Value.action.ShouldBe(RepositoryActionResult.Inserted);
        foundResult.Value.Temperature.ShouldBe(18.5m);
        foundResult.Value.Humidity.ShouldBe(65);
        foundResult.Value.Pressure.ShouldBe(1012.4m);

        // Act - update existing row
        foundResult.Value.Temperature = 22.1m;
        foundResult.Value.WeatherCode = 61;
        foundResult.Value.RetrievedAt = DateTime.UtcNow;
        var updateResult = await foundResult.Value.UpsertAsync();

        // Assert
        updateResult.IsSuccess.ShouldBeTrue(string.Join("; ", updateResult.Errors.Select(e => e.Message)));
        updateResult.Value.action.ShouldBe(RepositoryActionResult.Updated);
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var persisted = await dbContext.CurrentWeathers.SingleAsync(cw => cw.CityId == city.Id);
        persisted.Temperature.ShouldBe(22.1m);
        persisted.WeatherCode.ShouldBe(61);
    }

    [Fact]
    public async Task UserProfile_InsertFindUpdateAndSoftDelete_PersistsProfilePreferencesAndAuditState()
    {
        // Arrange
        await using var serviceProvider = await CreateServiceProviderAsync();
        var profile = UserProfile.Create(TestData.TestUserId, "test@example.com", "Test User");

        // Act - insert and find by UserId, not entity Id
        var insertResult = await profile.InsertAsync();
        var foundResult = await UserProfile.FindOneAsync(new UserProfileByUserSpecification(TestData.TestUserId));

        // Assert - inserted profile persisted
        insertResult.IsSuccess.ShouldBeTrue(string.Join("; ", insertResult.Errors.Select(e => e.Message)));
        foundResult.Value.Id.ShouldNotBe(UserProfileId.Create(Guid.Parse(TestData.TestUserId)));
        foundResult.Value.Email.ShouldBe("test@example.com");
        foundResult.Value.TemperatureUnit.ShouldBe(TemperatureUnit.Celsius);

        // Act - update profile fields, preferences, and audit state
        foundResult.Value.UpdateProfile("Updated User", "updated@example.com");
        foundResult.Value.UpdatePreferences(TemperatureUnit.Fahrenheit, WindSpeedUnit.Mph);
        foundResult.Value.SoftDelete();
        var updateResult = await foundResult.Value.UpdateAsync();

        // Assert
        updateResult.IsSuccess.ShouldBeTrue(string.Join("; ", updateResult.Errors.Select(e => e.Message)));
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var persisted = await dbContext.UserProfiles.SingleAsync(p => p.UserId == TestData.TestUserId);
        persisted.Name.ShouldBe("Updated User");
        persisted.Email.ShouldBe("updated@example.com");
        persisted.TemperatureUnit.ShouldBe(TemperatureUnit.Fahrenheit);
        persisted.WindSpeedUnit.ShouldBe(WindSpeedUnit.Mph);
        persisted.AuditState.IsDeleted().ShouldBeTrue();
    }

    [Fact]
    public async Task UserSubscription_InsertFindUpdateCancelAndReactivate_PersistsPlanStatusAndAuditState()
    {
        // Arrange
        await using var serviceProvider = await CreateServiceProviderAsync();
        var subscription = UserSubscription.CreateFree(TestData.TestUserId);

        // Act - insert and find by user id
        var insertResult = await subscription.InsertAsync();
        var foundResult = await UserSubscription.FindOneAsync(new SubscriptionByUserSpecification(TestData.TestUserId));

        // Assert - inserted free subscription persisted
        insertResult.IsSuccess.ShouldBeTrue(string.Join("; ", insertResult.Errors.Select(e => e.Message)));
        foundResult.Value.Plan.ShouldBe(SubscriptionPlan.Free);
        foundResult.Value.Status.ShouldBe(SubscriptionStatus.Active);
        foundResult.Value.BillingCycle.ShouldBe(SubscriptionBillingCycle.Never);

        // Act - update plan and cancel
        foundResult.Value.ChangePlan(SubscriptionPlan.Basic, SubscriptionBillingCycle.Monthly);
        foundResult.Value.Cancel();
        var cancelResult = await foundResult.Value.UpdateAsync();

        // Assert - cancelled state persisted
        cancelResult.IsSuccess.ShouldBeTrue(string.Join("; ", cancelResult.Errors.Select(e => e.Message)));
        using (var scope = serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            var persisted = await dbContext.UserSubscriptions.SingleAsync(s => s.UserId == TestData.TestUserId);
            persisted.Plan.ShouldBe(SubscriptionPlan.Basic);
            persisted.Status.ShouldBe(SubscriptionStatus.Cancelled);
            persisted.BillingCycle.ShouldBe(SubscriptionBillingCycle.Monthly);
            persisted.EndDate.ShouldNotBeNull();
        }

        // Act - soft-delete then reactivate
        foundResult.Value.AuditState.SetDeleted("test", "subscription test");
        foundResult.Value.Reactivate();
        var reactivateResult = await foundResult.Value.UpdateAsync();

        // Assert
        reactivateResult.IsSuccess.ShouldBeTrue(string.Join("; ", reactivateResult.Errors.Select(e => e.Message)));
        var reactivated = await UserSubscription.FindOneAsync(new SubscriptionByUserSpecification(TestData.TestUserId));
        reactivated.Value.Status.ShouldBe(SubscriptionStatus.Active);
        reactivated.Value.EndDate.ShouldBeNull();
        reactivated.Value.AuditState.IsDeleted().ShouldBeFalse();
    }

    [Fact]
    public async Task UpsertAsync_NewForecast_InsertsWithHourlyForecasts()
    {
        // Arrange
        await using var serviceProvider = await CreateServiceProviderAsync();
        var city = await SeedCityAsync(serviceProvider);
        var forecastDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1));
        var forecast = WeatherForecast.Create(city.Id);
        forecast.ForecastDate = forecastDate;
        forecast.TemperatureMax = 24m;
        forecast.TemperatureMin = 14m;
        forecast.DayWeatherCode = 3;
        forecast.RetrievedAt = DateTime.UtcNow;
        forecast.HourlyForecasts = Enumerable.Range(0, 24)
            .Select(h => new HourlyForecast
            {
                Hour = h,
                Temperature = 10m + h,
                WeatherCode = 3
            })
            .ToList();

        // Act
        var upsertResult = await forecast.UpsertAsync();

        // Assert - upsert succeeded
        upsertResult.IsSuccess.ShouldBeTrue(string.Join("; ", upsertResult.Errors.Select(e => e.Message)));
        upsertResult.Value.action.ShouldBe(RepositoryActionResult.Inserted);

        // Act - find by specification
        var spec = new WeatherForecastByCityAndDateSpecification(city.Id, forecastDate);
        var foundResult = await WeatherForecast.FindOneAsync(
            spec,
            new FindOptions<WeatherForecast>().AddInclude(new IncludeOption<WeatherForecast>(f => f.HourlyForecasts)));

        // Assert - found entity matches
        foundResult.IsSuccess.ShouldBeTrue(string.Join("; ", foundResult.Errors.Select(e => e.Message)));
        foundResult.Value.ShouldNotBeNull();
        foundResult.Value.TemperatureMax.ShouldBe(24m);
        foundResult.Value.DayWeatherCode.ShouldBe(3);
        foundResult.Value.HourlyForecasts.Count.ShouldBe(24);
    }

    [Fact]
    public async Task UpsertAsync_ExistingForecast_ReplacesHourlyForecastsAndUpdatesScalars()
    {
        // Arrange
        await using var serviceProvider = await CreateServiceProviderAsync();
        var city = await SeedCityAsync(serviceProvider, "Berlin", "Germany", "DE", "Europe/Berlin", 2950159, 34m);
        var forecastDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1));
        var forecast = WeatherForecast.Create(city.Id);
        forecast.ForecastDate = forecastDate;
        forecast.TemperatureMax = 20m;
        forecast.TemperatureMin = 10m;
        forecast.DayWeatherCode = 1;
        forecast.RetrievedAt = DateTime.UtcNow;
        forecast.HourlyForecasts = Enumerable.Range(0, 24)
            .Select(h => new HourlyForecast
            {
                Hour = h,
                Temperature = 8m + h,
                WeatherCode = 1
            })
            .ToList();

        var insertResult = await forecast.UpsertAsync();
        insertResult.IsSuccess.ShouldBeTrue(string.Join("; ", insertResult.Errors.Select(e => e.Message)));

        // Act - mutate scalars and replace children
        var spec = new WeatherForecastByCityAndDateSpecification(city.Id, forecastDate);
        var found = (await WeatherForecast.FindOneAsync(spec)).Value;

        found.TemperatureMax = 29m;
        found.TemperatureMin = 18m;
        found.DayWeatherCode = 61;
        found.HourlyForecasts.Clear();
        for (var h = 0; h < 24; h++)
        {
            found.HourlyForecasts.Add(new HourlyForecast
            {
                Hour = h,
                Temperature = 20m + h,
                WeatherCode = 61
            });
        }

        var updateResult = await found.UpsertAsync();

        // Assert - upsert result
        updateResult.IsSuccess.ShouldBeTrue(string.Join("; ", updateResult.Errors.Select(e => e.Message)));
        updateResult.Value.action.ShouldBe(RepositoryActionResult.Updated);

        // Assert - persisted state via DbContext Include
        using var assertScope = serviceProvider.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var persisted = await assertDb.WeatherForecasts
            .Include(f => f.HourlyForecasts)
            .SingleAsync(f => f.CityId == city.Id && f.ForecastDate == forecastDate);

        persisted.TemperatureMax.ShouldBe(29m);
        persisted.TemperatureMin.ShouldBe(18m);
        persisted.DayWeatherCode.ShouldBe(61);
        persisted.HourlyForecasts.Count.ShouldBe(24);
        persisted.HourlyForecasts.ShouldAllBe(hf => hf.WeatherCode == 61);
        persisted.HourlyForecasts.OrderBy(h => h.Hour).First().Temperature.ShouldBe(20m);
        persisted.HourlyForecasts.OrderBy(h => h.Hour).Last().Temperature.ShouldBe(43m);
    }

    private async Task<ServiceProvider> CreateServiceProviderAsync()
    {
        var connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString)
        {
            InitialCatalog = $"WeatherFiesta_{Guid.NewGuid():N}"
        }.ConnectionString;

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<CoreDbContext>(options => options.UseSqlServer(connectionString));
        services.AddActiveEntities();

        var serviceProvider = services.BuildServiceProvider();
        ActiveEntityConfigurator.SetGlobalServiceProvider(serviceProvider);

        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        return serviceProvider;
    }

    private static City CreateCity(
        string name,
        string country,
        string countryCode,
        string timeZone,
        long externalId,
        decimal elevation)
    {
        return City.Create(
            name,
            country,
            countryCode,
            timeZone,
            Location.Create(51.5074m, -0.1278m).Value,
            externalId,
            elevation);
    }

    private static async Task<City> SeedCityAsync(
        IServiceProvider serviceProvider,
        string name = "London",
        string country = "United Kingdom",
        string countryCode = "GB",
        string timeZone = "Europe/London",
        long externalId = 2643743,
        decimal elevation = 11m)
    {
        var entity = CreateCity(name, country, countryCode, timeZone, externalId, elevation);
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        dbContext.Cities.Add(entity);
        await dbContext.SaveChangesAsync();

        return entity;
    }
}
