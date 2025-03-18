// File: BridgingIT.DevKit.Application.FileMonitoring/ServiceCollectionExtensions.cs
namespace BridgingIT.DevKit.Application.FileMonitoring;

using System;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Provides extension methods for IServiceCollection to configure the FileMonitoring system.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the FileMonitoring system to the service collection with a fluent configuration API.
    /// Registers FileMonitoringService as a singleton and allows customization via a builder.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <param name="configure">An action to configure the FileMonitoringBuilder.</param>
    /// <returns>A FileMonitoringBuilderContext for further configuration chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddFileMonitoring(monitoring =>
    /// {
    ///     monitoring
    ///         .WithBehavior<LoggingBehavior>()
    ///         .UseLocal("Docs", "C:\\Docs", options =>
    ///         {
    ///             options.FilePattern = "*.txt";
    ///             options.UseOnDemandOnly();
    ///             options.WithProcessorBehavior<LoggingProcessorBehavior>();
    ///             options.UseProcessor<FileLoggerProcessor>();
    ///         });
    /// })
    /// .WithEntityFrameworkStore<MyAppDbContext>(); // Extension from EF infra project
    /// </code>
    /// </example>
    public static FileMonitoringBuilderContext AddFileMonitoring(
        this IServiceCollection services,
        Action<FileMonitoringBuilder> configure)
    {
        EnsureArg.IsNotNull(services, nameof(services));
        EnsureArg.IsNotNull(configure, nameof(configure));

        services.TryAddSingleton<IFileMonitoringService, FileMonitoringService>();
        services.TryAddScoped<IFileEventStore, InMemoryFileEventStore>(); // Default store

        var builder = new FileMonitoringBuilder(services);
        configure(builder);

        return new FileMonitoringBuilderContext(services);
    }
}

/// <summary>
/// Fluent builder for configuring the FileMonitoring system.
/// Provides core methods; location-specific configurations are added via extensions.
/// </summary>
public class FileMonitoringBuilder
{
    private readonly IServiceCollection services;

    internal FileMonitoringBuilder(IServiceCollection services)
    {
        this.services = services;
    }

    /// <summary>
    /// Configures the FileMonitoring system to use an in-memory event store for testing or simple applications.
    /// Suitable for scenarios where persistence is not required across restarts.
    /// </summary>
    /// <returns>The current FileMonitoringBuilder instance for chaining.</returns>
    public FileMonitoringBuilder WithInMemoryStore()
    {
        this.services.RemoveAll<IFileEventStore>(); // Replace default if already set
        this.services.AddScoped<IFileEventStore, InMemoryFileEventStore>();
        return this;
    }

    /// <summary>
    /// Adds a global monitoring behavior to observe scan operations across all locations.
    /// Behaviors are executed during ScanAsync and can log or track scan events.
    /// </summary>
    /// <typeparam name="TBehavior">The type of IMonitoringBehavior to register.</typeparam>
    /// <returns>The current FileMonitoringBuilder instance for chaining.</returns>
    public FileMonitoringBuilder WithBehavior<TBehavior>()
        where TBehavior : class, IMonitoringBehavior
    {
        this.services.AddScoped<IMonitoringBehavior, TBehavior>();
        return this;
    }

    /// <summary>
    /// Configures a local file system location for monitoring.
    /// Registers a LocationHandler with a LocalFileStorageProvider for the specified path.
    /// </summary>
    /// <param name="name">The unique name of the location (e.g., "Docs").</param>
    /// <param name="path">The local file system path to monitor (e.g., "C:\\Docs").</param>
    /// <param name="configure">An action to configure the LocationOptions (e.g., file pattern, processors).</param>
    /// <returns>The current FileMonitoringBuilder instance for chaining.</returns>
    public FileMonitoringBuilder UseLocal(string name, string path, Action<LocationOptions> configure)
    {
        EnsureArg.IsNotNullOrEmpty(name, nameof(name));
        EnsureArg.IsNotNullOrEmpty(path, nameof(path));
        EnsureArg.IsNotNull(configure, nameof(configure));

        var options = new LocationOptions(name);
        configure(options);
        this.RegisterLocation(name, options, () => new LocalFileStorageProvider(name, path));
        return this;
    }

    /// <summary>
    /// Configures an in-memory location for monitoring.
    /// Registers a LocationHandler with an InMemoryFileStorageProvider, useful for testing.
    /// </summary>
    /// <param name="name">The unique name of the location (e.g., "MemoryDocs").</param>
    /// <param name="configure">An action to configure the LocationOptions (e.g., processors).</param>
    /// <returns>The current FileMonitoringBuilder instance for chaining.</returns>
    public FileMonitoringBuilder UseInMemory(string name, Action<LocationOptions> configure)
    {
        EnsureArg.IsNotNullOrEmpty(name, nameof(name));
        EnsureArg.IsNotNull(configure, nameof(configure));

        var options = new LocationOptions(name);
        configure(options);
        this.RegisterLocation(name, options, () => new InMemoryFileStorageProvider(name));
        return this;
    }

    public void RegisterLocation(string name, LocationOptions options, Func<IFileStorageProvider> providerFactory)
    {
        // Register processors and behaviors from options
        foreach (var config in options.ProcessorConfigs)
        {
            this.services.TryAddScoped(config.ProcessorType);
            foreach (var behaviorType in config.BehaviorTypes)
            {
                this.services.TryAddScoped(behaviorType);
            }
        }
        foreach (var behaviorType in options.LocationProcessorBehaviors)
        {
            this.services.TryAddScoped(behaviorType);
        }

        // Register LocationHandler with the specified provider
        this.services.AddSingleton(sp =>
            new LocationHandler(
                sp.GetRequiredService<ILogger<LocationHandler>>(),
                providerFactory(),
                sp.GetRequiredService<IFileEventStore>(),
                options,
                sp,
                sp.GetServices<IMonitoringBehavior>()));
    }
}

/// <summary>
/// Context returned by AddFileMonitoring to allow further configuration chaining.
/// </summary>
public class FileMonitoringBuilderContext
{
    private readonly IServiceCollection services;

    internal FileMonitoringBuilderContext(IServiceCollection services)
    {
        this.services = services;
    }

    /// <summary>
    /// Provides access to the underlying IServiceCollection for additional service registrations.
    /// </summary>
    public IServiceCollection Services => this.services;
}