// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Storage;
using Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Provides top-level fluent configuration for named blob-storage clients.
/// </summary>
/// <param name="services">The service collection being configured.</param>
/// <param name="options">The blob-storage options.</param>
/// <param name="configuration">The optional application configuration used by provider extensions.</param>
/// <example>
/// <code>
/// services.AddBlobStorage()
///     .WithClient("reports", sp => provider);
/// </code>
/// </example>
public sealed class BlobStorageBuilderContext(
    IServiceCollection services,
    BlobStorageOptions options,
    IConfiguration configuration = null)
{
    private readonly List<Func<IBlobStoreClient, IServiceProvider, string, IBlobStoreClient>> behaviors = [];

    /// <summary>
    /// Gets the service collection being configured.
    /// </summary>
    /// <example>
    /// <code>
    /// var services = context.Services;
    /// </code>
    /// </example>
    public IServiceCollection Services { get; } = services ?? throw new ArgumentNullException(nameof(services));

    /// <summary>
    /// Gets the blob-storage options for this registration flow.
    /// </summary>
    /// <example>
    /// <code>
    /// var enabled = context.Options.IsEnabled;
    /// </code>
    /// </example>
    public BlobStorageOptions Options { get; } = options ?? new BlobStorageOptions();

    /// <summary>
    /// Gets the optional configuration root available to provider extensions.
    /// </summary>
    /// <example>
    /// <code>
    /// var configuration = context.Configuration;
    /// </code>
    /// </example>
    public IConfiguration Configuration { get; } = configuration;

    /// <summary>
    /// Gets the default lifetime used for clients registered through this builder.
    /// </summary>
    /// <example>
    /// <code>
    /// var lifetime = context.Lifetime;
    /// </code>
    /// </example>
    public ServiceLifetime Lifetime => this.Options.Lifetime;

    /// <summary>
    /// Registers a blob-store client behavior for all named clients in this builder.
    /// </summary>
    /// <typeparam name="TBehavior">The behavior type implementing <see cref="IBlobStoreClient" />.</typeparam>
    /// <returns>The current builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddBlobStorage()
    ///     .WithBehavior&lt;LoggingBlobStoreClientBehavior&gt;();
    /// </code>
    /// </example>
    public BlobStorageBuilderContext WithBehavior<TBehavior>()
        where TBehavior : class, IBlobStoreClient
    {
        this.behaviors.Add((inner, serviceProvider, name) =>
            ActivatorUtilities.CreateInstance<TBehavior>(serviceProvider, inner, name));

        return this;
    }

    /// <summary>
    /// Registers a blob-store client behavior factory for all named clients in this builder.
    /// </summary>
    /// <param name="behavior">The behavior factory receiving the inner client, service provider, and store name.</param>
    /// <returns>The current builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddBlobStorage()
    ///     .WithBehavior((inner, sp, name) => new LoggingBlobStoreClientBehavior(sp.GetRequiredService&lt;ILoggerFactory&gt;(), inner, name));
    /// </code>
    /// </example>
    public BlobStorageBuilderContext WithBehavior(
        Func<IBlobStoreClient, IServiceProvider, string, IBlobStoreClient> behavior)
    {
        ArgumentNullException.ThrowIfNull(behavior);

        this.behaviors.Add(behavior);

        return this;
    }

    /// <summary>
    /// Registers a named blob-store provider behind a validating <see cref="IBlobStoreClient" />.
    /// </summary>
    /// <param name="name">The unique store/client name.</param>
    /// <param name="providerFactory">The provider factory.</param>
    /// <param name="configure">The optional per-client options callback.</param>
    /// <param name="providerName">The provider label used for diagnostics and continuation-token binding.</param>
    /// <param name="capabilities">The provider capabilities exposed for diagnostics.</param>
    /// <param name="lifetime">The optional client lifetime override.</param>
    /// <returns>The current builder context.</returns>
    /// <example>
    /// <code>
    /// context.RegisterClient("reports", sp => provider, providerName: "Custom");
    /// </code>
    /// </example>
    public BlobStorageBuilderContext RegisterClient(
        string name,
        Func<IServiceProvider, IBlobStoreProvider> providerFactory,
        Action<BlobStoreOptions> configure = null,
        string providerName = null,
        BlobStoreProviderCapabilities capabilities = null,
        ServiceLifetime? lifetime = null)
    {
        ArgumentNullException.ThrowIfNull(providerFactory);

        return this.RegisterClient(
            name,
            (serviceProvider, _) => providerFactory(serviceProvider),
            configure,
            providerName,
            capabilities,
            lifetime);
    }

    /// <summary>
    /// Registers a named blob-store provider behind a validating <see cref="IBlobStoreClient" />.
    /// </summary>
    /// <param name="name">The unique store/client name.</param>
    /// <param name="providerFactory">The provider factory that receives the resolved per-client options.</param>
    /// <param name="configure">The optional per-client options callback.</param>
    /// <param name="providerName">The provider label used for diagnostics and continuation-token binding.</param>
    /// <param name="capabilities">The provider capabilities exposed for diagnostics.</param>
    /// <param name="lifetime">The optional client lifetime override.</param>
    /// <returns>The current builder context.</returns>
    /// <example>
    /// <code>
    /// context.RegisterClient("reports", (sp, options) => provider, providerName: "Custom");
    /// </code>
    /// </example>
    public BlobStorageBuilderContext RegisterClient(
        string name,
        Func<IServiceProvider, BlobStoreOptions, IBlobStoreProvider> providerFactory,
        Action<BlobStoreOptions> configure = null,
        string providerName = null,
        BlobStoreProviderCapabilities capabilities = null,
        ServiceLifetime? lifetime = null)
    {
        if (!this.Options.IsEnabled)
        {
            return this;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Blob store client name must not be null or whitespace.", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(providerFactory);

        if (this.Services.Any(descriptor =>
            descriptor.ServiceType == typeof(BlobStoreClientRegistration) &&
            descriptor.ImplementationInstance is BlobStoreClientRegistration registration &&
            string.Equals(registration.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Blob store client '{name}' is already registered.");
        }

        var options = new BlobStoreOptions();
        configure?.Invoke(options);
        var optionsResult = options.Validate();
        if (optionsResult.IsFailure)
        {
            throw new InvalidOperationException(optionsResult.Errors.FirstOrDefault()?.Message ?? "Blob store options are invalid.");
        }

        var resolvedProviderName = string.IsNullOrWhiteSpace(providerName) ? "Custom" : providerName;
        var resolvedCapabilities = capabilities ?? new BlobStoreProviderCapabilities();
        var resolvedLifetime = lifetime ?? this.Lifetime;

        this.Services.TryAddScoped<IBlobStoreClientFactory, BlobStoreClientFactory>();
        this.RegisterKeyedProvider(
            name,
            resolvedLifetime,
            (serviceProvider, _) => providerFactory(serviceProvider, options));
        this.RegisterKeyedClient(
            name,
            resolvedLifetime,
            (serviceProvider, _) => new BlobStoreClient(
                resolvedProviderName,
                serviceProvider.GetRequiredKeyedService<IBlobStoreProvider>(name),
                options,
                inner => this.ApplyBehaviors(name, serviceProvider, inner),
                serviceProvider.GetService<IContinuationTokenProtector>()));
        this.Services.AddSingleton(new BlobStoreClientRegistration
        {
            Name = name,
            ProviderName = resolvedProviderName,
            Capabilities = resolvedCapabilities,
            Lifetime = resolvedLifetime
        });
        this.Services.TryAddBlobStorageHealthCheck(tags: ["ready", "storage", "blobs"]);

        return this;
    }

    private void RegisterKeyedProvider(
        string name,
        ServiceLifetime lifetime,
        Func<IServiceProvider, object, IBlobStoreProvider> factory)
    {
        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                this.Services.AddKeyedSingleton(name, factory);
                break;
            case ServiceLifetime.Scoped:
                this.Services.AddKeyedScoped(name, factory);
                break;
            case ServiceLifetime.Transient:
                this.Services.AddKeyedTransient(name, factory);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "Unsupported service lifetime.");
        }
    }

    private void RegisterKeyedClient(
        string name,
        ServiceLifetime lifetime,
        Func<IServiceProvider, object, IBlobStoreClient> factory)
    {
        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                this.Services.AddKeyedSingleton(name, factory);
                break;
            case ServiceLifetime.Scoped:
                this.Services.AddKeyedScoped(name, factory);
                break;
            case ServiceLifetime.Transient:
                this.Services.AddKeyedTransient(name, factory);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "Unsupported service lifetime.");
        }
    }

    private IBlobStoreClient ApplyBehaviors(
        string name,
        IServiceProvider serviceProvider,
        IBlobStoreClient client)
    {
        foreach (var behavior in this.behaviors.AsEnumerable().Reverse())
        {
            client = behavior(client, serviceProvider, name);
        }

        return client;
    }
}
