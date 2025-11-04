// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

public class EnvConsoleCommand : ConsoleCommandBase
{
    /// <summary>
    /// Outputs ASP.NET hosting environment details: name, application name, content & web root paths.
    /// Usage: <c>env</c>
    /// </summary>
    public EnvConsoleCommand() : base("env", "Show environment info") { }

    /// <summary>Executes environment info output.</summary>
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        var env = services.GetRequiredService<IWebHostEnvironment>();
        var table = new Table().Border(TableBorder.Minimal);
        table.AddColumn("Key"); table.AddColumn("Value");
        table.AddRow("Name", env.EnvironmentName);
        table.AddRow("App", env.ApplicationName);
        table.AddRow("ContentRoot", env.ContentRootPath);
        table.AddRow("WebRoot", env.WebRootPath ?? "(none)");
        console.Write(table);

        return Task.CompletedTask;
    }
}
