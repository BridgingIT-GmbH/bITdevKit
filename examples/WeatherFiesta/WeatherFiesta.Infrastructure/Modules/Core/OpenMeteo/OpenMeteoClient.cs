// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Infrastructure.OpenMeteo;

using System.Globalization;
using System.Text.Json;

/// <summary>
/// Configuration options for the Open-Meteo API client.
/// </summary>
public class OpenMeteoClientOptions
{
    /// <summary>Gets or sets the base URL for the Open-Meteo Geocoding API.</summary>
    public string GeocodingBaseUrl { get; set; } = "https://geocoding-api.open-meteo.com/v1";

    /// <summary>Gets or sets the base URL for the Open-Meteo Forecast API.</summary>
    public string ForecastBaseUrl { get; set; } = "https://api.open-meteo.com/v1/forecast";

    /// <summary>Gets or sets the base URL for the Open-Meteo city lookup API.</summary>
    public string LookupBaseUrl { get; set; } = "https://geocoding-api.open-meteo.com/v1/get";

    /// <summary>Gets or sets the HTTP request timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>Gets or sets the number of retry attempts for failed requests.</summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>Gets or sets the delay between retries in milliseconds.</summary>
    public int RetryDelayMs { get; set; } = 1000;

    /// <summary>Gets or sets the delay between consecutive API calls in milliseconds.</summary>
    public int InterCallDelayMs { get; set; } = 100;
}

/// <summary>
/// HTTP client for the Open-Meteo weather and geocoding APIs.
/// </summary>
public class OpenMeteoClient : IOpenMeteoClient
{
    private readonly HttpClient httpClient;
    private readonly OpenMeteoClientOptions options;
    private readonly ILogger<OpenMeteoClient> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenMeteoClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="options">The client configuration options.</param>
    /// <param name="logger">The logger.</param>
    public OpenMeteoClient(
        HttpClient httpClient,
        IOptions<OpenMeteoClientOptions> options,
        ILogger<OpenMeteoClient> logger)
    {
        this.httpClient = httpClient;
        this.options = options.Value;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<GeocodingResult> SearchCitiesAsync(
        string name,
        string countryCode = null,
        CancellationToken cancellationToken = default)
    {
        var response = await this.SearchCitiesAllAsync(name, countryCode, cancellationToken);
        return response?.Results?.FirstOrDefault();
    }

    /// <inheritdoc />
    public async Task<GeocodingResponse> SearchCitiesAllAsync(
        string name,
        string countryCode = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"{this.options.GeocodingBaseUrl}/search?name={Uri.EscapeDataString(name)}&count=10&language=en&format=json";

        if (!string.IsNullOrEmpty(countryCode))
        {
            url += $"&country_code={Uri.EscapeDataString(countryCode)}";
        }

        var response = await this.httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<GeocodingResponse>(json);
    }

    /// <inheritdoc />
    public async Task<GeocodingResult> LookupCityAsync(
        long externalId,
        CancellationToken cancellationToken = default)
    {
        var url = $"{this.options.LookupBaseUrl}?id={externalId}&format=json";

        var response = await this.httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var geocodingResponse = JsonSerializer.Deserialize<GeocodingResponse>(json);

        return geocodingResponse?.Results?.FirstOrDefault();
    }

    /// <inheritdoc />
    public async Task<WeatherData> GetWeatherAsync(
        decimal latitude,
        decimal longitude,
        string timeZone,
        int forecastDays,
        CancellationToken cancellationToken = default)
    {
        var latStr = latitude.ToString("F7", CultureInfo.InvariantCulture);
        var lonStr = longitude.ToString("F7", CultureInfo.InvariantCulture);

        var url = $"{this.options.ForecastBaseUrl}?" +
            $"latitude={latStr}&longitude={lonStr}" +
            $"&current=temperature_2m,apparent_temperature,weather_code,wind_speed_10m,wind_direction_10m,wind_gusts_10m,relative_humidity_2m,precipitation,cloud_cover,pressure_msl" +
            $"&daily=weather_code,temperature_2m_max,temperature_2m_min,apparent_temperature_max,apparent_temperature_min,precipitation_sum,precipitation_probability_max,wind_speed_10m_max,wind_gusts_10m_max,wind_direction_10m_dominant,uv_index_max,sunshine_duration,daylight_duration,sunrise,sunset" +
            $"&hourly=temperature_2m,relative_humidity_2m,apparent_temperature,precipitation_probability,precipitation,weather_code,wind_speed_10m,wind_direction_10m,wind_gusts_10m,cloud_cover,visibility,is_day" +
            $"&timezone={Uri.EscapeDataString(timeZone)}" +
            $"&forecast_days={forecastDays}";

        this.logger.LogInformation("Fetching weather data for lat={Latitude}, lon={Longitude}, timezone={TimeZone}", latitude, longitude, timeZone);

        var response = await this.httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return this.ParseWeatherResponse(json);
    }

    private WeatherData ParseWeatherResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var result = new WeatherData();

        if (root.TryGetProperty("current", out var current))
        {
            result.Current = new CurrentWeatherData
            {
                Temperature = GetDecimalSafe(current, "temperature_2m"),
                ApparentTemperature = GetDecimalSafe(current, "apparent_temperature"),
                WeatherCode = GetInt32Safe(current, "weather_code"),
                WindSpeed = GetDecimalSafe(current, "wind_speed_10m"),
                WindDirection = GetInt32Safe(current, "wind_direction_10m"),
                WindGusts = GetDecimalSafe(current, "wind_gusts_10m"),
                Humidity = GetInt32Safe(current, "relative_humidity_2m"),
                Precipitation = GetDecimalSafe(current, "precipitation"),
                CloudCover = GetInt32Safe(current, "cloud_cover"),
                Pressure = GetDecimalSafe(current, "pressure_msl")
            };
        }

        if (root.TryGetProperty("daily", out var daily))
        {
            var timeArray = daily.GetProperty("time");
            var weatherCodeArray = daily.GetProperty("weather_code");
            var tempMaxArray = daily.GetProperty("temperature_2m_max");
            var tempMinArray = daily.GetProperty("temperature_2m_min");
            var appTempMaxArray = daily.GetProperty("apparent_temperature_max");
            var appTempMinArray = daily.GetProperty("apparent_temperature_min");
            var precipSumArray = daily.GetProperty("precipitation_sum");
            var precipProbArray = daily.GetProperty("precipitation_probability_max");
            var windSpeedMaxArray = daily.GetProperty("wind_speed_10m_max");
            var windGustsMaxArray = daily.GetProperty("wind_gusts_10m_max");
            var windDirArray = daily.GetProperty("wind_direction_10m_dominant");
            var uvIndexArray = daily.GetProperty("uv_index_max");
            var sunshineArray = daily.GetProperty("sunshine_duration");
            var daylightArray = daily.GetProperty("daylight_duration");
            var sunriseArray = daily.GetProperty("sunrise");
            var sunsetArray = daily.GetProperty("sunset");

            for (var i = 0; i < timeArray.GetArrayLength(); i++)
            {
                result.Daily.Add(new DailyForecastData
                {
                    Date = DateOnly.Parse(timeArray[i].GetString()!),
                    WeatherCode = GetInt32Safe(weatherCodeArray[i]),
                    TemperatureMax = GetDecimalSafe(tempMaxArray[i]),
                    TemperatureMin = GetDecimalSafe(tempMinArray[i]),
                    ApparentTemperatureMax = GetDecimalSafe(appTempMaxArray[i]),
                    ApparentTemperatureMin = GetDecimalSafe(appTempMinArray[i]),
                    PrecipitationSum = GetDecimalSafe(precipSumArray[i]),
                    PrecipitationProbabilityMax = GetInt32Safe(precipProbArray[i]),
                    WindSpeedMax = GetDecimalSafe(windSpeedMaxArray[i]),
                    WindGustsMax = GetDecimalSafe(windGustsMaxArray[i]),
                    DominantWindDirection = GetInt32Safe(windDirArray[i]),
                    UvIndexMax = GetDecimalSafe(uvIndexArray[i]),
                    SunshineDurationSeconds = GetInt32Safe(sunshineArray[i]),
                    DaylightDurationSeconds = GetInt32Safe(daylightArray[i]),
                    Sunrise = GetDateTimeSafe(sunriseArray[i]),
                    Sunset = GetDateTimeSafe(sunsetArray[i])
                });
            }
        }

        if (root.TryGetProperty("hourly", out var hourly))
        {
            var timeArray = hourly.GetProperty("time");
            var tempArray = hourly.GetProperty("temperature_2m");
            var humidityArray = hourly.GetProperty("relative_humidity_2m");
            var appTempArray = hourly.GetProperty("apparent_temperature");
            var precipProbArray = hourly.GetProperty("precipitation_probability");
            var precipArray = hourly.GetProperty("precipitation");
            var weatherCodeArray = hourly.GetProperty("weather_code");
            var windSpeedArray = hourly.GetProperty("wind_speed_10m");
            var windDirArray = hourly.GetProperty("wind_direction_10m");
            var windGustsArray = hourly.GetProperty("wind_gusts_10m");
            var cloudCoverArray = hourly.GetProperty("cloud_cover");
            var visibilityArray = hourly.GetProperty("visibility");
            var isDayArray = hourly.GetProperty("is_day");

            for (var i = 0; i < timeArray.GetArrayLength(); i++)
            {
                result.Hourly.Add(new HourlyForecastData
                {
                    Time = timeArray[i].GetDateTime(),
                    Temperature = GetDecimalSafe(tempArray[i]),
                    RelativeHumidity = GetInt32Safe(humidityArray[i]),
                    ApparentTemperature = GetDecimalSafe(appTempArray[i]),
                    PrecipitationProbability = GetInt32Safe(precipProbArray[i]),
                    Precipitation = GetDecimalSafe(precipArray[i]),
                    WeatherCode = GetInt32Safe(weatherCodeArray[i]),
                    WindSpeed = GetDecimalSafe(windSpeedArray[i]),
                    WindDirection = GetInt32Safe(windDirArray[i]),
                    WindGusts = GetDecimalSafe(windGustsArray[i]),
                    CloudCover = GetInt32Safe(cloudCoverArray[i]),
                    Visibility = GetDecimalSafe(visibilityArray[i], 10000m),
                    IsDay = GetInt32Safe(isDayArray[i]) == 1
                });
            }
        }

        return result;
    }

    private static int GetInt32Safe(JsonElement element, int defaultValue = 0)
    {
        return element.ValueKind == JsonValueKind.Null ? defaultValue : element.GetInt32();
    }

    private static int GetInt32Safe(JsonElement parent, string propertyName, int defaultValue = 0)
    {
        if (!parent.TryGetProperty(propertyName, out var element)) return defaultValue;
        return element.ValueKind == JsonValueKind.Null ? defaultValue : element.GetInt32();
    }

    private static decimal GetDecimalSafe(JsonElement element, decimal defaultValue = 0m)
    {
        return element.ValueKind == JsonValueKind.Null ? defaultValue : element.GetDecimal();
    }

    private static decimal GetDecimalSafe(JsonElement parent, string propertyName, decimal defaultValue = 0m)
    {
        if (!parent.TryGetProperty(propertyName, out var element)) return defaultValue;
        return element.ValueKind == JsonValueKind.Null ? defaultValue : element.GetDecimal();
    }

    private static DateTime GetDateTimeSafe(JsonElement element, DateTime defaultValue = default)
    {
        return element.ValueKind == JsonValueKind.Null ? defaultValue : element.GetDateTime();
    }
}
