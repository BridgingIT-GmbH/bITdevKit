// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Spectre.Console;
using System;

/// <summary>
/// Represents job resume console command.
/// </summary>
public class JobResumeConsoleCommand : JobGroupConsoleCommandBase
{
    /// <summary>
    /// Gets or sets the job name.
    /// </summary>
    [ConsoleCommandArgument(0, Description = "Job name", Required = true)]
    public string JobName { get; set; }

    /// <summary>
    /// Gets or sets the job group.
    /// </summary>
    [ConsoleCommandArgument(1, Description = "Job group", Required = false)]
    public string JobGroup { get; set; }

    /// <summary>
    /// Initializes a new instance of the <c>JobResumeConsoleCommand</c> class.
    /// </summary>
    public JobResumeConsoleCommand() : base("resume", "Resume paused job") { }

    /// <inheritdoc/>
    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(this.JobName))
        {
            console.MarkupLine("[red]Job name is required[/]");

            return;
        }

        await this.ExecuteWithJobServiceAsync(console, services, async jobService =>
        {
            var jobGroup = this.NormalizeJobGroup(this.JobGroup);
            await jobService.ResumeJobAsync(this.JobName, jobGroup);

            console.MarkupLine($"Job '[bold]{this.JobName}[/]' resumed");
        });
    }
}
