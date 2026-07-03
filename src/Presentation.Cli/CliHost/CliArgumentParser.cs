namespace BridgingIT.DevKit.Cli;

/// <summary>
/// Parses global CLI options while preserving command-specific tokens.
/// </summary>
public static class CliArgumentParser
{
    /// <summary>
    /// Parses command-line arguments into global options and command arguments.
    /// </summary>
    /// <param name="args">The raw process arguments.</param>
    /// <returns>The parsed command line.</returns>
    public static ParsedCommandLine Parse(string[] args)
    {
        var commandArguments = new List<string>();
        string workspacePath = null;
        var verbose = false;
        var quiet = false;
        var noColor = false;
        var noLogo = false;
        var banner = false;
        var nonInteractive = false;
        var showHelp = false;
        var showVersion = false;
        var outputFormat = CliOutputFormat.Text;
        var forwardingBoundaryReached = false;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];

            if (forwardingBoundaryReached)
            {
                commandArguments.Add(argument);
                continue;
            }

            switch (argument)
            {
                case "--":
                    forwardingBoundaryReached = true;
                    commandArguments.Add(argument);
                    break;
                case "--help":
                case "-h":
                    showHelp = true;
                    break;
                case "--version":
                    showVersion = true;
                    break;
                case "--workspace":
                    if (!TryReadValue(args, ref index, "--workspace", out workspacePath, out var workspaceError))
                    {
                        return Error(workspaceError);
                    }

                    break;
                case "--verbose":
                    verbose = true;
                    break;
                case "--quiet":
                    quiet = true;
                    break;
                case "--no-color":
                    noColor = true;
                    break;
                case "--nologo":
                case "--no-logo":
                    noLogo = true;
                    break;
                case "--banner":
                    banner = true;
                    break;
                case "--non-interactive":
                    nonInteractive = true;
                    break;
                case "--output":
                    if (!TryReadValue(args, ref index, "--output", out var outputValue, out var outputError))
                    {
                        return Error(outputError);
                    }

                    if (!Enum.TryParse<CliOutputFormat>(outputValue, ignoreCase: true, out outputFormat))
                    {
                        return Error("Unsupported output format. Use 'text' or 'json'.");
                    }

                    break;
                default:
                    commandArguments.Add(argument);
                    break;
            }
        }

        if (verbose && quiet)
        {
            return Error("--quiet and --verbose cannot be used together.");
        }

        if (noLogo && banner)
        {
            return Error("--nologo and --banner cannot be used together.");
        }

        if (outputFormat == CliOutputFormat.Json)
        {
            noColor = true;
        }

        return new ParsedCommandLine
        {
            CommandArguments = commandArguments.ToArray(),
            WorkspacePath = workspacePath,
            Verbose = verbose,
            Quiet = quiet,
            NoColor = noColor,
            NoLogo = noLogo,
            Banner = banner,
            NonInteractive = nonInteractive,
            ShowHelp = showHelp,
            ShowVersion = showVersion,
            OutputFormat = outputFormat
        };
    }

    private static bool TryReadValue(string[] args, ref int index, string optionName, out string value, out string error)
    {
        value = null;
        error = null;

        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            error = $"{optionName} requires a value.";
            return false;
        }

        index++;
        value = args[index];
        return true;
    }

    private static ParsedCommandLine Error(string error)
        => new() { Error = error };
}
