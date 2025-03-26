// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Fluent builder for configuring the FileMonitoring system.
/// Provides core methods; location-specific configurations are added via extensions.
/// </summary>
public class FileMonitoringBuilder
{
    private readonly IServiceCollection services;

    internal FileMonitoringBuilder(IServiceCollection services) => this.services = services;

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
    public FileMonitoringBuilder UseLocal(string name, string path, Action<LocationOptions> configure, bool ensureRoot = true)
    {
        EnsureArg.IsNotNullOrEmpty(name, nameof(name));
        EnsureArg.IsNotNullOrEmpty(path, nameof(path));
        EnsureArg.IsNotNull(configure, nameof(configure));

        var options = new LocationOptions(name);
        configure(options);
        this.RegisterLocation(name, options, () => new LocalFileStorageProvider(name, path, ensureRoot), typeof(LocalLocationHandler));
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
        this.RegisterLocation(name, options, () => new InMemoryFileStorageProvider(name), typeof(InMemoryLocationHandler));
        return this;
    }

    public void RegisterLocation(string name, LocationOptions options, Func<IFileStorageProvider> providerFactory, Type locationHandlerType)
    {
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

        if (locationHandlerType != null)
        {
            this.services.AddScoped(sp => Activator.CreateInstance(
                locationHandlerType,
                sp.GetRequiredService<ILoggerFactory>().CreateLogger(locationHandlerType),
                providerFactory(),
                sp.GetRequiredService<IFileEventStore>(),
                options,
                sp,
                sp.GetServices<IMonitoringBehavior>()) as ILocationHandler);
        }
    }
}