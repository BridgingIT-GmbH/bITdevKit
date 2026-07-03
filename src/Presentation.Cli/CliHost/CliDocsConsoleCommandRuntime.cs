namespace BridgingIT.DevKit.Cli;

using BridgingIT.DevKit.Presentation;
using Spectre.Console;

/// <summary>
/// Adapts the shared docs console command to CLI output and exit-code behavior.
/// </summary>
/// <param name="context">The CLI runtime context.</param>
/// <param name="console">The CLI console.</param>
/// <param name="state">The CLI execution state.</param>
public sealed class CliDocsConsoleCommandRuntime(CliRuntimeContext context, CliConsole console, CliExecutionState state) : IDocsConsoleCommandRuntime
{
    /// <inheritdoc />
    public bool CanOpenBrowser => !context.Output.IsJson && !context.Output.IsCi;

    /// <inheritdoc />
    public bool TryWriteResult(DocsConsoleCommandResult result)
    {
        if (!context.Output.IsJson)
        {
            return false;
        }

        console.WriteJson(result);
        return true;
    }

    /// <inheritdoc />
    public void Fail(IAnsiConsole ansiConsole, string message)
    {
        state.ExitCode = CliExitCode.CommandFailed;
        console.Error(message, "command_failed", state.ExitCode);
    }
}