namespace BridgingIT.DevKit.Common;

using System.Text;

/// <summary>
/// Provides stable display names and runtime identifiers for local DevKit hosts.
/// </summary>
/// <example>
/// <code>
/// var runtimeId = HostRuntimeNaming.CreateRuntimeId("BridgingIT.DevKit.Examples.WeatherFiesta.Presentation.Web.Server", 1234);
/// </code>
/// </example>
public static class HostRuntimeNaming
{
    /// <summary>
    /// Creates a concise runtime id for a host process.
    /// </summary>
    /// <param name="applicationName">The application or assembly name.</param>
    /// <param name="processId">The host process id.</param>
    /// <returns>The runtime id.</returns>
    public static string CreateRuntimeId(string applicationName, int processId)
        => $"{Sanitize(GetDisplayApplicationName(applicationName))}-{processId}";

    /// <summary>
    /// Gets a concise display name for a host application name.
    /// </summary>
    /// <param name="applicationName">The application or assembly name.</param>
    /// <returns>The display name.</returns>
    public static string GetDisplayApplicationName(string applicationName)
    {
        if (string.IsNullOrWhiteSpace(applicationName))
        {
            return "-";
        }

        var parts = applicationName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return applicationName;
        }

        var presentationIndex = Array.FindIndex(parts, part => string.Equals(part, "Presentation", StringComparison.OrdinalIgnoreCase));
        if (presentationIndex > 0)
        {
            return parts[presentationIndex - 1];
        }

        var examplesIndex = Array.FindIndex(parts, part => string.Equals(part, "Examples", StringComparison.OrdinalIgnoreCase));
        if (examplesIndex >= 0 && examplesIndex + 1 < parts.Length)
        {
            return parts[examplesIndex + 1];
        }

        return parts[^1];
    }

    /// <summary>
    /// Sanitizes a value for use in runtime ids and local endpoint names.
    /// </summary>
    /// <param name="value">The value to sanitize.</param>
    /// <returns>The sanitized value.</returns>
    public static string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "host";
        }

        var builder = new StringBuilder(value.Length);
        var previousWasSeparator = false;
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                previousWasSeparator = false;
                continue;
            }

            if (!previousWasSeparator)
            {
                builder.Append('-');
                previousWasSeparator = true;
            }
        }

        var result = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(result) ? "host" : result;
    }
}