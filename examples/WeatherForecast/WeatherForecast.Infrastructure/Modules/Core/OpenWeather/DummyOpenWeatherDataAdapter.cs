// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Infrastructure;

using Application.Modules.Core;
using Domain.Model;
using Microsoft.Extensions.Logging;

public class DummyOpenWeatherDataAdapter : IWeatherDataAdapter
{
    private readonly ILogger logger;

    public DummyOpenWeatherDataAdapter(ILoggerFactory loggerFactory)
    {
        EnsureArg.IsNotNull(loggerFactory, nameof(loggerFactory));
        this.logger = loggerFactory.CreateLogger(this.GetType());
    }

    public async IAsyncEnumerable<Forecast> ToForecastAsync(City city)
    {
        EnsureArg.IsNotNull(city, nameof(city));
        EnsureArg.IsNotNull(city.Location, nameof(city.Location));

        this.logger.LogInformation("Fake weather adapter: generating dummy data for city={cityName}", city.Name);

        var random = new Random();
        for (var i = 0; i < 5; i++)
        {
            var date = DateTimeOffset.UtcNow.AddDays(i + 1); // Next 5 days
            var minTemp = random.Next(-10, 15);             // Random min temp (-10 to 15 °C)
            var maxTemp = random.Next(minTemp, 25);         // Random max temp (minTemp to 25 °C)
            var windSpeed = random.Next(0, 15);             // Random wind speed (0 to 15 m/s)
            var weatherDescriptions = new[]
            {
                "Sunny", "Cloudy", "Rainy", "Snowy", "Windy", "Foggy", "Stormy"
            };
            var description = weatherDescriptions[random.Next(weatherDescriptions.Length)];

            yield return Forecast.Create(
                city.Id,
                date,
                description,
                minTemp,
                maxTemp,
                windSpeed
            );

            await Task.Delay(1); // Simulate async operation
        }
    }
}
