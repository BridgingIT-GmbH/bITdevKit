// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core.ConsoleCommands;

using BridgingIT.DevKit.Application;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core;
using BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core.Abstractions;
using BridgingIT.DevKit.Examples.WeatherFiesta.Application.Modules.Core.Models;
using BridgingIT.DevKit.Presentation;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

/// <summary>
/// Console command to create a city by name using geocoding lookup.
/// </summary>
public class CityCreateConsoleCommand : ConsoleCommandBase, IGroupedConsoleCommand
{
    /// <summary>Gets or sets the city name to search for.</summary>
    [ConsoleCommandArgument(0, Description = "City name to create", Required = true)]
    public new string Name { get; set; }

    /// <summary>Gets or sets the ISO country code to filter geocoding results.</summary>
    [ConsoleCommandOption("code", Alias = "c", Description = "ISO country code to filter results (e.g. NL, DE, FR)")]
    public string CountryCode { get; set; }

    /// <inheritdoc />
    public string GroupName => "city";

    /// <inheritdoc />
    public IReadOnlyCollection<string> GroupAliases => ["cities"];

    /// <summary>Initializes a new instance of the <see cref="CityCreateConsoleCommand"/> class.</summary>
    public CityCreateConsoleCommand() : base("create", "Create a city by name using geocoding lookup", "add") { }

    /// <inheritdoc />
    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        var geocodingClient = services.GetRequiredService<IWeatherGeocodingClient>();

        // Step 1: Geocode the city name
        var searchResult = await geocodingClient.SearchCitiesAsync(this.Name, this.CountryCode, CancellationToken.None);

        if (searchResult?.Results is null || searchResult.Results.Count == 0)
        {
            console.MarkupLine($"[red]No geocoding results found for '{Markup.Escape(this.Name)}'.[/]");
            return;
        }

        GeocodingResultModel geocodingResult;

        if (searchResult.Results.Count == 1)
        {
            geocodingResult = searchResult.Results[0];
        }
        else
        {
            // Multiple results — show table and let user pick
            console.MarkupLine($"[yellow]Found {searchResult.Results.Count} cities matching '{Markup.Escape(this.Name)}'. Please select one:[/]");

            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("#");
            table.AddColumn("Name");
            table.AddColumn("Country");
            table.AddColumn("Code");
            table.AddColumn("Lat");
            table.AddColumn("Lon");
            table.AddColumn("Timezone");

            for (var i = 0; i < searchResult.Results.Count; i++)
            {
                var r = searchResult.Results[i];
                table.AddRow(
                    (i + 1).ToString(),
                    Markup.Escape(r.Name ?? "-"),
                    Markup.Escape(r.Country ?? "-"),
                    Markup.Escape(r.CountryCode ?? "-"),
                    r.Latitude.ToString("F4"),
                    r.Longitude.ToString("F4"),
                    Markup.Escape(r.TimeZone ?? "-"));
            }

            console.Write(table);

            var selection = console.Prompt(new TextPrompt<int>("[bold]Enter the number[/]:")
                .Validate(n => n >= 1 && n <= searchResult.Results.Count
                    ? ValidationResult.Success()
                    : ValidationResult.Error($"[red]Please enter a number between 1 and {searchResult.Results.Count}[/]")));

            geocodingResult = searchResult.Results[selection - 1];
        }

        // Step 2: Show what we found
        console.MarkupLine("[bold]Creating city:[/]");
        console.MarkupLine($"  Name:       {Markup.Escape(geocodingResult.Name)}");
        console.MarkupLine($"  Country:    {Markup.Escape(geocodingResult.Country)} ({Markup.Escape(geocodingResult.CountryCode)})");
        console.MarkupLine($"  Timezone:   {Markup.Escape(geocodingResult.TimeZone)}");
        console.MarkupLine($"  Location:   {geocodingResult.Latitude}, {geocodingResult.Longitude}");
        if (geocodingResult.Elevation.HasValue)
        {
            console.MarkupLine($"  Elevation:  {geocodingResult.Elevation}m");
        }

        if (geocodingResult.ExternalId.HasValue)
        {
            console.MarkupLine($"  ExternalId: {geocodingResult.ExternalId}");
        }

        // Step 3: Create via AdminCityCreateCommand
        var requester = services.GetRequiredService<IRequester>();

        var model = new AdminCityCreateModel
        {
            Name = geocodingResult.Name,
            Country = geocodingResult.Country,
            CountryCode = geocodingResult.CountryCode,
            TimeZone = geocodingResult.TimeZone,
            Latitude = geocodingResult.Latitude,
            Longitude = geocodingResult.Longitude,
            Elevation = geocodingResult.Elevation,
            ExternalId = geocodingResult.ExternalId
        };

        var command = new AdminCityCreateCommand { Model = model };
        var result = await requester.SendAsync(command);

        if (result.IsFailure)
        {
            console.MarkupLine($"[red]Failed to create city: {Markup.Escape(string.Join(", ", result.Errors.Select(e => e.Message)))}[/]");
            return;
        }

        console.MarkupLine($"[green]City '{Markup.Escape(geocodingResult.Name)}' created successfully![/]");
    }
}
