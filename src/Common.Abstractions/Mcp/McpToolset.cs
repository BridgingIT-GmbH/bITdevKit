namespace BridgingIT.DevKit.Common;

/// <summary>
/// Defines supported MCP toolset names.
/// </summary>
/// <example>
/// <code>
/// var toolsets = McpToolset.ParseCsv("diagnostics,operations");
/// </code>
/// </example>
public static class McpToolset
{
    /// <summary>
    /// The read-only diagnostics toolset.
    /// </summary>
    public const string Diagnostics = "diagnostics";

    /// <summary>
    /// The operational action toolset.
    /// </summary>
    public const string Operations = "operations";

    /// <summary>
    /// The destructive administrative toolset.
    /// </summary>
    public const string Admin = "admin";

    /// <summary>
    /// Parses comma-separated toolsets.
    /// </summary>
    /// <param name="value">The comma-separated value.</param>
    /// <returns>The parsed unique toolsets.</returns>
    public static IReadOnlySet<string> ParseCsv(string value)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Diagnostics };
        if (string.IsNullOrWhiteSpace(value))
        {
            return result;
        }

        result.Clear();
        foreach (var item in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (IsKnown(item))
            {
                result.Add(item.ToLowerInvariant());
            }
        }

        if (result.Count == 0)
        {
            result.Add(Diagnostics);
        }

        return result;
    }

    /// <summary>
    /// Checks whether the supplied toolset name is supported.
    /// </summary>
    /// <param name="value">The toolset name.</param>
    /// <returns><see langword="true" /> when supported.</returns>
    public static bool IsKnown(string value)
        => string.Equals(value, Diagnostics, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, Operations, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, Admin, StringComparison.OrdinalIgnoreCase);
}
