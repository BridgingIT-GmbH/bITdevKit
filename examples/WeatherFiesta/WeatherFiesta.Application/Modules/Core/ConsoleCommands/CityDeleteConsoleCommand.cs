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
using BridgingIT.DevKit.Presentation;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

/// <summary>
/// Console command to delete a city by name (with confirmation).
/// </summary>
public class CityDeleteConsoleCommand : ConsoleCommandBase, IGroupedConsoleCommand
{
    /// <summary>Gets or sets the city name to delete.</summary>
    [ConsoleCommandArgument(0, Description = "City name to delete", Required = true)]
    public string CityName { get; set; }

    /// <summary>Gets or sets a value indicating whether to skip the confirmation prompt.</summary>
    [ConsoleCommandOption("force", Alias = "f", Description = "Skip confirmation prompt")]
    public bool Force { get; set; }

    /// <inheritdoc />
    public string GroupName => "city";

    /// <inheritdoc />
    public IReadOnlyCollection<string> GroupAliases => ["cities"];

    /// <summary>Initializes a new instance of the <see cref="CityDeleteConsoleCommand"/> class.</summary>
    public CityDeleteConsoleCommand() : base("delete", "Delete a city and all its weather data", "remove", "rm") { }

    /// <inheritdoc />
    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        // Resolve city name to ID
        var cityId = await this.ResolveCityIdAsync(console);
        if (cityId is null)
        {
            return;
        }

        if (!this.Force)
        {
            if (!console.Confirm($"[bold red]Delete city '{Markup.Escape(this.CityName)}' and all associated weather data?[/]"))
            {
                console.MarkupLine("[yellow]Cancelled.[/]");
                return;
            }
        }

        var requester = services.GetRequiredService<IRequester>();
        var command = new AdminCityDeleteCommand(cityId.Value.ToString());
        var result = await requester.SendAsync(command);

        if (result.IsFailure)
        {
            console.MarkupLine($"[red]Failed to delete city: {Markup.Escape(string.Join(", ", result.Errors.Select(e => e.Message)))}[/]");
            return;
        }

        console.MarkupLine($"[green]City '{Markup.Escape(this.CityName)}' deleted successfully.[/]");
    }

    private async Task<CityId> ResolveCityIdAsync(IAnsiConsole console)
    {
        var cityResult = await City.FindAllAsync(
            c => c.AuditState.Deleted != true && c.Name.Contains(this.CityName),
            null,
            CancellationToken.None);

        if (cityResult.IsFailure)
        {
            console.MarkupLine($"[red]Error searching cities: {Markup.Escape(string.Join(", ", cityResult.Errors.Select(e => e.Message)))}[/]");
            return null;
        }

        var cities = cityResult.Value.ToList();
        if (cities.Count == 0)
        {
            console.MarkupLine($"[yellow]No city found matching '{Markup.Escape(this.CityName)}'[/]");
            return null;
        }

        if (cities.Count > 1)
        {
            console.MarkupLine($"[yellow]Multiple cities match '{Markup.Escape(this.CityName)}'. Please be more specific:[/]");
            foreach (var c in cities)
            {
                console.MarkupLine($"  - {Markup.Escape(c.Name)}, {Markup.Escape(c.Country)} ({Markup.Escape(c.CountryCode)}) [grey]id={c.Id}[/]");
            }

            return null;
        }

        return cities[0].Id;
    }
}
