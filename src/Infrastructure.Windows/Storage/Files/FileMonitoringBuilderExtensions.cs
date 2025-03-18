// File: BridgingIT.DevKit.Infrastructure.Windows.Storage/FileMonitoringBuilderExtensions.cs
namespace BridgingIT.DevKit.Infrastructure.Windows.Storage;

using BridgingIT.DevKit.Application.FileMonitoring;

/// <summary>
/// Provides extension methods for configuring Windows-specific storage in the FileMonitoring system.
/// </summary>
public static class FileMonitoringBuilderExtensions
{
    /// <summary>
    /// Configures a Windows network share location for monitoring.
    /// Registers a LocationHandler with a NetworkFileStorageProvider for the specified UNC path.
    /// </summary>
    /// <param name="builder">The FileMonitoringBuilder instance.</param>
    /// <param name="name">The unique name of the location (e.g., "NetworkDocs").</param>
    /// <param name="path">The network share UNC path to monitor (e.g., "\\\\server\\docs").</param>
    /// <param name="impersonationService">The Windows impersonation service for accessing the network share.</param>
    /// <param name="configure">An action to configure the LocationOptions (e.g., file pattern, processors).</param>
    /// <returns>The FileMonitoringBuilder instance for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddFileMonitoring(monitoring =>
    /// {
    ///     monitoring.UseWindowsNetwork("NetworkDocs", "\\\\server\\docs", impersonationService, options =>
    ///     {
    ///         options.FilePattern = "*.docx";
    ///         options.UseProcessor<FileLoggerProcessor>();
    ///     });
    /// });
    /// </code>
    /// </example>
    public static FileMonitoringBuilder UseWindowsNetwork(
        this FileMonitoringBuilder builder,
        string name,
        string path,
        IWindowsImpersonationService impersonationService,
        Action<LocationOptions> configure)
    {
        EnsureArg.IsNotNull(builder, nameof(builder));
        EnsureArg.IsNotNullOrEmpty(name, nameof(name));
        EnsureArg.IsNotNullOrEmpty(path, nameof(path));
        EnsureArg.IsNotNull(impersonationService, nameof(impersonationService));
        EnsureArg.IsNotNull(configure, nameof(configure));

        var options = new LocationOptions(name);
        configure(options);
        builder.RegisterLocation(name, options, () => new NetworkFileStorageProvider(name, path, impersonationService), locationHandlerType: null);
        return builder;
    }
}