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
/// Console command that shows current weather for a specific city.
/// </summary>
public class CityCurrentConsoleCommand : ConsoleCommandBase
{
    /// <summary>Gets or sets the city name to look up.</summary>
    [ConsoleCommandArgument(0, Description = "City name to get weather for", Required = true)]
    public string CityName { get; set; }

    /// <summary>Gets or sets the output format.</summary>
    [ConsoleCommandOption("format", Alias = "f", Description = "Output format: table (default) or json")]
    public string Format { get; set; } = "table";

    /// <summary>Initializes a new instance of the <see cref="CityCurrentConsoleCommand"/> class.</summary>
    public CityCurrentConsoleCommand() : base("city-current", "Get current weather for a city", "cc") { }

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
        var weatherResult = await CurrentWeather.FindAllAsync(cw => cw.CityId == city.Id, null, CancellationToken.None);

        if (weatherResult.IsFailure || !weatherResult.Value.Any())
        {
            console.MarkupLine($"[yellow]No current weather data for {Markup.Escape(city.Name)}[/]");
            return;
        }

        var weather = weatherResult.Value.First();
        var condition = Enumeration.GetAll<WeatherConditionCode>().FirstOrDefault(c => c.Id == weather.WeatherCode);

        var panel = new Panel($"[bold]{Markup.Escape(city.Name)}, {Markup.Escape(city.Country)}[/]\n\n" +
            $"  [bold]Temperature:[/]     {weather.Temperature}°C (feels {weather.ApparentTemperature}°C)\n" +
            $"  [bold]Conditions:[/]      {condition?.Value ?? weather.WeatherCode.ToString()}\n" +
            $"  [bold]Humidity:[/]        {weather.Humidity}%\n" +
            $"  [bold]Wind:[/]            {weather.WindSpeed} km/h from {weather.WindDirection}° (gusts {weather.WindGusts} km/h)\n" +
            $"  [bold]Pressure:[/]        {weather.Pressure} hPa\n" +
            $"  [bold]Cloud Cover:[/]     {weather.CloudCover}%\n" +
            $"  [bold]Precipitation:[/]   {weather.Precipitation} mm\n" +
            $"  [bold]Retrieved:[/]       {weather.RetrievedAt:yyyy-MM-dd HH:mm} UTC")
        {
            Border = BoxBorder.Rounded,
            Header = new PanelHeader("[bold blue]Current Weather[/]")
        };

        console.Write(panel);
    }
}
