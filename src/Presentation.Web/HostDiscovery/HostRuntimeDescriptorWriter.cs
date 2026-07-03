namespace BridgingIT.DevKit.Presentation.Web;

using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Hosting;
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

        return descriptor;
    }
}