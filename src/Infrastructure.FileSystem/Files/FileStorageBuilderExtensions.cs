namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.FileSystem;

/// <summary>Registers filesystem File Storage and monitoring implementations.</summary>
/// <example><code>services.AddFileStorage(factory => factory.RegisterProvider("reports", builder => builder.UseLocal("reports", root)));</code></example>
public static class FileSystemFileStorageBuilderExtensions
{
    /// <summary>Configures a provider that stores ordinary files at their caller-supplied relative paths.</summary>
    /// <param name="builder">The provider builder.</param>
    /// <param name="locationName">The location displayed by the provider.</param>
    /// <param name="rootPath">The filesystem root containing the ordinary files.</param>
    /// <param name="ensureRoot">Whether initialization creates a missing root.</param>
    /// <param name="lockTimeout">The existing in-process lock timeout, defaulting to thirty seconds.</param>
    /// <returns>The same builder for behavior and lifetime configuration.</returns>
    /// <example><code>builder.UseLocal("reports", root).WithLogging();</code></example>
    public static FileStorageProviderFactory.FileStorageBuilder UseLocal(
        this FileStorageProviderFactory.FileStorageBuilder builder,
        string locationName,
        string rootPath,
        bool ensureRoot = true,
        TimeSpan? lockTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ProviderFactory = _ => new LocalFileStorageProvider(locationName, rootPath, ensureRoot, lockTimeout);

        return builder;
    }

    /// <summary>Registers a local file provider and filesystem watcher through the existing monitoring pipeline.</summary>
    /// <param name="builder">The monitoring builder.</param>
    /// <param name="name">The unique monitoring location name.</param>
    /// <param name="path">The local directory or mounted-share directory.</param>
    /// <param name="configure">Optional location and processor configuration.</param>
    /// <param name="ensureRoot">Whether initialization creates a missing root.</param>
    /// <returns>The same monitoring builder.</returns>
    /// <example><code>services.AddFileMonitoring(builder => builder.UseLocal("inbound", root));</code></example>
    public static FileMonitoringBuilder UseLocal(
        this FileMonitoringBuilder builder,
        string name,
        string path,
        Action<LocationOptions> configure = null,
        bool ensureRoot = true)
    {
        ArgumentNullException.ThrowIfNull(builder);
        EnsureArg.IsNotNullOrEmpty(name, nameof(name));
        EnsureArg.IsNotNullOrEmpty(path, nameof(path));
        var options = new LocationOptions(name);
        configure?.Invoke(options);
        builder.RegisterLocation(name, options, () => new LocalFileStorageProvider(name, path, ensureRoot), typeof(LocalLocationHandler));

        return builder;
    }
}
