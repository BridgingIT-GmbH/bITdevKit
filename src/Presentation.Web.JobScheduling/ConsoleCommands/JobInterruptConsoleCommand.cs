// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Spectre.Console;
using System;

public class JobInterruptConsoleCommand : JobGroupConsoleCommandBase
{
    [ConsoleCommandArgument(0, Description = "Job name", Required = true)]
    public string JobName { get; set; }

    [ConsoleCommandArgument(1, Description = "Job group", Required = false)]
    public string JobGroup { get; set; }

    public JobInterruptConsoleCommand() : base("interrupt", "Interrupt running job", "stop") { }

    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        if (string.IsNullOrWhiteSpace(this.JobName))
        {
            console.MarkupLine("[red]Job name is required[/]");

            return;
        }

        await this.ExecuteWithJobServiceAsync(console, services, async jobService =>
        {
            var jobGroup = this.NormalizeJobGroup(this.JobGroup);
            await jobService.InterruptJobAsync(this.JobName, jobGroup);

            console.MarkupLine($"Job '[bold]{this.JobName}[/]' interrupted");
        });
    }
}
