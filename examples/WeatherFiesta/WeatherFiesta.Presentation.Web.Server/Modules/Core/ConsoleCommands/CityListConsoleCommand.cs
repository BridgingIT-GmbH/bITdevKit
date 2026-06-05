// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Presentation.Web.Server.Modules.Core;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Modules.Core.Model;
using BridgingIT.DevKit.Presentation;
using Spectre.Console;

/// <summary>
/// Console command that lists all cities with their current weather conditions.
/// </summary>
public class CityListConsoleCommand : AppGroupConsoleCommandBase
{
    /// <summary>Gets or sets the optional city, country, or country-code filter.</summary>
    [ConsoleCommandArgument(0, Description = "Optional city, country, or country-code filter")]
    public string Filter { get; set; }

    /// <summary>Gets or sets a value indicating whether to show detailed city information.</summary>
    [ConsoleCommandOption("detailed", Alias = "d", Description = "Show detailed information including coordinates and timezone")]
    public bool Detailed { get; set; }

    /// <summary>Initializes a new instance of the <see cref="CityListConsoleCommand"/> class.</summary>
    public CityListConsoleCommand() : base("cities", "List all cities with current weather", "list", "ls", "city-list", "cl") { }

    /// <inheritdoc />
    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var cityResult = await City.FindAllAsync(null, CancellationToken.None);

        if (cityResult.IsFailure)
        {
            console.MarkupLine($"[red]Error loading cities: {Markup.Escape(string.Join(", ", cityResult.Errors.Select(e => e.Message)))}[/]");
            return;
        }

        var cities = cityResult.Value.ToList();
        if (!string.IsNullOrWhiteSpace(this.Filter))
        {
            cities = cities
                .Where(city => city.Name.Contains(this.Filter, StringComparison.OrdinalIgnoreCase)
                    || city.Country.Contains(this.Filter, StringComparison.OrdinalIgnoreCase)
                    || city.CountryCode.Contains(this.Filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (cities.Count == 0)
        {
            console.MarkupLine(string.IsNullOrWhiteSpace(this.Filter)
                ? "[yellow]No cities found. Seed data first.[/]"
                : $"[yellow]No cities found matching '{Markup.Escape(this.Filter)}'.[/]");
            return;
        }

        // Fetch current weather separately (navigation property not eagerly loaded)
        var weatherResult = await CurrentWeather.FindAllAsync(null, CancellationToken.None);
        var weatherByCity = weatherResult.IsFailure
            ? new Dictionary<string, CurrentWeather>()
            : weatherResult.Value.ToDictionary(cw => cw.CityId.Value.ToString(), cw => cw);

        if (this.Detailed)
        {
            var table = new Table().Border(TableBorder.Rounded).Title("[bold]Cities (Detailed)[/]");
            table.AddColumn("Name");
            table.AddColumn("Country");
            table.AddColumn("Code");
            table.AddColumn("Lat");
            table.AddColumn("Lon");
            table.AddColumn("Timezone");
            table.AddColumn("Elevation");
            table.AddColumn("Temp");
            table.AddColumn("Weather");

            foreach (var city in cities.OrderBy(c => c.Name))
            {
                weatherByCity.TryGetValue(city.Id.Value.ToString(), out var weather);
                var condition = weather is not null
                    ? Enumeration.GetAll<WeatherConditionCode>().FirstOrDefault(c => c.Id == weather.WeatherCode)
                    : null;

                table.AddRow(
                    Markup.Escape(city.Name),
                    Markup.Escape(city.Country),
                    Markup.Escape(city.CountryCode),
                    city.Location?.Latitude.ToString("F4") ?? "-",
                    city.Location?.Longitude.ToString("F4") ?? "-",
                    Markup.Escape(city.TimeZone ?? "-"),
                    city.Elevation?.ToString() ?? "-",
                    weather is not null ? $"{weather.Temperature}°C" : "-",
                    condition is not null ? condition.Value : (weather?.WeatherCode.ToString() ?? "-"));
            }

            console.Write(table);
        }
        else
        {
            var table = new Table().Border(TableBorder.Rounded).Title("[bold]Cities[/]");
            table.AddColumn("Name");
            table.AddColumn("Country");
            table.AddColumn("Temp");
            table.AddColumn("Weather");

            foreach (var city in cities.OrderBy(c => c.Name))
            {
                weatherByCity.TryGetValue(city.Id.Value.ToString(), out var weather);
                var condition = weather is not null
                    ? Enumeration.GetAll<WeatherConditionCode>().FirstOrDefault(c => c.Id == weather.WeatherCode)
                    : null;

                table.AddRow(
                    Markup.Escape(city.Name),
                    Markup.Escape(city.Country),
                    weather is not null ? $"{weather.Temperature}°C" : "-",
                    condition is not null ? condition.Value : (weather?.WeatherCode.ToString() ?? "-"));
            }

            console.Write(table);
        }

        // Temperature comparison bar chart
        var citiesWithWeather = cities
            .OrderBy(c => c.Name)
            .Select(c => (City: c, Weather: weatherByCity.TryGetValue(c.Id.Value.ToString(), out var w) ? w : null))
            .Where(x => x.Weather is not null)
            .ToList();

        if (citiesWithWeather.Count > 0)
        {
            var colors = new[] { Color.Blue, Color.Green, Color.Yellow1, Color.Red, Color.Orange1, Color.Purple, Color.Cyan, Color.Magenta };
            var chart = new BarChart()
                .Width(60)
                .Label("[bold]City[/]")
                //.ValueTagAlignment(Justify.Right)
                .ShowValues();

            for (var i = 0; i < citiesWithWeather.Count; i++)
            {
                var (city, weather) = citiesWithWeather[i];
                chart.AddItem(
                    city.Name,
                    (double)weather.Temperature,
                    colors[i % colors.Length]);
            }

            console.Write(chart);
        }

        console.MarkupLine($"[grey]Total: {cities.Count} cities[/]");
    }
}
