namespace BridgingIT.DevKit.Common;

using System.Text.Json;

/// <summary>
/// Reads bounded MCP operation arguments from JSON.
/// </summary>
/// <example>
/// <code>
/// var take = McpArgumentReader.GetInt32(arguments, "take", 50);
/// var id = McpArgumentReader.GetGuid(arguments, "id");
/// </code>
/// </example>
public static class McpArgumentReader
{
    /// <summary>
    /// Gets a string argument.
    /// </summary>
    public static string GetString(JsonElement arguments, string name, string defaultValue = null)
        => TryGetProperty(arguments, name, out var property) && property.ValueKind != JsonValueKind.Null
            ? property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString()
            : defaultValue;

    /// <summary>
    /// Gets an integer argument.
    /// </summary>
    public static int? GetInt32(JsonElement arguments, string name, int? defaultValue = null)
    {
        if (!TryGetProperty(arguments, name, out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return defaultValue;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value))
        {
            return value;
        }

        return int.TryParse(property.ToString(), out value) ? value : defaultValue;
    }

    /// <summary>
    /// Gets a long argument.
    /// </summary>
    public static long? GetInt64(JsonElement arguments, string name, long? defaultValue = null)
    {
        if (!TryGetProperty(arguments, name, out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return defaultValue;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var value))
        {
            return value;
        }

        return long.TryParse(property.ToString(), out value) ? value : defaultValue;
    }

    /// <summary>
    /// Gets a boolean argument.
    /// </summary>
    public static bool? GetBoolean(JsonElement arguments, string name, bool? defaultValue = null)
    {
        if (!TryGetProperty(arguments, name, out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return defaultValue;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(property.GetString(), out var value) => value,
            _ => defaultValue
        };
    }

    /// <summary>
    /// Gets a date-time argument.
    /// </summary>
    public static DateTimeOffset? GetDateTimeOffset(JsonElement arguments, string name, DateTimeOffset? defaultValue = null)
        => TryGetProperty(arguments, name, out var property) &&
            property.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(property.GetString(), out var value)
                ? value
                : defaultValue;

    /// <summary>
    /// Gets a GUID argument.
    /// </summary>
    public static Guid? GetGuid(JsonElement arguments, string name, Guid? defaultValue = null)
        => TryGetProperty(arguments, name, out var property) &&
            property.ValueKind == JsonValueKind.String &&
            Guid.TryParse(property.GetString(), out var value)
                ? value
                : defaultValue;

    /// <summary>
    /// Gets a string array argument.
    /// </summary>
    public static IReadOnlyList<string> GetStringArray(JsonElement arguments, string name)
    {
        if (!TryGetProperty(arguments, name, out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return [];
        }

        if (property.ValueKind == JsonValueKind.Array)
        {
            return property.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray();
        }

        var value = property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();

        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>
    /// Gets an enum argument.
    /// </summary>
    public static TEnum? GetEnum<TEnum>(JsonElement arguments, string name)
        where TEnum : struct
    {
        var value = GetString(arguments, name);

        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var result) ? result : null;
    }

    /// <summary>
    /// Gets an enum array argument.
    /// </summary>
    public static IReadOnlyList<TEnum> GetEnumArray<TEnum>(JsonElement arguments, string name)
        where TEnum : struct
        => GetStringArray(arguments, name)
            .Select(value => Enum.TryParse<TEnum>(value, ignoreCase: true, out var result) ? (TEnum?)result : null)
            .Where(value => value.HasValue)
            .Select(value => value.Value)
            .ToArray();

    /// <summary>
    /// Gets an object argument as a dictionary.
    /// </summary>
    public static IDictionary<string, object> GetObjectDictionary(JsonElement arguments, string name)
    {
        if (!TryGetProperty(arguments, name, out var property) || property.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return JsonSerializer.Deserialize<Dictionary<string, object>>(property.GetRawText());
    }

    /// <summary>
    /// Gets a JSON object argument.
    /// </summary>
    public static JsonElement GetObject(JsonElement arguments, string name)
        => TryGetProperty(arguments, name, out var property) && property.ValueKind == JsonValueKind.Object
            ? property
            : JsonDocument.Parse("{}").RootElement;

    private static bool TryGetProperty(JsonElement arguments, string name, out JsonElement property)
    {
        property = default;

        if (arguments.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (arguments.TryGetProperty(name, out property))
        {
            return true;
        }

        foreach (var item in arguments.EnumerateObject())
        {
            if (string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                property = item.Value;
                return true;
            }
        }

        return false;
    }
}
