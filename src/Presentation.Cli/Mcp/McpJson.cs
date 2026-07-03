namespace BridgingIT.DevKit.Cli;

using System.Text.Json;

/// <summary>
/// Provides JSON helpers for MCP tool argument handling.
/// </summary>
/// <example>
/// <code>
/// var query = McpJson.GetString(arguments, "query");
/// </code>
/// </example>
internal static class McpJson
{
    /// <summary>
    /// Gets an empty JSON object element.
    /// </summary>
    /// <returns>The empty object.</returns>
    public static JsonElement EmptyObject()
        => JsonDocument.Parse("{}").RootElement.Clone();

    /// <summary>
    /// Reads a string property.
    /// </summary>
    /// <param name="element">The JSON element.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The string value.</returns>
    public static string GetString(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(name, out var property) &&
            property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;

    /// <summary>
    /// Reads a boolean property.
    /// </summary>
    /// <param name="element">The JSON element.</param>
    /// <param name="name">The property name.</param>
    /// <param name="defaultValue">The default value.</param>
    /// <returns>The boolean value.</returns>
    public static bool GetBoolean(JsonElement element, string name, bool defaultValue = false)
        => element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(name, out var property) &&
            property.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? property.GetBoolean()
                : defaultValue;

    /// <summary>
    /// Reads an integer property.
    /// </summary>
    /// <param name="element">The JSON element.</param>
    /// <param name="name">The property name.</param>
    /// <param name="defaultValue">The default value.</param>
    /// <param name="min">The minimum accepted value.</param>
    /// <param name="max">The maximum accepted value.</param>
    /// <returns>The bounded integer value.</returns>
    public static int GetInt32(JsonElement element, string name, int defaultValue, int min, int max)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(name, out var property) ||
            property.ValueKind != JsonValueKind.Number ||
            !property.TryGetInt32(out var value))
        {
            return defaultValue;
        }

        return Math.Clamp(value, min, max);
    }

    /// <summary>
    /// Clones an object property.
    /// </summary>
    /// <param name="element">The JSON element.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The property value or an empty object.</returns>
    public static JsonElement GetObject(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(name, out var property) &&
            property.ValueKind == JsonValueKind.Object
                ? property.Clone()
                : EmptyObject();
}
