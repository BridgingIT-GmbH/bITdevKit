namespace BridgingIT.DevKit.Presentation.Web;

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using BridgingIT.DevKit.Common;

/// <summary>
/// Maintains stable local IPC endpoint metadata for the current host process.
/// </summary>
/// <example>
/// <code>
/// var endpoint = state.GetOrCreate("consoleCommands");
/// </code>
/// </example>
public sealed class LocalIpcEndpointState(HostDescriptorOptions options)
{
    private readonly Dictionary<string, HostFeatureEndpointMetadata> endpoints = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or creates endpoint metadata for a feature.
    /// </summary>
    /// <param name="featureName">The feature name.</param>
    /// <returns>The endpoint metadata.</returns>
    public HostFeatureEndpointMetadata GetOrCreate(string featureName)
    {
        if (this.endpoints.TryGetValue(featureName, out var endpoint))
        {
            return endpoint;
        }

        var transport = string.Equals(featureName, "mcp", StringComparison.OrdinalIgnoreCase) &&
            !RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "unix-socket"
                : "named-pipe";

        endpoint = new HostFeatureEndpointMetadata
        {
            ProtocolVersion = 1,
            Transport = transport,
            Endpoint = transport == "unix-socket"
                ? GetUnixSocketPath(options, featureName)
                : $"bdk-{Sanitize(options.RuntimeId)}-{Sanitize(featureName)}",
            Nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant()
        };

        this.endpoints[featureName] = endpoint;

        return endpoint;
    }

    private static string Sanitize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-');
        }

        return builder.ToString().Trim('-');
    }

    private static string GetUnixSocketPath(HostDescriptorOptions options, string featureName)
    {
        var directory = GetUnixSocketDirectory();
        Directory.CreateDirectory(directory);

        var key = $"{options.RegistryPath}|{options.RuntimeId}|{featureName}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant()[..16];

        return Path.Combine(directory, $"bdk-{hash}.sock");
    }

    private static string GetUnixSocketDirectory()
    {
        var candidates = new[]
            {
                Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR"),
                Path.GetTempPath(),
                "/tmp"
            }
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Distinct(StringComparer.Ordinal);

        foreach (var candidate in candidates)
        {
            var directory = Path.Combine(candidate, "bdk-ipc");
            if (directory.Length <= 72)
            {
                return directory;
            }
        }

        return Path.Combine(Path.GetTempPath(), "bdk-ipc");
    }
}
