// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core.ConsoleCommands;

using BridgingIT.DevKit.Application;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Examples.WeatherFiesta.Domain.Model;
using BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;
using BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core.Abstractions;
using BridgingIT.DevKit.Presentation;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

/// <summary>
/// Console command to reset weather data for a city (delete existing data and re-ingest).
/// </summary>
public class CityResetConsoleCommand : ConsoleCommandBase, IGroupedConsoleCommand
{
    /// <summary>Gets or sets the city name to reset weather data for.</summary>
    [ConsoleCommandArgument(0, Description = "City name to reset weather data for", Required = true)]
    public string CityName { get; set; }

    /// <summary>Gets or sets a value indicating whether to also trigger weather data ingestion after reset.</summary>
    [ConsoleCommandOption("ingest", Alias = "i", Description = "Also trigger weather data ingestion after reset")]
    public bool Ingest { get; set; }

    /// <inheritdoc />
    public string GroupName => "city";

    /// <inheritdoc />
    public IReadOnlyCollection<string> GroupAliases => ["cities"];

    /// <summary>Initializes a new instance of the <see cref="CityResetConsoleCommand"/> class.</summary>
    public CityResetConsoleCommand() : base("reset", "Reset weather data for a city (delete and optionally re-ingest)") { }

    /// <inheritdoc />
    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        // Resolve city name to ID
        var cityResult = await City.FindAllAsync(
            c => !c.AuditState.IsDeleted() && c.Name.Contains(this.CityName),
            null,
            CancellationToken.None);

        if (cityResult.IsFailure)
        {
            console.MarkupLine($"[red]Error searching cities: {Markup.Escape(string.Join(", ", cityResult.Errors.Select(e => e.Message)))}[/]");
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
                console.MarkupLine($"  - {Markup.Escape(c.Name)}, {Markup.Escape(c.Country)} ({Markup.Escape(c.CountryCode)})");
            }

            return;
        }

        var city = cities[0];
        var cityIdStr = city.Id.Value.ToString();

        // Step 1: Reset (delete weather data)
        await console.Status()
            .StartAsync($"Resetting weather data for {city.Name}...", async ctx =>
            {
                var requester = services.GetRequiredService<IRequester>();
                var resetCommand = new AdminCityWeatherResetCommand(cityIdStr);
                var resetResult = await requester.SendAsync(resetCommand);

                if (resetResult.IsFailure)
                {
                    console.MarkupLine($"[red]Failed to reset weather data: {Markup.Escape(string.Join(", ", resetResult.Errors.Select(e => e.Message)))}[/]");
                    return;
                }

                console.MarkupLine($"[green]Weather data for '{Markup.Escape(city.Name)}' reset successfully.[/]");
            });

        // Step 2: Optionally ingest fresh data
        if (this.Ingest)
        {
            await console.Status()
                .StartAsync($"Ingesting fresh weather data for {city.Name}...", async ctx =>
                {
                    var weatherAgent = services.GetRequiredService<IWeatherAgent>();
                    var ingestResult = await weatherAgent.IngestWeatherAsync(
                        cityIdStr,
                        (double)city.Location.Latitude,
                        (double)city.Location.Longitude,
                        CancellationToken.None);

                    if (ingestResult.IsFailure)
                    {
                        console.MarkupLine($"[red]Failed to ingest weather data: {Markup.Escape(string.Join(", ", ingestResult.Errors.Select(e => e.Message)))}[/]");
                        return;
                    }

                    console.MarkupLine($"[green]Fresh weather data ingested for '{Markup.Escape(city.Name)}'.[/]");
                });
        }
        else
        {
            console.MarkupLine("[grey]Use --ingest to also fetch fresh weather data after reset.[/]");
        }
    }
}
