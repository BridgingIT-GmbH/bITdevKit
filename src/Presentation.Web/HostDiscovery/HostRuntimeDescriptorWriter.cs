namespace BridgingIT.DevKit.Presentation.Web;

using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Logging;

/// <summary>
/// Writes host runtime descriptors for local CLI discovery.
/// </summary>
/// <example>
/// <code>
/// await writer.WriteAsync(services, cancellationToken);
/// </code>
/// </example>
public sealed class HostRuntimeDescriptorWriter(
    IWebHostEnvironment environment,
    HostDescriptorOptions options,
    IEnumerable<IHostFeatureEndpointContributor> contributors,
    ILogger<HostRuntimeDescriptorWriter> logger)
{
    private const string LogKey = "BDK";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    /// <summary>
    /// Gets the current descriptor file path.
    /// </summary>
    public string DescriptorPath => HostDescriptorPath.GetDescriptorPath(options);

    /// <summary>
    /// Writes the current host descriptor.
    /// </summary>
    /// <param name="services">The application service provider.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task WriteAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(options.RegistryPath);
            var descriptor = this.CreateDescriptor(services);
            var path = this.DescriptorPath;
            var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";

            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(stream, descriptor, SerializerOptions, cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, path, overwrite: true);

            logger.LogDebug(
                "[{LogKey}] host descriptor written (runtimeId={RuntimeId}, path={DescriptorPath}, features={Features})",
                LogKey,
                descriptor.RuntimeId,
                path,
                string.Join(",", descriptor.Features.Keys));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "[{LogKey}] host descriptor write failed (path={DescriptorPath})", LogKey, this.DescriptorPath);
        }
    }

    /// <summary>
    /// Removes the current host descriptor when present.
    /// </summary>
    public void Remove()
    {
        try
        {
            var path = this.DescriptorPath;
            if (!File.Exists(path))
            {
                return;
            }

            File.Delete(path);
            logger.LogDebug("[{LogKey}] host descriptor removed (path={DescriptorPath})", LogKey, path);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "[{LogKey}] host descriptor cleanup skipped (path={DescriptorPath})", LogKey, this.DescriptorPath);
        }
    }

    /// <summary>
    /// Deletes descriptors of hosts that no longer run, and temporary files left by interrupted writes.
    /// </summary>
    /// <remarks>
    /// A descriptor is stale when its process has exited or its process ID now belongs to a process that started
    /// after the descriptor. Descriptors that cannot be read or checked are kept. The current descriptor is never touched.
    /// </remarks>
    /// <returns>The number of deleted files.</returns>
    /// <example>
    /// <code>
    /// var deleted = writer.PruneStale();
    /// </code>
    /// </example>
    public int PruneStale()
    {
        var deleted = 0;
        try
        {
            if (!Directory.Exists(options.RegistryPath))
            {
                return 0;
            }

            var currentPath = Path.GetFullPath(this.DescriptorPath);
            foreach (var path in Directory.EnumerateFiles(options.RegistryPath, "*.json"))
            {
                if (!string.Equals(Path.GetFullPath(path), currentPath, StringComparison.OrdinalIgnoreCase) &&
                    IsStaleDescriptor(path) &&
                    TryDelete(path))
                {
                    deleted++;
                }
            }

            var abandonedBefore = DateTime.UtcNow.AddHours(-1);
            foreach (var path in Directory.EnumerateFiles(options.RegistryPath, "*.tmp"))
            {
                if (File.GetLastWriteTimeUtc(path) < abandonedBefore && TryDelete(path))
                {
                    deleted++;
                }
            }

            if (deleted > 0)
            {
                logger.LogDebug("[{LogKey}] stale host descriptors pruned (count={Count}, path={RegistryPath})", LogKey, deleted, options.RegistryPath);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            logger.LogDebug(ex, "[{LogKey}] stale host descriptor pruning skipped (path={RegistryPath})", LogKey, options.RegistryPath);
        }

        return deleted;
    }

    internal static bool IsStaleDescriptor(string path)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("processId", out var processIdElement) ||
                !processIdElement.TryGetInt32(out var processId) ||
                processId <= 0)
            {
                return false;
            }

            var processStartedAt = root.TryGetProperty("devAppInstance", out var instance) &&
                instance.ValueKind == JsonValueKind.Object &&
                instance.TryGetProperty("processStartedAt", out var startedElement) &&
                startedElement.TryGetDateTimeOffset(out var recordedStart)
                    ? recordedStart
                    : (DateTimeOffset?)null;
            var startedAt = root.TryGetProperty("startedAt", out var startedAtElement) &&
                startedAtElement.TryGetDateTimeOffset(out var descriptorStart)
                    ? descriptorStart
                    : (DateTimeOffset?)null;

            return !IsHostProcessRunning(processId, processStartedAt, startedAt);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsHostProcessRunning(int processId, DateTimeOffset? processStartedAt, DateTimeOffset? startedAt)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            if (process.HasExited)
            {
                return false;
            }

            var processStart = new DateTimeOffset(process.StartTime);
            if (processStartedAt is not null)
            {
                return (processStart - processStartedAt.Value).Duration() < TimeSpan.FromSeconds(1);
            }

            // A process that started after the descriptor reused the host's process ID.
            return startedAt is null || processStart <= startedAt.Value.AddSeconds(1);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            // Unknown liveness keeps the descriptor.
            return true;
        }
    }

    private static bool TryDelete(string path)
    {
        try
        {
            File.Delete(path);

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private HostRuntimeDescriptor CreateDescriptor(IServiceProvider services)
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var fileVersion = FileVersionInfo.GetVersionInfo(assembly.Location);
        var descriptor = new HostRuntimeDescriptor
        {
            RuntimeId = options.RuntimeId,
            ApplicationName = environment.ApplicationName,
            EnvironmentName = environment.EnvironmentName,
            WorkspacePath = options.WorkspacePath,
            ContentRootPath = environment.ContentRootPath,
            ProjectPath = options.ProjectPath,
            ProcessId = Environment.ProcessId,
            StartedAt = options.StartedAt,
            Assembly = new HostRuntimeAssemblyDescriptor
            {
                Name = assembly.GetName().Name,
                Version = assembly.GetName().Version?.ToString(),
                InformationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                FileVersion = fileVersion.FileVersion
            }
        };

        foreach (var contributor in contributors)
        {
            var endpoint = contributor.GetEndpointMetadata(services);
            if (endpoint is not null)
            {
                descriptor.Features[contributor.FeatureName] = endpoint;
            }
        }

        var instance = services.GetService(typeof(DevAppInstanceContext)) as DevAppInstanceContext;
        if (instance is { IsActive: true })
        {
            descriptor.DevAppInstance = new HostRuntimeDevAppInstanceDescriptor
            {
                AppId = instance.AppId,
                WorktreeId = instance.WorktreeId ?? Guid.Empty,
                Slot = instance.Slot ?? 0,
                WorktreeRoot = instance.WorktreeRoot,
                HttpUrl = instance.HttpUrl,
                HttpsUrl = instance.HttpsUrl,
                LeasePath = instance.LeasePath,
                ProcessStartedAt = DevAppInstanceLease.GetCurrentProcessStartedAt()
            };
        }

        var addresses = (services.GetService(typeof(IServer)) as IServer)?.Features.Get<IServerAddressesFeature>()?.Addresses;
        descriptor.Urls = GetUrls(addresses, instance);

        return descriptor;
    }

    internal static IReadOnlyList<string> GetUrls(IEnumerable<string> addresses, DevAppInstanceContext instance)
    {
        var urls = (addresses ?? [])
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Select(NormalizeAddress)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(url => url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ToArray();
        if (urls.Length > 0 || instance is not { IsActive: true })
        {
            return urls;
        }

        return new[] { instance.HttpsUrl, instance.HttpUrl }.Where(url => !string.IsNullOrWhiteSpace(url)).ToArray();
    }

    private static string NormalizeAddress(string address)
    {
        var url = address.TrimEnd('/');
        foreach (var wildcard in new[] { "://*", "://+", "://[::]", "://0.0.0.0" })
        {
            url = url.Replace(wildcard, "://localhost", StringComparison.Ordinal);
        }

        return url;
    }
}