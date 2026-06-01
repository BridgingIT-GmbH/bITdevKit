// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Application.Orchestrations;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

public class OrchestrationDefinitionsConsoleCommand : OrchestrationConsoleCommandBase
{
    public OrchestrationDefinitionsConsoleCommand()
        : base("definitions", "List registered orchestration definitions", "defs") { }

    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        var registrations = this.GetRequired<OrchestrationRegistrationStore>(console, services);
        if (registrations is null)
        {
            return Task.CompletedTask;
        }

        using var scope = services.CreateScope();
        var names = registrations.GetRegisteredTypes()
            .Select(type => new
            {
                Type = type,
                Name = type.GetProperty("Name")?.GetValue(scope.ServiceProvider.GetRequiredService(type)) as string
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .Select(item =>
            {
                registrations.RegisterName(item.Name, item.Type);
                return item.Name;
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (names.Length == 0)
        {
            console.MarkupLine("[yellow]No orchestration definitions registered[/]");
            return Task.CompletedTask;
        }

        console.Write(OrchestrationTableBuilders.BuildDefinitionTable(names));
        return Task.CompletedTask;
    }
}
