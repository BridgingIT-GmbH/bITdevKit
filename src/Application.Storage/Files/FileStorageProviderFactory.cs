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
/// Factory for creating and managing file storage providers with various configurations and behaviors.
/// </summary>
public class FileStorageProviderFactory(IServiceProvider serviceProvider) : IFileStorageProviderFactory
{
    private readonly IServiceProvider serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly IServiceScopeFactory scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
    private readonly ConcurrentDictionary<string, (ServiceLifetime Lifetime, Func<IServiceProvider, IFileStorageProvider> ProviderFactory, List<Func<IFileStorageProvider, IServiceProvider, IFileStorageProvider>> Behaviors)> providerConfigs = [];
    private readonly ConcurrentDictionary<string, Lazy<IFileStorageProvider>> singletonProviders = [];

    /// <summary>
    /// Creates a file storage provider with the specified name.
    /// </summary>
    public IFileStorageProvider CreateProvider(string name)
    {
        if (!this.providerConfigs.TryGetValue(name, out var config))
        {
            throw new KeyNotFoundException($"No file storage provider registered with name '{name}'.");
        }

        return config.Lifetime switch
        {
            ServiceLifetime.Singleton => this.GetOrCreateSingletonProvider(name, config),
            ServiceLifetime.Scoped => this.CreateScopedProvider(config),
            ServiceLifetime.Transient => this.CreateTransientProvider(config),
            _ => throw new ArgumentException($"Unsupported lifetime: {config.Lifetime}", nameof(config.Lifetime))
        };
    }

    /// <summary>
    /// Creates a file storage provider of the specified implementation type.
    /// </summary>
    public IFileStorageProvider CreateProvider<TImplementation>() where TImplementation : IFileStorageProvider
    {
        var matchingProviders = this.providerConfigs
            .Select(kvp => (Name: kvp.Key, Config: kvp.Value))
            .Select(x => (x.Name, Provider: x.Config.Lifetime switch
            {
                ServiceLifetime.Singleton => this.GetOrCreateSingletonProvider(x.Name, x.Config),
                ServiceLifetime.Scoped => this.CreateScopedProvider(x.Config),
                ServiceLifetime.Transient => this.CreateTransientProvider(x.Config),
                _ => throw new ArgumentException($"Unsupported lifetime: {x.Config.Lifetime}", nameof(x.Config.Lifetime))
            }))
            .Where(x => x.Provider is TImplementation)
            .ToList();

        if (matchingProviders.Count == 0)
        {
            throw new InvalidOperationException($"No file storage provider of type {typeof(TImplementation).Name} is registered.");
        }

        if (matchingProviders.Count > 1)
        {
            var providerNames = string.Join(", ", matchingProviders.Select(x => x.Name));
            throw new InvalidOperationException($"Multiple file storage providers of type {typeof(TImplementation).Name} are registered with names: {providerNames}. Please specify a provider name using CreateProvider(string) to resolve ambiguity.");
        }

        return matchingProviders.First().Provider;
    }

    /// <summary>
    /// Registers a new file storage provider with the specified name and configuration.
    /// </summary>
    public IFileStorageProviderFactory RegisterProvider(string name, Action<FileStorageBuilder> configure)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Provider name cannot be null or empty.", nameof(name));
        }

        if (this.providerConfigs.ContainsKey(name))
        {
            throw new ArgumentException($"A provider with name '{name}' is already registered.", nameof(name));
        }

        var builder = new FileStorageBuilder(this, name);
        configure?.Invoke(builder);

        this.providerConfigs.TryAdd(name, (builder.Lifetime, builder.ProviderFactory, builder.Behaviors));

        return this;
    }

    /// <summary>
    /// Adds a behavior to one or all registered providers.
    /// </summary>
    public IFileStorageProviderFactory WithBehavior(string providerName, Func<IFileStorageProvider, IServiceProvider, IFileStorageBehavior> behaviorFactory)
    {
        ArgumentNullException.ThrowIfNull(behaviorFactory);

        if (providerName == null)
        {
            foreach (var kvp in this.providerConfigs)
            {
                var currentProvider = this.CreateProvider(kvp.Key);
                var newProvider = behaviorFactory(currentProvider, this.serviceProvider);
                if (kvp.Value.Lifetime == ServiceLifetime.Singleton)
                {
                    this.singletonProviders.TryUpdate(kvp.Key, new Lazy<IFileStorageProvider>(() => newProvider, LazyThreadSafetyMode.ExecutionAndPublication), this.singletonProviders[kvp.Key]);
                }
            }
        }
        else
        {
            if (!this.providerConfigs.TryGetValue(providerName, out var config))
            {
                throw new KeyNotFoundException($"No file storage provider registered with name '{providerName}'.");
            }

            var currentProvider = this.CreateProvider(providerName);
            var newProvider = behaviorFactory(currentProvider, this.serviceProvider);
            if (config.Lifetime == ServiceLifetime.Singleton)
            {
                this.singletonProviders.TryUpdate(providerName, new Lazy<IFileStorageProvider>(() => newProvider, LazyThreadSafetyMode.ExecutionAndPublication), this.singletonProviders[providerName]);
            }
        }

        return this;
    }

    private IFileStorageProvider GetOrCreateSingletonProvider(string name, (ServiceLifetime Lifetime, Func<IServiceProvider, IFileStorageProvider> ProviderFactory, List<Func<IFileStorageProvider, IServiceProvider, IFileStorageProvider>> Behaviors) config)
    {
        if (!this.singletonProviders.TryGetValue(name, out var providerLazy))
        {
            var provider = config.ProviderFactory(this.serviceProvider);
            var decoratedProvider = this.ApplyBehaviors(provider, config.Behaviors);
            this.singletonProviders.TryAdd(name, new Lazy<IFileStorageProvider>(() => decoratedProvider, LazyThreadSafetyMode.ExecutionAndPublication));
        }
        return this.singletonProviders[name].Value;
    }

    private IFileStorageProvider CreateScopedProvider((ServiceLifetime Lifetime, Func<IServiceProvider, IFileStorageProvider> ProviderFactory, List<Func<IFileStorageProvider, IServiceProvider, IFileStorageProvider>> Behaviors) config)
    {
        using var scope = this.scopeFactory.CreateScope();
        var scopedProvider = config.ProviderFactory(scope.ServiceProvider);
        return this.ApplyBehaviors(scopedProvider, config.Behaviors);
    }

    private IFileStorageProvider CreateTransientProvider((ServiceLifetime Lifetime, Func<IServiceProvider, IFileStorageProvider> ProviderFactory, List<Func<IFileStorageProvider, IServiceProvider, IFileStorageProvider>> Behaviors) config)
    {
        var transientProvider = config.ProviderFactory(this.serviceProvider);
        return this.ApplyBehaviors(transientProvider, config.Behaviors);
    }

    private IFileStorageProvider ApplyBehaviors(IFileStorageProvider provider, List<Func<IFileStorageProvider, IServiceProvider, IFileStorageProvider>> behaviors)
    {
        var decoratedProvider = provider;
        foreach (var behavior in behaviors)
        {
            decoratedProvider = behavior(decoratedProvider, this.serviceProvider) ?? throw new InvalidOperationException("Behavior returned null provider.");
        }
        return decoratedProvider;
    }

    /// <summary>
    /// Builder for configuring file storage providers and their behaviors.
    /// </summary>
    public class FileStorageBuilder(FileStorageProviderFactory factory, string providerName)
    {
        private readonly FileStorageProviderFactory factory = factory ?? throw new ArgumentNullException(nameof(factory));
        private readonly string providerName = providerName ?? throw new ArgumentNullException(nameof(providerName));
        private ServiceLifetime lifetime = ServiceLifetime.Scoped;
        private readonly List<Func<IFileStorageProvider, IServiceProvider, IFileStorageProvider>> behaviors = [];

        /// <summary>
        /// Gets or sets the factory function to create the provider instance.
        /// </summary>
        public Func<IServiceProvider, IFileStorageProvider> ProviderFactory;

        /// <summary>
        /// Gets the service lifetime for the provider.
        /// </summary>
        public ServiceLifetime Lifetime => this.lifetime;

        /// <summary>
        /// Gets the list of behavior decorator functions for the provider.
        /// </summary>
        public List<Func<IFileStorageProvider, IServiceProvider, IFileStorageProvider>> Behaviors => this.behaviors;

        /// <summary>
        /// Configures an in-memory file storage provider.
        /// </summary>
        public FileStorageBuilder UseInMemory(string locationName)
        {
            this.ProviderFactory = (sp) => new InMemoryFileStorageProvider(locationName);
            return this;
        }

        /// <summary>
        /// Configures a local file system storage provider.
        /// </summary>
        public FileStorageBuilder UseLocal(string locationName, string rootPath, bool ensureRoot = true, TimeSpan? lockTimeout = null)
        {
            this.ProviderFactory = (sp) => new LocalFileStorageProvider(locationName, rootPath, ensureRoot, lockTimeout);
            return this;
        }

        /// <summary>
        /// Adds logging behavior to the provider.
        /// </summary>
        public FileStorageBuilder WithLogging(LoggingOptions options = null)
        {
            this.behaviors.Add((p, sp) => new LoggingFileStorageBehavior(p, sp.GetRequiredService<ILoggerFactory>(), options));
            return this;
        }

        /// <summary>
        /// Adds retry behavior to the provider.
        /// </summary>
        public FileStorageBuilder WithRetry(RetryOptions options = null)
        {
            this.behaviors.Add((p, sp) => new RetryFileStorageBehavior(p, sp.GetRequiredService<ILoggerFactory>(), options));
            return this;
        }

        /// <summary>
        /// Adds caching behavior to the provider.
        /// </summary>
        public FileStorageBuilder WithCaching(CachingOptions options = null)
        {
            this.behaviors.Add((p, sp) => new CachingFileStorageBehavior(p, sp.GetRequiredService<IMemoryCache>(), options));
            return this;
        }

        /// <summary>
        /// Adds a custom behavior to the provider.
        /// </summary>
        public FileStorageBuilder WithBehavior(Func<IFileStorageProvider, IServiceProvider, IFileStorageProvider> behaviorFactory)
        {
            this.behaviors.Add(behaviorFactory ?? throw new ArgumentNullException(nameof(behaviorFactory)));
            return this;
        }

        /// <summary>
        /// Sets the service lifetime for the provider.
        /// </summary>
        public FileStorageBuilder WithLifetime(ServiceLifetime lifetime)
        {
            this.lifetime = lifetime;
            return this;
        }

        /// <summary>
        /// Builds and returns a configured file storage provider.
        /// </summary>
        public IFileStorageProvider Build()
        {
            if (this.ProviderFactory == null)
            {
                throw new InvalidOperationException("Provider configuration must be specified before building.");
            }

            var provider = this.ProviderFactory(this.factory.serviceProvider);
            return this.ApplyBehaviors(provider);
        }

        private IFileStorageProvider ApplyBehaviors(IFileStorageProvider provider)
        {
            var decoratedProvider = provider;
            foreach (var behavior in this.behaviors)
            {
                decoratedProvider = behavior(decoratedProvider, this.factory.serviceProvider) ?? throw new InvalidOperationException("Behavior returned null provider.");
            }
            return decoratedProvider;
        }
    }
}