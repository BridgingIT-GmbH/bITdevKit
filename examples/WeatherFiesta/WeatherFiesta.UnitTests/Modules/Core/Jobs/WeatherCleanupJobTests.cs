// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.UnitTests.Modules.Core.Jobs;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;

/// <summary>
/// Unit tests for <see cref="WeatherCleanupJob"/>.
/// </summary>
public class WeatherCleanupJobTests
{
    [Fact]
    public async Task DispatchAndWaitAsync_NoStaleWeatherData_CompletesWithoutDeletingRows()
    {
        // Arrange
        var dbName = $"WeatherCleanupJobTests_{Guid.NewGuid():N}";
        using var harness = JobSchedulerTestHarness.Create()
            .WithJob<WeatherCleanupJob>("core_cleanup", job => job
                .AddTrigger("manual", trigger => trigger.Manual()))
            .WithServices(services =>
            {
                services.AddLogging();
                services.Configure<CoreModuleConfiguration>(options =>
                {
                    options.ConnectionStrings = new Dictionary<string, string> { ["Default"] = "Test" };
                    options.Jobs.CleanupRetentionDays = 31;
                });
                services.AddDbContext<CoreDbContext>(options => options.UseInMemoryDatabase(dbName));
                services.AddActiveEntity(cfg =>
                {
                    cfg.For<CurrentWeather, CurrentWeatherId>()
                        .UseEntityFrameworkProvider(o => o.Context<CoreDbContext>());
                    cfg.For<WeatherForecast, WeatherForecastId>()
                        .UseEntityFrameworkProvider(o => o.Context<CoreDbContext>());
                });
            })
            .Build();

        ActiveEntityConfigurator.SetGlobalServiceProvider(harness.Services);

        using (var scope = harness.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
        }

        // Act
        var result = await harness.DispatchAndWaitAsync<WeatherCleanupJob>();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Completed);
        result.Value.Messages.ShouldContain(m => m.Contains("CurrentWeatherDeleted=0") && m.Contains("ForecastsDeleted=0"));
    }

    [Fact]
    public async Task DispatchAndWaitAsync_StaleWeatherData_DeletesOnlyExpiredRows()
    {
        // Arrange
        var dbName = $"WeatherCleanupJobTests_{Guid.NewGuid():N}";
        using var harness = JobSchedulerTestHarness.Create()
            .WithJob<WeatherCleanupJob>("core_cleanup", job => job
                .AddTrigger("manual", trigger => trigger.Manual()))
            .WithServices(services =>
            {
                services.AddLogging();
                services.Configure<CoreModuleConfiguration>(options =>
                {
                    options.ConnectionStrings = new Dictionary<string, string> { ["Default"] = "Test" };
                    options.Jobs.CleanupRetentionDays = 31;
                });
                services.AddDbContext<CoreDbContext>(options => options.UseInMemoryDatabase(dbName));
                services.AddActiveEntity(cfg =>
                {
                    cfg.For<CurrentWeather, CurrentWeatherId>()
                        .UseEntityFrameworkProvider(o => o.Context<CoreDbContext>());
                    cfg.For<WeatherForecast, WeatherForecastId>()
                        .UseEntityFrameworkProvider(o => o.Context<CoreDbContext>());
                });
            })
            .Build();

        ActiveEntityConfigurator.SetGlobalServiceProvider(harness.Services);

        var cityId = CityId.Create(Guid.NewGuid());
        var now = DateTime.UtcNow;
        var staleCurrentWeather = CreateCurrentWeather(cityId, now.AddDays(-32));
        var freshCurrentWeather = CreateCurrentWeather(cityId, now.AddDays(-30));
        var staleForecast = CreateForecast(cityId, now.AddDays(-32));
        var freshForecast = CreateForecast(cityId, now.AddDays(-30));

        using (var scope = harness.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
            dbContext.CurrentWeathers.AddRange(staleCurrentWeather, freshCurrentWeather);
            dbContext.WeatherForecasts.AddRange(staleForecast, freshForecast);
            await dbContext.SaveChangesAsync();
        }

        // Act
        var result = await harness.DispatchAndWaitAsync<WeatherCleanupJob>();
        var store = harness.Services.GetRequiredService<IJobStoreProvider>();
        var execution = (await store.Executions.ListByOccurrenceAsync(result.Value.OccurrenceId)).Single();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Completed, execution.Message);
        result.Value.Messages.ShouldContain(m => m.Contains("CurrentWeatherDeleted=1") && m.Contains("ForecastsDeleted=1"));

        using (var scope = harness.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            var remainingWeather = await dbContext.CurrentWeathers.SingleAsync();
            var remainingForecast = await dbContext.WeatherForecasts.SingleAsync();

            remainingWeather.Id.ShouldBe(freshCurrentWeather.Id);
            remainingForecast.Id.ShouldBe(freshForecast.Id);
        }
    }

    private static CurrentWeather CreateCurrentWeather(CityId cityId, DateTime retrievedAt)
    {
        var weather = CurrentWeather.Create(cityId);
        weather.Id = CurrentWeatherId.Create(Guid.NewGuid());
        weather.RetrievedAt = retrievedAt;
        weather.Temperature = 20;

        return weather;
    }

    private static WeatherForecast CreateForecast(CityId cityId, DateTime retrievedAt)
    {
        var forecast = WeatherForecast.Create(cityId);
        forecast.Id = WeatherForecastId.Create(Guid.NewGuid());
        forecast.RetrievedAt = retrievedAt;
        forecast.ForecastDate = DateOnly.FromDateTime(retrievedAt);
        forecast.TemperatureMax = 20;
        forecast.TemperatureMin = 10;

        return forecast;
    }
}
