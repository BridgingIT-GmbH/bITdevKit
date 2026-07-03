namespace BridgingIT.DevKit.Cli;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Provides shared JSON serializer options for CLI output and descriptor files.
/// </summary>
public static class CliJson
{
    /// <summary>
    /// Gets the shared JSON serializer options.
    /// </summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };
}
