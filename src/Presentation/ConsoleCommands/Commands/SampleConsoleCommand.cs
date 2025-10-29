// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Spectre.Console;

public class SampleConsoleCommand : ConsoleCommandBase
{
    [ConsoleCommandOption("name", Description = "Name to use", Required = true)] public string UserName { get; set; }
    [ConsoleCommandOption("count", Alias = "c", Description = "Base count", Default = 1)] public int Count { get; set; }
    [ConsoleCommandOption("enable", Alias = "e", Description = "Enable extra output")] public bool Enable { get; set; }
    [ConsoleCommandOption("guid", Description = "Optional guid")] public Guid GuidValue { get; set; }
    [ConsoleCommandArgument(0, Description = "Greeting target", Required = true)] public string Target { get; set; }
    [ConsoleCommandArgument(1, Description = "Repeat override", Required = false)] public int Repeat { get; set; }

    /// <summary>
    /// Demonstrates option & argument binding plus post-bind adjustments.
    /// Usage: <c>sample --name Bob Alice --count3 --enable</c>
    /// </summary>
    public SampleConsoleCommand() : base("sample", "Demonstration command for binding", "demo") { }
    /// <summary>Adjusts values after binding (applies Count to Repeat when not supplied).</summary>
    public override void OnAfterBind(IAnsiConsole console, string[] tokens)
    {
        // Cross-field adjustment: if Repeat not supplied (0) use Count
        if (this.Repeat <= 0)
        {
            this.Repeat = this.Count;
        }
    }
    /// <summary>Executes sample output logic.</summary>
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        var table = new Table().Border(TableBorder.Minimal);
        table.AddColumn("Field"); table.AddColumn("Value");
        table.AddRow("Name", this.UserName);
        table.AddRow("Target", this.Target);
        table.AddRow("Count", this.Count.ToString());
        table.AddRow("Repeat", this.Repeat.ToString());
        table.AddRow("Enable", this.Enable.ToString());
        table.AddRow("GuidValue", this.GuidValue == Guid.Empty ? "(empty)" : this.GuidValue.ToString());
        console.Write(table);
        if (this.Enable)
        {
            for (var i = 0; i < this.Repeat; i++)
            {
                console.MarkupLine($"[green]{i + 1}. Hello {this.Target} from {this.UserName}![/]");
            }
        }

        return Task.CompletedTask;
    }
}
// === end diagnostic additions ===