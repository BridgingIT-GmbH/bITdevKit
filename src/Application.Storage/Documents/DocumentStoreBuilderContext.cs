// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using Configuration;
using Scrutor;

/// <summary>
/// Provides fluent registration helpers for an <see cref="IDocumentStoreClient{T}" /> and its decorator pipeline.
/// </summary>
/// <typeparam name="T">The document type handled by the registered client.</typeparam>
/// <param name="services">The service collection being configured.</param>
/// <param name="lifetime">The service lifetime of the registered client.</param>
/// <param name="configuration">The optional application configuration used by downstream builder extensions.</param>
/// <example>
/// <code>
/// services.AddDocumentStoreClient&lt;Person&gt;(sp =>
///     new DocumentStoreClient&lt;Person&gt;(new InMemoryDocumentStoreProvider(sp.GetRequiredService&lt;ILoggerFactory&gt;())))
///     .WithBehavior&lt;LoggingDocumentStoreClientBehavior&lt;Person&gt;&gt;()
///     .WithBehavior((inner, sp) => new RetryDocumentStoreClientBehavior&lt;Person&gt;(
///         sp.GetRequiredService&lt;ILoggerFactory&gt;(),
///         inner));
/// </code>
/// </example>
public class DocumentStoreBuilderContext<T>(
    IServiceCollection services,
    ServiceLifetime lifetime = ServiceLifetime.Scoped,
    IConfiguration configuration = null)
    where T : class, new()
{
    private readonly List<Action<IServiceCollection>> behaviors = [];
    private ServiceDescriptor clientDescriptor;

    /// <summary>
    /// Gets the service collection being configured.
    /// </summary>
    public IServiceCollection Services { get; } = services;

    /// <summary>
    /// Gets the service lifetime that will be used for the registered document-store client.
    /// </summary>
    public ServiceLifetime Lifetime { get; } = lifetime;

    /// <summary>
    /// Gets the optional configuration root available to builder extensions.
    /// </summary>
    public IConfiguration Configuration { get; } = configuration;

    /// <summary>
    /// Registers a decorator that will wrap the current <see cref="IDocumentStoreClient{T}" />.
    /// </summary>
    /// <typeparam name="TBehavior">The decorator type to add.</typeparam>
    /// <returns>The current builder context so additional behaviors can be chained.</returns>
    /// <example>
    /// <code>
    /// services.AddEntityFrameworkDocumentStoreClient&lt;Person, AppDbContext&gt;()
    ///     .WithBehavior&lt;LoggingDocumentStoreClientBehavior&lt;Person&gt;&gt;()
    ///     .WithBehavior&lt;TimeoutDocumentStoreClientBehavior&lt;Person&gt;&gt;();
    /// </code>
    /// </example>
    public DocumentStoreBuilderContext<T> WithBehavior<TBehavior>()
        where TBehavior : class, IDocumentStoreClient<T>
    {
        this.behaviors.Add(s => s.Decorate<IDocumentStoreClient<T>, TBehavior>());
        this.RegisterBehaviors();

        return this;
    }

    /// <summary>
    /// Registers a decorator using a factory that receives the current inner client.
    /// </summary>
    /// <typeparam name="TBehavior">The decorator type to add.</typeparam>
    /// <param name="behavior">A factory that creates the decorator around the current inner client.</param>
    /// <returns>The current builder context so additional behaviors can be chained.</returns>
    public DocumentStoreBuilderContext<T> WithBehavior<TBehavior>(Func<IDocumentStoreClient<T>, TBehavior> behavior)
        where TBehavior : notnull, IDocumentStoreClient<T>
    {
        EnsureArg.IsNotNull(behavior, nameof(behavior));

        this.behaviors.Add(s => s.Decorate<IDocumentStoreClient<T>>((service, _) => behavior(service)));
        this.RegisterBehaviors();

        return this;
    }

    /// <summary>
    /// Registers a decorator using a factory that receives the current inner client and the active service provider.
    /// </summary>
    /// <typeparam name="TBehavior">The decorator type to add.</typeparam>
    /// <param name="behavior">A factory that creates the decorator around the current inner client.</param>
    /// <returns>The current builder context so additional behaviors can be chained.</returns>
    public DocumentStoreBuilderContext<T> WithBehavior<TBehavior>(
        Func<IDocumentStoreClient<T>, IServiceProvider, TBehavior> behavior)
        where TBehavior : notnull, IDocumentStoreClient<T>
    {
        EnsureArg.IsNotNull(behavior, nameof(behavior));

        this.behaviors.Add(s => s.Decorate<IDocumentStoreClient<T>>((inner, sp) => behavior(inner, sp)));
        this.RegisterBehaviors();

        return this;
    }

    /// <summary>
    ///     Registers all recorded behaviors (decorators). Before registering all existing behavior registrations are removed.
    ///     This needs to be done to apply the registrations in reverse order.
    /// </summary>
    private IServiceCollection RegisterBehaviors()
    {
        // reset the repo registration to the original implementation, as scrutor changes the implementation
        this.clientDescriptor ??= this.Services.Find<IDocumentStoreClient<T>>();
        if (this.clientDescriptor is null)
        {
            throw new Exception(
                $"Cannot register behaviors for {typeof(IDocumentStoreClient<T>).PrettyName()} as it has not been registerd.");
        }

        var descriptorIndex = this.Services.IndexOf<IDocumentStoreClient<T>>();
        if (descriptorIndex != -1)
        {
            this.Services[descriptorIndex] = this.clientDescriptor;
        }
        else
        {
            return this.Services;
        }

        foreach (var descriptor in this.Services.Where(s =>
                         s.ServiceType is DecoratedType &&
                         s.ServiceType.ImplementsInterface(typeof(IDocumentStoreClient<T>)))
                     ?.ToList())
        {
            this.Services.Remove(descriptor); // remove the registered behavior
        }

        // register all behaviors in reverse order (first...last)
        foreach (var behavior in this.behaviors.AsEnumerable().Reverse())
        {
            behavior.Invoke(this.Services);
        }

        return this.Services;
    }
}
