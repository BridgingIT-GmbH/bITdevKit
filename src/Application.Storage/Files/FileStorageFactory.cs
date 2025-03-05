// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Linq;

/// <summary>
/// A factory that creates and manages multiple configured IFileStorageProvider instances,
/// supporting runtime configuration, custom behaviors, configurable lifetimes, and provider type lookup.
/// Example: `var factory = new FileStorageFactory(services); var provider = factory.CreateProvider<InMemoryFileStorageProvider>();`
/// </summary>
public class FileStorageFactory(IServiceProvider serviceProvider = null)
    : IFileStorageFactory
{
    private readonly ConcurrentDictionary<string, Lazy<IFileStorageProvider>> providers = [];

    /// <summary>
    /// Creates or retrieves the configured IFileStorageProvider instance for a specific name.
    /// Example: `var provider = factory.CreateProvider("local"); var result = await provider.ExistsAsync("folder/file.txt", null, CancellationToken.None);`
    /// </summary>
    /// <param name="name">The name of the provider configuration.</param>
    /// <returns>The configured IFileStorageProvider instance.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if no provider is registered for the specified name.</exception>
    public IFileStorageProvider CreateProvider(string name)
    {
        if (!this.providers.TryGetValue(name, out var providerLazy))
        {
            throw new KeyNotFoundException($"No file storage provider registered with name '{name}'.");
        }
        return providerLazy.Value;
    }

    /// <summary>
    /// Creates or retrieves the first configured IFileStorageProvider instance of the specified implementation type,
    /// or throws an exception if multiple providers of the same type exist without unique names.
    /// Example: `var provider = factory.CreateProvider<InMemoryFileStorageProvider>(); var result = await provider.ExistsAsync("test/file.txt", null, CancellationToken.None);`
    /// </summary>
    /// <typeparam name="TImplementation">The type of the IFileStorageProvider implementation (e.g., InMemoryFileStorageProvider, LocalFileStorageProvider).</typeparam>
    /// <returns>The first configured IFileStorageProvider instance of the specified type.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if no provider of the specified type is registered, or if multiple providers of the same type exist
    /// without unique names, causing ambiguity.
    /// </exception>
    public IFileStorageProvider CreateProvider<TImplementation>() where TImplementation : IFileStorageProvider
    {
        var matchingProviders = this.providers.Values
            .Select(p => p.Value)
            .Where(p => p is TImplementation)
            .ToList();

        if (!matchingProviders.Any())
        {
            throw new InvalidOperationException($"No file storage provider of type {typeof(TImplementation).Name} is registered.");
        }

        if (matchingProviders.Count > 1)
        {
            var providerNames = string.Join(", ", this.providers
                .Where(kvp => matchingProviders.Contains(kvp.Value.Value))
                .Select(kvp => kvp.Key));
            throw new InvalidOperationException($"Multiple file storage providers of type {typeof(TImplementation).Name} are registered with names: {providerNames}. Please specify a provider name using CreateProvider(string) to resolve ambiguity.");
        }

        return matchingProviders.First();
    }

    /// <summary>
    /// Registers a new file storage provider with the specified name, configuration, and lifetime.
    /// Example: `factory.RegisterProvider("local", builder => builder.UseLocal("C:\\Storage", "Local").WithLogging(), ServiceLifetime.Singleton);`
    /// </summary>
    /// <param name="name">The unique name for the provider configuration.</param>
    /// <param name="configure">Action to configure the FileStorageBuilder for this provider.</param>
    /// <param name="lifetime">The service lifetime for the provider (Scoped, Singleton, or Transient).</param>
    /// <exception cref="ArgumentException">Thrown if the name is null, empty, or already registered.</exception>
    public IFileStorageFactory WithProvider(string name, Action<FileStorageBuilder> configure, ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Provider name cannot be null or empty.", nameof(name));
        }

        if (this.providers.ContainsKey(name))
        {
            throw new ArgumentException($"A provider with name '{name}' is already registered.", nameof(name));
        }

        var builder = new FileStorageBuilder(serviceProvider).WithLifetime(lifetime);
        configure(builder);

        var provider = builder.Build();
        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                this.providers.TryAdd(name, new Lazy<IFileStorageProvider>(() => provider, LazyThreadSafetyMode.ExecutionAndPublication));
                break;
            case ServiceLifetime.Scoped:
                this.providers.TryAdd(name, new Lazy<IFileStorageProvider>(() => provider, LazyThreadSafetyMode.None)); // Scoped typically managed by DI
                break;
            case ServiceLifetime.Transient:
                this.providers.TryAdd(name, new Lazy<IFileStorageProvider>(() => provider, LazyThreadSafetyMode.None));
                break;
            default:
                throw new ArgumentException($"Unsupported lifetime: {lifetime}", nameof(lifetime));
        }

        return this;
    }

    /// <summary>
    /// Registers a custom behavior for all providers or a specific provider.
    /// Example: `factory.RegisterCustomBehavior("local", (p, sp) => new CustomBehavior(p));`
    /// </summary>
    /// <param name="providerName">The name of the provider to apply the behavior to (null for all providers).</param>
    /// <param name="behaviorFactory">A factory function that creates an IFileStorageBehavior instance from the provider and service provider.</param>
    /// <exception cref="KeyNotFoundException">Thrown if the specified provider name is not registered.</exception>
    public IFileStorageFactory WithBehavior(string providerName, Func<IFileStorageProvider, IServiceProvider, IFileStorageBehavior> behaviorFactory)
    {
        ArgumentNullException.ThrowIfNull(behaviorFactory);

        if (providerName == null)
        {
            // Apply to all providers
            foreach (var kvp in this.providers)
            {
                var currentProvider = kvp.Value.Value;
                var newProvider = behaviorFactory(currentProvider, serviceProvider);
                this.providers.TryUpdate(kvp.Key, new Lazy<IFileStorageProvider>(() => newProvider, kvp.Value.IsValueCreated ? LazyThreadSafetyMode.None : LazyThreadSafetyMode.ExecutionAndPublication), kvp.Value);
            }
        }
        else
        {
            // Apply to a specific provider
            if (!this.providers.TryGetValue(providerName, out var providerLazy))
            {
                throw new KeyNotFoundException($"No file storage provider registered with name '{providerName}'.");
            }

            var currentProvider = providerLazy.Value;
            var newProvider = behaviorFactory(currentProvider, serviceProvider);
            this.providers.TryUpdate(providerName, new Lazy<IFileStorageProvider>(() => newProvider, providerLazy.IsValueCreated ? LazyThreadSafetyMode.None : LazyThreadSafetyMode.ExecutionAndPublication), providerLazy);
        }

        return this;
    }
}