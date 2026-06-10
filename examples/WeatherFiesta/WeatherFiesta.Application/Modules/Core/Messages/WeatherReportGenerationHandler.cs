// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core;
using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles weather report generation queue messages.
/// </summary>
public sealed class WeatherReportGenerationHandler(
    IWeatherReportTextGenerator textGenerator,
    ILogger<WeatherReportGenerationHandler> logger) : IQueueMessageHandler<WeatherReportGenerationMessage>
{
    /// <inheritdoc />
    public async Task Handle(WeatherReportGenerationMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var cityId = message.GetCityId();
        logger.LogInformation(
            "weather report generation started (cityId={CityId}, reportType={ReportType}, start={PeriodStartUtc}, end={PeriodEndUtc})",
            cityId,
            message.ReportType,
            message.PeriodStartUtc,
            message.PeriodEndUtc);

        var city = await LoadCityAsync(cityId, cancellationToken);
        var currentWeather = await LoadCurrentWeatherAsync(cityId, cancellationToken);
        var forecasts = await LoadForecastsAsync(message, cityId, cancellationToken);
        ValidateForecasts(message, forecasts);

        var request = CreateGenerationRequest(city, currentWeather, forecasts, message);
        var summaryResult = await textGenerator.GenerateAsync(request, cancellationToken);
        if (summaryResult.IsFailure || string.IsNullOrWhiteSpace(summaryResult.Value))
        {
            throw new InvalidOperationException($"Weather report text generation failed for city '{city.Name}' ({message.ReportType}): {string.Join("; ", summaryResult.Errors.Select(e => e.Message))}");
        }

        await UpsertReportAsync(message, cityId, summaryResult.Value, cancellationToken);

        logger.LogInformation(
            "weather report generation completed (cityId={CityId}, reportType={ReportType}, start={PeriodStartUtc}, end={PeriodEndUtc})",
            cityId,
            message.ReportType,
            message.PeriodStartUtc,
            message.PeriodEndUtc);
    }

    private static async Task<City> LoadCityAsync(CityId cityId, CancellationToken cancellationToken)
    {
        var result = await City.FindOneAsync(cityId, null, cancellationToken);
        if (result.IsFailure || result.Value is null)
        {
            throw new InvalidOperationException($"City '{cityId}' was not found for weather report generation.");
        }

        return result.Value;
    }

    private static async Task<CurrentWeather> LoadCurrentWeatherAsync(CityId cityId, CancellationToken cancellationToken)
    {
        var result = await CurrentWeather.FindOneAsync(new CurrentWeatherByCitySpecification(cityId), null, cancellationToken);
        if (result.IsFailure || result.Value is null)
        {
            throw new InvalidOperationException($"Current weather for city '{cityId}' was not found for weather report generation.");
        }

        return result.Value;
    }

    private static async Task<List<WeatherForecast>> LoadForecastsAsync(
        WeatherReportGenerationMessage message,
        CityId cityId,
        CancellationToken cancellationToken)
    {
        var options = new FindOptions<WeatherForecast>()
            .AddInclude(new IncludeOption<WeatherForecast>(f => f.HourlyForecasts));
        var result = await WeatherForecast.FindAllAsync(
            new Specification<WeatherForecast>(f =>
                f.CityId == cityId &&
                f.ForecastDate >= message.ForecastDateStart &&
                f.ForecastDate < message.ForecastDateEndExclusive),
            options,
            cancellationToken);

        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Forecasts for city '{cityId}' could not be loaded: {string.Join("; ", result.Errors.Select(e => e.Message))}");
        }

        return result.Value.OrderBy(f => f.ForecastDate).ToList();
    }

    private static void ValidateForecasts(WeatherReportGenerationMessage message, IReadOnlyCollection<WeatherForecast> forecasts)
    {
        var expectedDays = message.ForecastDateEndExclusive.DayNumber - message.ForecastDateStart.DayNumber;
        if (message.ReportType == WeatherReportType.Week && forecasts.Count < 7)
        {
            throw new InvalidOperationException($"Week weather report requires 7 forecast days but found {forecasts.Count}.");
        }

        if (message.ReportType is WeatherReportType.Today or WeatherReportType.Tomorrow && forecasts.Count < expectedDays)
        {
            throw new InvalidOperationException($"{message.ReportType} weather report requires {expectedDays} forecast day(s) but found {forecasts.Count}.");
        }
    }

    private static WeatherReportTextGenerationRequest CreateGenerationRequest(
        City city,
        CurrentWeather currentWeather,
        IReadOnlyCollection<WeatherForecast> forecasts,
        WeatherReportGenerationMessage message)
    {
        return new WeatherReportTextGenerationRequest(
            city.Name,
            message.ReportType,
            message.PeriodStartUtc,
            message.PeriodEndUtc,
            message.ForecastDateStart,
            message.ForecastDateEndExclusive,
            forecasts.Select(f => CreateDayInput(f, message.ReportType)).ToList(),
            message.ReportType == WeatherReportType.Tomorrow ? null : CreateCurrentWeatherInput(currentWeather));
    }

    private static CurrentWeatherInput CreateCurrentWeatherInput(CurrentWeather weather)
    {
        return new CurrentWeatherInput(
            weather.Temperature,
            weather.ApparentTemperature,
            weather.Humidity,
            weather.WeatherCode,
            weather.WindSpeed,
            weather.WindDirection,
            weather.WindGusts,
            weather.Precipitation,
            weather.CloudCover,
            weather.Pressure,
            weather.RetrievedAt);
    }

    private static WeatherForecastDayInput CreateDayInput(WeatherForecast forecast, WeatherReportType reportType)
    {
        return new WeatherForecastDayInput(
            forecast.ForecastDate,
            forecast.DayWeatherCode,
            forecast.TemperatureMin,
            forecast.TemperatureMax,
            forecast.ApparentTemperatureMin,
            forecast.ApparentTemperatureMax,
            forecast.PrecipitationSum,
            forecast.PrecipitationProbabilityMax,
            forecast.WindSpeedMax,
            forecast.WindGustsMax,
            forecast.DominantWindDirection,
            forecast.UvIndexMax,
            forecast.SunshineDurationSeconds,
            forecast.DaylightDurationSeconds,
            reportType == WeatherReportType.Week ? [] : CreateHourHighlights(forecast.HourlyForecasts));
    }

    private static IReadOnlyCollection<WeatherForecastHourHighlightInput> CreateHourHighlights(IEnumerable<HourlyForecast> hourlyForecasts)
    {
        var hours = hourlyForecasts?.ToList() ?? [];
        if (hours.Count == 0)
        {
            return [];
        }

        return new[]
            {
                hours.MaxBy(h => h.Precipitation),
                hours.MaxBy(h => h.WindGusts),
                hours.MaxBy(h => h.Temperature),
                hours.MinBy(h => h.Temperature)
            }
            .Where(h => h is not null)
            .DistinctBy(h => h.Hour)
            .OrderBy(h => h.Hour)
            .Select(h => new WeatherForecastHourHighlightInput(
                h.Hour,
                h.Temperature,
                h.ApparentTemperature,
                h.PrecipitationProbability,
                h.Precipitation,
                h.WeatherCode,
                h.WindSpeed,
                h.WindGusts,
                h.CloudCover,
                h.IsDay))
            .ToList();
    }

    private static async Task UpsertReportAsync(
        WeatherReportGenerationMessage message,
        CityId cityId,
        string summary,
        CancellationToken cancellationToken)
    {
        var existingResult = await WeatherReport.FindOneAsync(
            new Specification<WeatherReport>(r =>
                r.CityId == cityId &&
                r.ReportType == message.ReportType &&
                r.PeriodStartUtc == message.PeriodStartUtc &&
                r.PeriodEndUtc == message.PeriodEndUtc),
            null,
            cancellationToken);

        if (existingResult.IsSuccess && existingResult.Value is not null)
        {
            existingResult.Value.SetContent(summary);
            await existingResult.Value.UpdateAsync(cancellationToken);
            return;
        }

        if (existingResult.IsFailure && !existingResult.Errors.Any(e => e is NotFoundError))
        {
            throw new InvalidOperationException($"Existing weather report could not be loaded: {string.Join("; ", existingResult.Errors.Select(e => e.Message))}");
        }

        var report = WeatherReport.Create(
            cityId,
            message.ReportType,
            message.PeriodStartUtc,
            message.PeriodEndUtc,
            message.ForecastDateStart,
            message.ForecastDateEndExclusive);
        report.SetContent(summary);

        await report.InsertAsync(cancellationToken);
    }
}
