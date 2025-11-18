// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using System.Collections.Generic;

public class RequesterInfoConsoleCommand : ConsoleCommandBase, IGroupedConsoleCommand
{
    public RequesterInfoConsoleCommand() : base("list", "List registrations") { }

    public string GroupName => "requester";

    public IReadOnlyCollection<string> GroupAliases => ["req"];

    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        // TODO: get scoped IRequester from services
        var requester = services.GetRequiredService<IRequester>();
        if (requester != null)
        {
            var infos = requester.GetRegistrationInformation();
        }
        else
        {
            console.MarkupLine("[red]No requester registered.[/]");
        }

        return Task.CompletedTask;
    }
}

public class NotifierInfoConsoleCommand : ConsoleCommandBase, IGroupedConsoleCommand
{
    public NotifierInfoConsoleCommand() : base("list", "List registrations") { }

    public string GroupName => "notifier";

    public IReadOnlyCollection<string> GroupAliases => ["not"];

    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        // TODO: get scoped IRequester from services
        var notifier = services.GetRequiredService<INotifier>();
        if (notifier != null)
        {
            var infos = notifier.GetRegistrationInformation();
        }
        else
        {
            console.MarkupLine("[red]No notifier registered.[/]");
        }

        return Task.CompletedTask;
    }
}