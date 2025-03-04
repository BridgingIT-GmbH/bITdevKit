// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// A fluent builder for configuring and creating IFileStorageProvider instances with optional behaviors (e.g., logging, caching, retry),
/// supporting multiple providers, unknown/custom providers, and unknown/custom behaviors with configurable lifetimes.
/// Example: `var builder = new FileStorageBuilder(services).Use<CustomFileStorageProvider>().WithLogging().Build();`
/// </summary>
public class FileStorageBuilder(IServiceProvider serviceProvider = null)
{
    private IFileStorageProvider provider;
    private readonly List<Func<IFileStorageProvider, IFileStorageBehavior>> customBehaviors = [];
    private ServiceLifetime lifetime = ServiceLifetime.Scoped; // Default to Scoped

    /// <summary>
    /// Sets the service lifetime for the provider (Scoped, Singleton, or Transient).
    /// Example: `var builder = new FileStorageBuilder().WithLifetime(ServiceLifetime.Singleton);`
    /// </summary>
    /// <param name="lifetime">The service lifetime (Scoped, Singleton, or Transient).</param>
    /// <returns>The builder instance for chaining.</returns>
    public FileStorageBuilder WithLifetime(ServiceLifetime lifetime)
    {
        this.lifetime = lifetime;
        return this;
    }

    /// <summary>
    /// Sets the underlying file storage provider to an in-memory implementation.
    /// Example: `var builder = new FileStorageBuilder().UseInMemory("InMemoryTest");`
    /// </summary>
    /// <param name="locationName">The name of the storage location (e.g., "InMemoryTest").</param>
    /// <returns>The builder instance for chaining.</returns>
    public FileStorageBuilder UseInMemory(string locationName = "InMemory")
    {
        this.provider = new InMemoryFileStorageProvider(locationName);
        return this;
    }

    /// <summary>
    /// Sets the underlying file storage provider to a local file system implementation.
    /// Example: `var builder = new FileStorageBuilder().UseLocal("C:\\Storage", "LocalStorage");`
    /// </summary>
    /// <param name="rootPath">The root directory path for local storage (e.g., "C:\\Storage").</param>
    /// <param name="locationName">The name of the storage location (e.g., "LocalStorage").</param>
    /// <returns>The builder instance for chaining.</returns>
    public FileStorageBuilder UseLocal(string rootPath, string locationName = "Local")
    {
        this.provider = new LocalFileStorageProvider(rootPath, locationName);
        return this;
    }

    /// <summary>
    /// Sets the underlying file storage provider to a custom provider of the specified type, resolved via the service provider.
    /// Example: `var builder = new FileStorageBuilder(services).Use<CustomFileStorageProvider>().WithLogging().Build();`
    /// </summary>
    /// <typeparam name="TProvider">The type of the IFileStorageProvider implementation to use (must implement IFileStorageProvider).</typeparam>
    /// <returns>The builder instance for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the provider type cannot be resolved or created via the service provider.</exception>
    public FileStorageBuilder Use<TProvider>()
        where TProvider : IFileStorageProvider
    {
        if (serviceProvider == null)
        {
            throw new InvalidOperationException("Service provider is required to resolve a custom provider type.");
        }

        this.provider = serviceProvider.GetService<TProvider>() ?? ActivatorUtilities.CreateInstance<TProvider>(serviceProvider);
        if (this.provider == null)
        {
            throw new InvalidOperationException($"Could not resolve or create an instance of {typeof(TProvider).Name}.");
        }
        return this;
    }

    /// <summary>
    /// Sets the underlying file storage provider to a specific provider instance.
    /// Example: `var customProvider = new CustomFileStorageProvider(); var builder = new FileStorageBuilder().Use(customProvider).WithLogging().Build();`
    /// </summary>
    /// <param name="provider">The IFileStorageProvider instance to use.</param>
    /// <returns>The builder instance for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the provider is null.</exception>
    public FileStorageBuilder Use(IFileStorageProvider provider)
    {
        this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        return this;
    }

    /// <summary>
    /// Adds logging behavior to the file storage provider.
    /// Example: `var builder = new FileStorageBuilder().UseInMemory("InMemoryTest").WithLogging();`
    /// </summary>
    /// <param name="options">Optional logging configuration (defaults to LogLevel.Information).</param>
    /// <returns>The builder instance for chaining.</returns>
    public FileStorageBuilder WithLogging(LoggingOptions options = null)
    {
        if (this.provider == null)
        {
            throw new InvalidOperationException("Provider must be set before adding behaviors.");
        }

        var loggerFactory = serviceProvider?.GetService<ILoggerFactory>() ?? new LoggerFactory();
        this.provider = new LoggingFileStorageBehavior(this.provider, loggerFactory, options ?? new LoggingOptions());
        return this;
    }

    /// <summary>
    /// Adds caching behavior to the file storage provider.
    /// Example: `var builder = new FileStorageBuilder().UseInMemory("InMemoryTest").WithCaching();`
    /// </summary>
    /// <param name="options">Optional caching configuration (defaults to 10-minute duration).</param>
    /// <returns>The builder instance for chaining.</returns>
    public FileStorageBuilder WithCaching(CachingOptions options = null)
    {
        if (this.provider == null)
        {
            throw new InvalidOperationException("Provider must be set before adding behaviors.");
        }

        var cache = serviceProvider?.GetService<IMemoryCache>() ?? new MemoryCache(new MemoryCacheOptions());
        this.provider = new CachingFileStorageBehavior(this.provider, cache, options ?? new CachingOptions());
        return this;
    }

    /// <summary>
    /// Adds retry behavior to the file storage provider.
    /// Example: `var builder = new FileStorageBuilder().UseInMemory("InMemoryTest").WithRetry();`
    /// </summary>
    /// <param name="options">Optional retry configuration (defaults to 3 retries).</param>
    /// <returns>The builder instance for chaining.</returns>
    public FileStorageBuilder WithRetry(RetryOptions options = null)
    {
        if (this.provider == null)
        {
            throw new InvalidOperationException("Provider must be set before adding behaviors.");
        }

        var loggerFactory = serviceProvider?.GetService<ILoggerFactory>() ?? new LoggerFactory();
        this.provider = new RetryFileStorageBehavior(this.provider, loggerFactory, options ?? new RetryOptions());
        return this;
    }

    /// <summary>
    /// Adds a custom behavior to the file storage provider.
    /// Example: `var builder = new FileStorageBuilder().UseInMemory("InMemoryTest").WithCustomBehavior(p => new CustomBehavior(p));`
    /// </summary>
    /// <param name="behaviorFactory">A factory function that creates an IFileStorageBehavior instance from the current provider.</param>
    /// <returns>The builder instance for chaining.</returns>
    public FileStorageBuilder WithCustomBehavior(Func<IFileStorageProvider, IFileStorageBehavior> behaviorFactory)
    {
        if (this.provider == null)
        {
            throw new InvalidOperationException("Provider must be set before adding behaviors.");
        }

        ArgumentNullException.ThrowIfNull(behaviorFactory);

        this.customBehaviors.Add(behaviorFactory);
        return this;
    }

    /// <summary>
    /// Builds and returns the configured IFileStorageProvider instance, applying all behaviors in order.
    /// Example: `var provider = new FileStorageBuilder(services).Use<CustomFileStorageProvider>().WithLogging().Build();`
    /// </summary>
    /// <returns>The configured IFileStorageProvider instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no provider has been set.</exception>
    public IFileStorageProvider Build()
    {
        if (this.provider == null)
        {
            throw new InvalidOperationException("No provider has been configured. Use UseInMemory, UseLocal, Use<TProvider>, or Use(IFileStorageProvider) first.");
        }

        var currentProvider = this.provider;
        foreach (var behaviorFactory in this.customBehaviors)
        {
            currentProvider = behaviorFactory(currentProvider);
        }
        return currentProvider;
    }

    /// <summary>
    /// Gets the configured service lifetime for the provider.
    /// </summary>
    internal ServiceLifetime Lifetime => this.lifetime;
}