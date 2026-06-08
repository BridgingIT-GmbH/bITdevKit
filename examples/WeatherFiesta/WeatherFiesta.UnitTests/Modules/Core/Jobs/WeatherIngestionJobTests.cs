// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.UnitTests.Modules.Core.Jobs;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;

/// <summary>
/// Unit tests for <see cref="WeatherIngestionJob"/>.
/// </summary>
public class WeatherIngestionJobTests
{
    [Fact]
    public async Task DispatchAndWaitAsync_StaleCity_PersistsWeatherData()
    {
        // Arrange
        var weatherAgent = Substitute.For<IWeatherAgent>();
        weatherAgent.IngestWeatherAsync(
                Arg.Any<string>(),
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<WeatherIngestionResult>.Success(new WeatherIngestionResult
            {
                CurrentWeather = new CurrentWeatherData
                {
                    TemperatureCelsius = 21.5,
                    ApparentTemperatureCelsius = 20.2,
                    RelativeHumidity = 55,
                    WindSpeedKmh = 14,
                    WindGustsKmh = 28,
                    WindDirectionDegrees = 180,
                    WeatherCode = 2,
                    PressureHpa = 1015,
                    CloudCoverPercent = 40,
                    PrecipitationMm = 0.1,
                    RetrievedAt = DateTime.UtcNow
                },
                Forecasts =
                [
                    new ForecastData
                    {
                        ForecastDate = DateTime.UtcNow.Date.AddDays(1),
                        TemperatureMaxCelsius = 24,
                        TemperatureMinCelsius = 16,
                        ApparentTemperatureMaxCelsius = 25,
                        ApparentTemperatureMinCelsius = 15,
                        WeatherCode = 3,
                        PrecipitationSumMm = 2.4,
                        PrecipitationProbability = 20,
                        WindSpeedMaxKmh = 18,
                        WindGustsMaxKmh = 31,
                        DominantWindDirectionDegrees = 225,
                        UvIndex = 5,
                        SunshineDurationSeconds = 18000,
                        DaylightDurationSeconds = 54000,
                        Sunrise = DateTime.UtcNow.Date.AddHours(5),
                        Sunset = DateTime.UtcNow.Date.AddHours(21)
                    }
                ]
            }));

        var dbName = $"WeatherIngestionJobTests_{Guid.NewGuid():N}";
        using var harness = JobSchedulerTestHarness.Create()
            .WithJob<WeatherIngestionJob>("core_ingestion", job => job
                .AddTrigger("manual", trigger => trigger.Manual()))
            .WithServices(services =>
            {
                services.AddLogging();
                services.AddSingleton(weatherAgent);
                services.Configure<CoreModuleConfiguration>(options =>
                {
                    options.ConnectionStrings = new Dictionary<string, string> { ["Default"] = "Test" };
                    options.StaleThresholdMinutes = 60;
                });
                services.AddDbContext<CoreDbContext>(options => options.UseInMemoryDatabase(dbName));
                services.AddActiveEntity(cfg =>
                {
                    cfg.For<City, CityId>()
                        .UseEntityFrameworkProvider(o => o.Context<CoreDbContext>());
                    cfg.For<CurrentWeather, CurrentWeatherId>()
                        .UseEntityFrameworkProvider(o => o.Context<CoreDbContext>());
                    cfg.For<WeatherForecast, WeatherForecastId>()
                        .UseEntityFrameworkProvider(o => o.Context<CoreDbContext>());
                });
            })
            .Build();

        ActiveEntityConfigurator.SetGlobalServiceProvider(harness.Services);
        var city = City.Create(
            "London",
            "United Kingdom",
            "GB",
            "Europe/London",
            Location.Create(51.5074m, -0.1278m).Value,
            2643743,
            11m);
        city.Id = CityId.Create(Guid.NewGuid());

        using (var scope = harness.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
            dbContext.Cities.Add(city);
            await dbContext.SaveChangesAsync();
        }

        // Act
        var result = await harness.DispatchAndWaitAsync<WeatherIngestionJob>();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(JobExecutionStatus.Completed);
        result.Value.Messages.ShouldContain(m => m.Contains("Success=1"));
        await weatherAgent.Received(1).IngestWeatherAsync(
            city.Id.ToString(),
            51.5074,
            -0.1278,
            Arg.Any<CancellationToken>());

        using (var scope = harness.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            var currentWeather = await dbContext.CurrentWeathers.SingleAsync(w => w.CityId == city.Id);
            var forecast = await dbContext.WeatherForecasts.SingleAsync(w => w.CityId == city.Id);

            currentWeather.Temperature.ShouldBe(21.5m);
            currentWeather.WeatherCode.ShouldBe(2);
            currentWeather.WindGusts.ShouldBe(28m);
            forecast.TemperatureMax.ShouldBe(24m);
            forecast.DayWeatherCode.ShouldBe(3);
            forecast.ApparentTemperatureMax.ShouldBe(25m);
            forecast.ApparentTemperatureMin.ShouldBe(15m);
            forecast.PrecipitationSum.ShouldBe(2.4m);
            forecast.WindGustsMax.ShouldBe(31m);
            forecast.DominantWindDirection.ShouldBe(225);
            forecast.SunshineDurationSeconds.ShouldBe(18000);
            forecast.DaylightDurationSeconds.ShouldBe(54000);
        }
    }
}
