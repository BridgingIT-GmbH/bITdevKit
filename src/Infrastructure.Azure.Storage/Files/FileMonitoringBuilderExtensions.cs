// File: BridgingIT.DevKit.Infrastructure.Azure.Storage/FileMonitoringBuilderExtensions.cs
namespace BridgingIT.DevKit.Infrastructure.Azure.Storage;

using BridgingIT.DevKit.Application.FileMonitoring;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for configuring Azure-specific storage in the FileMonitoring system.
/// </summary>
public static class FileMonitoringBuilderExtensions
{
    /// <summary>
    /// Configures an Azure Blob Storage location for monitoring.
    /// Registers a LocationHandler with an AzureBlobFileStorageProvider for the specified container.
    /// </summary>
    /// <param name="builder">The FileMonitoringBuilder instance.</param>
    /// <param name="name">The unique name of the location (e.g., "AzureDocs").</param>
    /// <param name="connectionString">The Azure Blob Storage connection string.</param>
    /// <param name="containerName">The name of the blob container to monitor.</param>
    /// <param name="configure">An action to configure the LocationOptions (e.g., file pattern, processors).</param>
    /// <returns>The FileMonitoringBuilder instance for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddFileMonitoring(monitoring =>
    /// {
    ///     monitoring.UseAzureBlobs("AzureDocs", "connection-string", "docs-container", options =>
    ///     {
    ///         options.FilePattern = "*.pdf";
    ///         options.UseProcessor<FileLoggerProcessor>();
    ///     });
    /// });
    /// </code>
    /// </example>
    public static FileMonitoringBuilder UseAzureBlobs(
        this FileMonitoringBuilder builder,
        string name,
        string connectionString,
        string containerName,
        Action<LocationOptions> configure)
    {
        EnsureArg.IsNotNull(builder, nameof(builder));
        EnsureArg.IsNotNullOrEmpty(name, nameof(name));
        EnsureArg.IsNotNullOrEmpty(connectionString, nameof(connectionString));
        EnsureArg.IsNotNullOrEmpty(containerName, nameof(containerName));
        EnsureArg.IsNotNull(configure, nameof(configure));

        var options = new LocationOptions(name);
        configure(options);
        builder.RegisterLocation(name, options, () => new AzureBlobFileStorageProvider(name, connectionString, containerName));
        return builder;
    }

    /// <summary>
    /// Configures an Azure Files location for monitoring.
    /// Registers a LocationHandler with an AzureFilesFileStorageProvider for the specified share.
    /// </summary>
    /// <param name="builder">The FileMonitoringBuilder instance.</param>
    /// <param name="name">The unique name of the location (e.g., "AzureFilesDocs").</param>
    /// <param name="connectionString">The Azure Files connection string.</param>
    /// <param name="shareName">The name of the file share to monitor.</param>
    /// <param name="configure">An action to configure the LocationOptions (e.g., file pattern, processors).</param>
    /// <returns>The FileMonitoringBuilder instance for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddFileMonitoring(monitoring =>
    /// {
    ///     monitoring.UseAzureFiles("AzureFilesDocs", "connection-string", "docs-share", options =>
    ///     {
    ///         options.FilePattern = "*.txt";
    ///         options.UseProcessor<FileLoggerProcessor>();
    ///     });
    /// });
    /// </code>
    /// </example>
    public static FileMonitoringBuilder UseAzureFiles(
        this FileMonitoringBuilder builder,
        string name,
        string connectionString,
        string shareName,
        Action<LocationOptions> configure)
    {
        EnsureArg.IsNotNull(builder, nameof(builder));
        EnsureArg.IsNotNullOrEmpty(name, nameof(name));
        EnsureArg.IsNotNullOrEmpty(connectionString, nameof(connectionString));
        EnsureArg.IsNotNullOrEmpty(shareName, nameof(shareName));
        EnsureArg.IsNotNull(configure, nameof(configure));

        var options = new LocationOptions(name);
        configure(options);
        builder.RegisterLocation(name, options, () => new AzureFilesFileStorageProvider(name, connectionString, shareName));
        return builder;
    }
}