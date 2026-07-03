namespace BridgingIT.DevKit.Cli;

using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using BridgingIT.DevKit.Common;

/// <summary>
/// Discovers and validates host runtime descriptors.
/// </summary>
/// <param name="options">The host registry options.</param>
public sealed class HostRuntimeDiscovery(HostRegistryOptions options)
{
    /// <summary>
    /// Discovers host descriptors for a workspace.
    /// </summary>
    /// <param name="workspace">The workspace context.</param>
    /// <param name="includeAll">A value indicating whether descriptors outside the workspace should be included.</param>
    /// <param name="featureName">The optional required feature endpoint name.</param>
    /// <returns>The discovered hosts.</returns>
    public IReadOnlyList<HostRuntimeInfo> Discover(CliWorkspaceContext workspace, bool includeAll = false, string featureName = null)
    {
        if (!Directory.Exists(options.RuntimePath))
        {
            return [];
        }

        var hosts = new List<HostRuntimeInfo>();
        foreach (var descriptorPath in Directory.EnumerateFiles(options.RuntimePath, "*.json"))
        {
            hosts.Add(Read(descriptorPath, featureName));
        }

        return hosts
            .Where(host => includeAll || host.MatchesWorkspace(workspace))
            .Where(host => string.IsNullOrWhiteSpace(featureName) || HasFeature(host, featureName))
            .OrderBy(host => host.Descriptor?.ApplicationName)
            .ThenBy(host => host.Descriptor?.RuntimeId)
            .ToArray();
    }

    /// <summary>
    /// Reads and validates a single descriptor file.
    /// </summary>
    /// <param name="descriptorPath">The descriptor file path.</param>
    /// <returns>The discovered host information.</returns>
    public HostRuntimeInfo Read(string descriptorPath)
        => Read(descriptorPath, null);

    private static HostRuntimeInfo Read(string descriptorPath, string featureName)
    {
        try
        {
            var json = File.ReadAllText(descriptorPath);
            var descriptor = JsonSerializer.Deserialize<HostRuntimeDescriptor>(json, CliJson.Options);
            if (descriptor is null)
            {
                return Invalid(descriptorPath, "Descriptor JSON is empty.");
            }

            return Validate(descriptorPath, descriptor, featureName);
        }
        catch (JsonException exception)
        {
            return Invalid(descriptorPath, exception.Message);
        }
        catch (IOException exception)
        {
            return Invalid(descriptorPath, exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            return Invalid(descriptorPath, exception.Message);
        }
    }

    private static HostRuntimeInfo Validate(string descriptorPath, HostRuntimeDescriptor descriptor, string featureName)
    {
        if (descriptor.SchemaVersion != HostRuntimeDescriptorSchema.CurrentVersion)
        {
            return new HostRuntimeInfo { DescriptorPath = descriptorPath, Descriptor = descriptor, Status = HostRuntimeStatus.VersionMismatch, Reason = "Unsupported descriptor schema version." };
        }

        if (string.IsNullOrWhiteSpace(descriptor.RuntimeId) ||
            string.IsNullOrWhiteSpace(descriptor.ApplicationName) ||
            string.IsNullOrWhiteSpace(descriptor.EnvironmentName) ||
            string.IsNullOrWhiteSpace(descriptor.WorkspacePath) ||
            string.IsNullOrWhiteSpace(descriptor.ContentRootPath) ||
            string.IsNullOrWhiteSpace(descriptor.Assembly?.Name) ||
            string.IsNullOrWhiteSpace(descriptor.Assembly?.Version) ||
            descriptor.ProcessId <= 0 ||
            descriptor.StartedAt == default)
        {
            return new HostRuntimeInfo { DescriptorPath = descriptorPath, Descriptor = descriptor, Status = HostRuntimeStatus.Invalid, Reason = "Required descriptor fields are missing." };
        }

        if (!IsProcessRunning(descriptor.ProcessId))
        {
            return new HostRuntimeInfo { DescriptorPath = descriptorPath, Descriptor = descriptor, Status = HostRuntimeStatus.Stale, Reason = "Host process is not running." };
        }

        if (!string.IsNullOrWhiteSpace(featureName))
        {
            return ValidateFeatureEndpoint(descriptorPath, descriptor, featureName);
        }

        return new HostRuntimeInfo { DescriptorPath = descriptorPath, Descriptor = descriptor, Status = HostRuntimeStatus.Ready };
    }

    private static HostRuntimeInfo ValidateFeatureEndpoint(string descriptorPath, HostRuntimeDescriptor descriptor, string featureName)
    {
        if (descriptor.Features?.TryGetValue(featureName, out var endpoint) != true)
        {
            return new HostRuntimeInfo { DescriptorPath = descriptorPath, Descriptor = descriptor, Status = HostRuntimeStatus.FeatureUnavailable, Reason = $"Feature endpoint '{featureName}' is not advertised." };
        }

        if (endpoint.ProtocolVersion != 1)
        {
            return new HostRuntimeInfo { DescriptorPath = descriptorPath, Descriptor = descriptor, Status = HostRuntimeStatus.VersionMismatch, Reason = $"Feature endpoint '{featureName}' protocol version is unsupported." };
        }

        if (string.IsNullOrWhiteSpace(endpoint.Transport) ||
            string.IsNullOrWhiteSpace(endpoint.Endpoint) ||
            string.IsNullOrWhiteSpace(endpoint.Nonce))
        {
            return new HostRuntimeInfo { DescriptorPath = descriptorPath, Descriptor = descriptor, Status = HostRuntimeStatus.FeatureUnavailable, Reason = $"Feature endpoint '{featureName}' metadata is incomplete." };
        }

        return new HostRuntimeInfo { DescriptorPath = descriptorPath, Descriptor = descriptor, Status = HostRuntimeStatus.Ready };
    }

    private static bool HasFeature(HostRuntimeInfo host, string featureName)
        => host.Descriptor?.Features?.ContainsKey(featureName) == true;

    private static bool IsProcessRunning(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    private static HostRuntimeInfo Invalid(string descriptorPath, string reason)
        => new() { DescriptorPath = descriptorPath, Status = HostRuntimeStatus.Invalid, Reason = reason };
}
