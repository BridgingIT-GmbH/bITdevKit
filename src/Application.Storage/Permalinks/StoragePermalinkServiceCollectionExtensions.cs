// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Storage;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Registers the Storage Permalink Registry and its persistence provider.
/// </summary>
/// <example>
/// <code>
/// services.AddStoragePermalinks().UseInMemory();
/// </code>
/// </example>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Storage Permalink services. A registry provider must be selected explicitly.
    /// </summary>
    public static StoragePermalinkBuilderContext AddStoragePermalinks(this IServiceCollection services, Action<StoragePermalinkOptions> configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = new StoragePermalinkOptions();
        configure?.Invoke(options);
        var validation = options.Validate();
        if (validation.IsFailure)
        {
            throw new InvalidOperationException(validation.Errors.First().Message);
        }

        services.TryAddSingleton(options);
        services.TryAddSingleton<StoragePermalinkMetrics>();
        services.TryAddSingleton<StoragePermalinkChangeQueue>();
        services.TryAddSingleton<IStoragePermalinkChangeQueue>(sp => sp.GetRequiredService<StoragePermalinkChangeQueue>());
        services.TryAddSingleton<StoragePermalinkRegistry>();
        services.TryAddSingleton<IStoragePermalinkRegistry>(sp => sp.GetRequiredService<StoragePermalinkRegistry>());
        services.TryAddSingleton<IStoragePermalinkMaintenanceService>(sp => sp.GetRequiredService<StoragePermalinkRegistry>());
        services.TryAddScoped<StoragePermalinkChangeHandler>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, StoragePermalinkDispatchService>());

        return new StoragePermalinkBuilderContext(services);
    }
}

/// <summary>
/// Configures Storage Permalink Registry persistence.
/// </summary>
/// <example>
/// <code>
/// services.AddStoragePermalinks().UseInMemory();
/// </code>
/// </example>
public sealed class StoragePermalinkBuilderContext(IServiceCollection services)
{
    /// <summary>
    /// Gets the service collection.
    /// </summary>
    public IServiceCollection Services { get; } = services;

    /// <summary>
    /// Uses the volatile in-memory registry provider.
    /// </summary>
    public StoragePermalinkBuilderContext UseInMemory()
    {
        this.Services.Replace(ServiceDescriptor.Singleton<IStoragePermalinkRegistryProvider>(sp => new InMemoryStoragePermalinkRegistryProvider(sp.GetService<TimeProvider>())));
        return this;
    }
}
