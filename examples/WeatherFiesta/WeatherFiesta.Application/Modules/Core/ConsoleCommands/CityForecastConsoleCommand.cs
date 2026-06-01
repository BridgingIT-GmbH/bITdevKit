// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core.ConsoleCommands;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation;
using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Model;
using Spectre.Console;

/// <summary>
/// Console command that shows weather forecast for a specific city.
/// </summary>
public class CityForecastConsoleCommand : ConsoleCommandBase
{
    /// <summary>Gets or sets the city name to look up.</summary>
    [ConsoleCommandArgument(0, Description = "City name to get forecast for", Required = true)]
    public string CityName { get; set; }

    /// <summary>Gets or sets the number of forecast days to display.</summary>
    [ConsoleCommandOption("days", Alias = "d", Description = "Number of forecast days to show (default 3)")]
    public int Days { get; set; } = 3;

    /// <summary>Gets or sets a value indicating whether to include hourly breakdown.</summary>
    [ConsoleCommandOption("hourly", Alias = "h", Description = "Include hourly breakdown")]
    public bool Hourly { get; set; }

    /// <summary>Initializes a new instance of the <see cref="CityForecastConsoleCommand"/> class.</summary>
    public CityForecastConsoleCommand() : base("city-forecast", "Get weather forecast for a city", "cf") { }

    /// <inheritdoc />
    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        var cityResult = await City.FindAllAsync(
            c => c.AuditState.Deleted != true && c.Name.Contains(this.CityName),
            null,
            CancellationToken.None);

        if (cityResult.IsFailure)
        {
            console.MarkupLine($"[red]Error: {Markup.Escape(string.Join(", ", cityResult.Errors.Select(e => e.Message)))}[/]");
            return;
        }

        var cities = cityResult.Value.ToList();

        if (cities.Count == 0)
        {
            console.MarkupLine($"[yellow]No city found matching '{Markup.Escape(this.CityName)}'[/]");
            return;
        }

        if (cities.Count > 1)
        {
            console.MarkupLine($"[yellow]Multiple cities match '{Markup.Escape(this.CityName)}'. Please be more specific:[/]");
            foreach (var c in cities)
            {
                console.MarkupLine($"  • {Markup.Escape(c.Name)}, {Markup.Escape(c.Country)} ({Markup.Escape(c.CountryCode)})");
            }

            return;
        }

        var city = cities[0];
        var forecastResult = await WeatherForecast.FindAllAsync(wf => wf.CityId == city.Id, null, CancellationToken.None);

        if (forecastResult.IsFailure)
        {
            console.MarkupLine($"[red]Error loading forecasts: {Markup.Escape(string.Join(", ", forecastResult.Errors.Select(e => e.Message)))}[/]");
            return;
        }

        var forecasts = forecastResult.Value
            .OrderBy(f => f.ForecastDate)
            .Take(this.Days)
            .ToList();

        if (forecasts.Count == 0)
        {
            console.MarkupLine($"[yellow]No forecast data for {Markup.Escape(city.Name)}[/]");
            return;
        }

        console.MarkupLine($"[bold blue]Forecast for {Markup.Escape(city.Name)}, {Markup.Escape(city.Country)}[/]\n");

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Date");
        table.AddColumn("Weather");
        table.AddColumn("Temp");
        table.AddColumn("Feels");
        table.AddColumn("Precip");
        table.AddColumn("Wind");
        table.AddColumn("UV");
        table.AddColumn("Sunrise");
        table.AddColumn("Sunset");

        foreach (var f in forecasts)
        {
            var condition = Enumeration.GetAll<WeatherConditionCode>().FirstOrDefault(c => c.Id == f.DayWeatherCode);
            var weatherDesc = condition is not null ? condition.Description : f.DayWeatherCode.ToString();

            table.AddRow(
                f.ForecastDate.ToString("ddd MMM dd"),
                Markup.Escape(weatherDesc),
                $"{f.TemperatureMin}–{f.TemperatureMax}°C",
                $"{f.ApparentTemperatureMin}–{f.ApparentTemperatureMax}°C",
                $"{f.PrecipitationSum}mm ({f.PrecipitationProbabilityMax}%)",
                $"{f.WindSpeedMax} km/h",
                f.UvIndexMax.ToString("F1"),
                f.Sunrise.ToString("HH:mm"),
                f.Sunset.ToString("HH:mm"));
        }

        console.Write(table);

        if (this.Hourly && forecasts.Any())
        {
            foreach (var forecast in forecasts)
            {
                if (forecast.HourlyForecasts?.Any() != true)
                {
                    continue;
                }

                console.MarkupLine($"\n[bold]Hourly for {forecast.ForecastDate:ddd MMM dd}[/]");
                var hourlyTable = new Table().Border(TableBorder.Simple).Collapse();
                hourlyTable.AddColumn("H");
                hourlyTable.AddColumn("T");
                hourlyTable.AddColumn("Feel");
                hourlyTable.AddColumn("Code");
                hourlyTable.AddColumn("Wind");
                hourlyTable.AddColumn("Gust");
                hourlyTable.AddColumn("Hum");
                hourlyTable.AddColumn("Prec%");

                foreach (var h in forecast.HourlyForecasts.OrderBy(h => h.Hour))
                {
                    hourlyTable.AddRow(
                        $"{h.Hour:D2}",
                        $"{h.Temperature}°",
                        $"{h.ApparentTemperature}°",
                        h.WeatherCode.ToString(),
                        $"{h.WindSpeed}",
                        $"{h.WindGusts}",
                        $"{h.RelativeHumidity}%",
                        $"{h.PrecipitationProbability}%");
                }

                console.Write(hourlyTable);
            }
        }
    }
}
