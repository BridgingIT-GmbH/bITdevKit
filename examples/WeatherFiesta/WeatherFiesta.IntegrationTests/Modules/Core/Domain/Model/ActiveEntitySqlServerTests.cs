// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.IntegrationTests.Modules.Core.Domain.Model;

using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core;
using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;
using BridgingIT.DevKit.Examples.WeatherFiesta.IntegrationTests;
using Microsoft.Data.SqlClient;

/// <summary>
/// SQL Server-backed integration tests for ActiveEntity upsert and find behavior
/// against <see cref="WeatherForecast"/> with owned <see cref="HourlyForecast"/> children.
/// </summary>
[Collection(WeatherFiestaSqlServerCollection.Name)]
public sealed class ActiveEntitySqlServerTests(WeatherFiestaSqlServerFixture fixture)
{
    [Fact]
    public async Task UpsertAsync_NewForecast_InsertsWithHourlyForecasts()
    {
        // Arrange
        var connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString)
        {
            InitialCatalog = $"WeatherFiesta_{Guid.NewGuid():N}"
        }.ConnectionString;

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<CoreDbContext>(options => options.UseSqlServer(connectionString));
        services.AddActiveEntity(cfg =>
        {
            cfg.For<City, CityId>()
                .UseEntityFrameworkProvider(o => o.Context<CoreDbContext>());
            cfg.For<WeatherForecast, WeatherForecastId>()
                .UseEntityFrameworkProvider(o => o
                    .Context<CoreDbContext>()
                    .Options<CoreDbContext>(options => options.GenericMergeStrategy()));
        });

        await using var serviceProvider = services.BuildServiceProvider();
        ActiveEntityConfigurator.SetGlobalServiceProvider(serviceProvider);

        var city = City.Create(
            "London",
            "United Kingdom",
            "GB",
            "Europe/London",
            Location.Create(51.5074m, -0.1278m).Value,
            2643743,
            11m);
        city.Id = CityId.Create(Guid.NewGuid());

        using (var scope = serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            await dbContext.Database.EnsureDeletedAsync();
            await dbContext.Database.EnsureCreatedAsync();
            dbContext.Cities.Add(city);
            await dbContext.SaveChangesAsync();
        }

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
        upsertResult.IsSuccess.ShouldBeTrue();
        upsertResult.Value.action.ShouldBe(RepositoryActionResult.Inserted);

        // Act - find by specification
        var spec = new WeatherForecastByCityAndDateSpecification(city.Id, forecastDate);
        var foundResult = await WeatherForecast.FindOneAsync(
            spec,
            new FindOptions<WeatherForecast>().AddInclude(new IncludeOption<WeatherForecast>(f => f.HourlyForecasts)));

        // Assert - found entity matches
        foundResult.IsSuccess.ShouldBeTrue();
        foundResult.Value.ShouldNotBeNull();
        foundResult.Value.TemperatureMax.ShouldBe(24m);
        foundResult.Value.DayWeatherCode.ShouldBe(3);
        foundResult.Value.HourlyForecasts.Count.ShouldBe(24);
    }

    [Fact]
    public async Task UpsertAsync_ExistingForecast_ReplacesHourlyForecastsAndUpdatesScalars()
    {
        // Arrange
        var connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString)
        {
            InitialCatalog = $"WeatherFiesta_{Guid.NewGuid():N}"
        }.ConnectionString;

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<CoreDbContext>(options => options.UseSqlServer(connectionString));
        services.AddActiveEntity(cfg =>
        {
            cfg.For<City, CityId>()
                .UseEntityFrameworkProvider(o => o.Context<CoreDbContext>());
            cfg.For<WeatherForecast, WeatherForecastId>()
                .UseEntityFrameworkProvider(o => o
                    .Context<CoreDbContext>()
                    .Options<CoreDbContext>(options => options.GenericMergeStrategy()));
        });

        await using var serviceProvider = services.BuildServiceProvider();
        ActiveEntityConfigurator.SetGlobalServiceProvider(serviceProvider);

        var city = City.Create(
            "Berlin",
            "Germany",
            "DE",
            "Europe/Berlin",
            Location.Create(52.52m, 13.405m).Value,
            2950159,
            34m);
        city.Id = CityId.Create(Guid.NewGuid());

        using (var scope = serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            await dbContext.Database.EnsureDeletedAsync();
            await dbContext.Database.EnsureCreatedAsync();
            dbContext.Cities.Add(city);
            await dbContext.SaveChangesAsync();
        }

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
        insertResult.IsSuccess.ShouldBeTrue();

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
}
