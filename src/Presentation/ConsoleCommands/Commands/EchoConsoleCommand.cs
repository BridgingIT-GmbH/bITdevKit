// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Spectre.Console;

public class EchoConsoleCommand : ConsoleCommandBase
{
    [ConsoleCommandArgument(0, Description = "Text to echo", Required = true)] public string Text { get; set; }
    [ConsoleCommandOption("upper", Alias = "u", Description = "Uppercase output")] public bool Upper { get; set; }
    [ConsoleCommandOption("repeat", Alias = "r", Description = "Repeat count", Default = 1)] public int Repeat { get; set; }
    [ConsoleCommandOption("color", Alias = "c", Description = "Spectre color (e.g. green, yellow)")] public string Color { get; set; }

    /// <summary>
    /// Echoes provided text with optional repetition, color and case transformation.
    /// Usage: <c>echo "Hello World" --repeat3 --color yellow --upper</c>
    /// </summary>
    public EchoConsoleCommand() : base("echo", "Echo text with optional transformations") { }
    /// <summary>Post-bind adjustments (normalizes repeat & sets default color).</summary>
    public override void OnAfterBind(IAnsiConsole console, string[] tokens)
    {
        if (this.Repeat <= 0)
        {
            this.Repeat = 1;
        }

        if (string.IsNullOrWhiteSpace(this.Color))
        {
            this.Color = "white";
        }
    }
    /// <summary>Outputs the echoed text lines.</summary>
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        var output = this.Upper ? this.Text.ToUpperInvariant() : this.Text;
        for (var i = 1; i <= this.Repeat; i++)
        {
            console.MarkupLine($"[{this.Color}]{i}. {Markup.Escape(output)}[/]");
        }
        return Task.CompletedTask;
    }
}
// === end diagnostic additions ===