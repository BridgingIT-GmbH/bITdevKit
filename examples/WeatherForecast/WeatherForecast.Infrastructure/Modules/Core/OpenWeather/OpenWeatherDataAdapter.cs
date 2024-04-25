// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Infrastructure;

using System;
using System.Collections.Generic;
using System.Net.Http;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Examples.WeatherForecast.Application.Modules.Core;
using BridgingIT.DevKit.Examples.WeatherForecast.Domain.Model;
using EnsureThat;
using Microsoft.Extensions.Logging;

public class OpenWeatherDataAdapter : IWeatherDataAdapter
{
    private readonly ILogger logger;
    private readonly HttpClient client;
    private readonly string apiKey;

    public OpenWeatherDataAdapter(ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory, string apiKey)
    {
        EnsureArg.IsNotNull(loggerFactory, nameof(loggerFactory));
        EnsureArg.IsNotNull(httpClientFactory, nameof(httpClientFactory));
        EnsureArg.IsNotNullOrEmpty(apiKey, nameof(apiKey));

        this.logger = loggerFactory.CreateLogger(this.GetType());
        this.client = httpClientFactory.CreateClient("OpenWeatherClient");
        this.apiKey = apiKey;
    }

    public async IAsyncEnumerable<Forecast> ToForecastAsync(City city)
    {
        EnsureArg.IsNotNull(city, nameof(city));
        EnsureArg.IsNotNull(city.Location, nameof(city.Location));

        // doc: https://openweathermap.org/api/one-call-api
        var url = $"data/2.5/onecall?lat={city.Location.Latitude}&lon={city.Location.Longitude}&units=metric&APPID={this.apiKey}";
        this.logger.LogInformation("openweather adapter: processing (city={cityName})", city.Name);
        this.logger.LogDebug("openweather adapter: request (url={url})", url);
        var response = await this.client.GetAsync(url).AnyContext();
        if (response.IsSuccessStatusCode)
        {
            this.logger.LogInformation("openweather adapter: processed (city={cityName})", city.Name);
            var content = await response.Content.ReadAsAsync<Temperatures>().AnyContext();
            foreach (var daily in content.Daily.SafeNull())
            {
                yield return Forecast.Create(
                    city.Id,
                    DateTimeOffset.FromUnixTimeSeconds(daily.Dt),
                    daily.Weather[0].Description,
                    daily.Temp.Min,
                    daily.Temp.Max,
                    daily.WindSpeed); // TODO: map more
            }
        }
        else
        {
            this.logger.LogWarning("openweather adapter: failed (city={cityName})", city.Name);
            //    throw
        }
    }
}
