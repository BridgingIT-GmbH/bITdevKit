namespace BridgingIT.DevKit.Cli;

using BridgingIT.DevKit.Common;

/// <summary>
/// Contains options for the <c>bdk mcp</c> STDIO server.
/// </summary>
/// <example>
/// <code>
/// var options = McpCliOptions.Parse(["--toolset", "diagnostics,operations"], out var error);
/// </code>
/// </example>
public sealed class McpCliOptions
{
    /// <summary>
    /// Gets the explicitly selected runtime id.
    /// </summary>
    public string RuntimeId { get; init; }

    /// <summary>
    /// Gets enabled MCP toolsets.
    /// </summary>
    public IReadOnlySet<string> Toolsets { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { McpToolset.Diagnostics };

    /// <summary>
    /// Gets a value indicating whether diagnostic STDERR output is enabled.
    /// </summary>
    public bool Verbose { get; init; }

    /// <summary>
    /// Parses MCP command options.
    /// </summary>
    /// <param name="arguments">The command arguments after <c>mcp</c>.</param>
    /// <param name="error">The parse error.</param>
    /// <returns>The parsed options.</returns>
    public static McpCliOptions Parse(IReadOnlyList<string> arguments, out string error)
    {
        error = null;
        string runtimeId = null;
        IReadOnlySet<string> toolsets = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { McpToolset.Diagnostics };
        var verbose = false;

        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            switch (argument)
            {
                case "--runtime-id":
                case "--host":
                    if (!TryReadValue(arguments, ref index, argument, out runtimeId, out error))
                    {
                        return null;
                    }

                    break;
                case "--toolset":
                case "--toolsets":
                    if (!TryReadValue(arguments, ref index, argument, out var toolsetValue, out error))
                    {
                        return null;
                    }

                    toolsets = McpToolset.ParseCsv(toolsetValue);
                    break;
                case "--verbose":
                    verbose = true;
                    break;
                default:
                    error = $"Unsupported bdk mcp option '{argument}'.";
                    return null;
            }
        }

        return new McpCliOptions
        {
            RuntimeId = runtimeId,
            Toolsets = toolsets,
            Verbose = verbose
        };
    }

    private static bool TryReadValue(IReadOnlyList<string> arguments, ref int index, string optionName, out string value, out string error)
    {
        value = null;
        error = null;
        if (index + 1 >= arguments.Count || arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            error = $"{optionName} requires a value.";
            return false;
        }

        index++;
        value = arguments[index];
        return true;
    }
}
