// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Scrutor;

/// <summary>
/// Provides top-level fluent configuration for document-storage clients and shared client behaviors.
/// </summary>
/// <param name="services">The service collection being configured.</param>
/// <param name="options">The document-storage options.</param>
/// <param name="configuration">The optional application configuration used by provider extensions.</param>
/// <example>
/// <code>
/// services.AddDocumentStorage(o => o.Enabled())
///     .WithBehavior&lt;LoggingDocumentStoreClientBehavior&lt;Person&gt;&gt;()
///     .WithEntityFrameworkClient&lt;Person, AppDbContext&gt;();
/// </code>
/// </example>
public sealed class DocumentStorageBuilderContext(
    IServiceCollection services,
    DocumentStorageOptions options,
    IConfiguration configuration = null)
{
    private readonly List<DocumentStorageBehaviorRegistration> behaviors = [];
    private readonly Dictionary<Type, ServiceDescriptor> clientDescriptors = [];
    private readonly HashSet<Type> registeredDocumentTypes = [];

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
    /// Gets the document-storage options for this registration flow.
    /// </summary>
    /// <example>
    /// <code>
    /// var enabled = context.Options.IsEnabled;
    /// </code>
    /// </example>
    public DocumentStorageOptions Options { get; } = options ?? new DocumentStorageOptions();

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
    /// Registers a document-store client behavior for the document type handled by <typeparamref name="TBehavior" />.
    /// </summary>
    /// <typeparam name="TBehavior">The closed behavior type implementing <see cref="IDocumentStoreClient{T}" />.</typeparam>
    /// <param name="providerName">The provider label shown in dashboard client selection.</param>
    /// <param name="displayName">The optional display name for the document type.</param>
    /// <returns>The current builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddDocumentStorage()
    ///     .WithBehavior&lt;LoggingDocumentStoreClientBehavior&lt;Person&gt;&gt;()
    ///     .WithEntityFrameworkClient&lt;Person, AppDbContext&gt;();
    /// </code>
    /// </example>
    public DocumentStorageBuilderContext WithBehavior<TBehavior>()
        where TBehavior : class
    {
        var documentType = GetDocumentType(typeof(TBehavior));
        this.behaviors.Add(new DocumentStorageBehaviorRegistration(
            documentType,
            services => services.Decorate(typeof(IDocumentStoreClient<>).MakeGenericType(documentType), typeof(TBehavior))));

        if (this.registeredDocumentTypes.Contains(documentType))
        {
            this.ApplyClientBehaviors(documentType);
        }

        return this;
    }

    /// <summary>
    /// Registers a document-store client behavior using a factory that receives the current inner client.
    /// </summary>
    /// <typeparam name="T">The document type handled by the decorated client.</typeparam>
    /// <typeparam name="TBehavior">The behavior type that decorates <see cref="IDocumentStoreClient{T}" />.</typeparam>
    /// <param name="behavior">A factory that creates the behavior around the current inner client.</param>
    /// <returns>The current builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddDocumentStorage()
    ///     .WithBehavior&lt;Person, MetricsDocumentStoreClientBehavior&lt;Person&gt;&gt;(
    ///         inner => new MetricsDocumentStoreClientBehavior&lt;Person&gt;(inner, metrics))
    ///     .WithEntityFrameworkClient&lt;Person, AppDbContext&gt;();
    /// </code>
    /// </example>
    public DocumentStorageBuilderContext WithBehavior<T, TBehavior>(Func<IDocumentStoreClient<T>, TBehavior> behavior)
        where T : class, new()
        where TBehavior : notnull, IDocumentStoreClient<T>
    {
        EnsureArg.IsNotNull(behavior, nameof(behavior));

        this.behaviors.Add(new DocumentStorageBehaviorRegistration(
            typeof(T),
            services => services.Decorate<IDocumentStoreClient<T>>((inner, _) => behavior(inner))));

        if (this.registeredDocumentTypes.Contains(typeof(T)))
        {
            this.ApplyClientBehaviors<T>();
        }

        return this;
    }

    /// <summary>
    /// Registers a document-store client behavior using a factory that receives the current inner client and service provider.
    /// </summary>
    /// <typeparam name="T">The document type handled by the decorated client.</typeparam>
    /// <typeparam name="TBehavior">The behavior type that decorates <see cref="IDocumentStoreClient{T}" />.</typeparam>
    /// <param name="behavior">A factory that creates the behavior around the current inner client.</param>
    /// <returns>The current builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddDocumentStorage()
    ///     .WithBehavior&lt;Person, MetricsDocumentStoreClientBehavior&lt;Person&gt;&gt;(
    ///         (inner, sp) => new MetricsDocumentStoreClientBehavior&lt;Person&gt;(
    ///             inner,
    ///             sp.GetRequiredService&lt;IMetrics&gt;()))
    ///     .WithEntityFrameworkClient&lt;Person, AppDbContext&gt;();
    /// </code>
    /// </example>
    public DocumentStorageBuilderContext WithBehavior<T, TBehavior>(
        Func<IDocumentStoreClient<T>, IServiceProvider, TBehavior> behavior)
        where T : class, new()
        where TBehavior : notnull, IDocumentStoreClient<T>
    {
        EnsureArg.IsNotNull(behavior, nameof(behavior));

        this.behaviors.Add(new DocumentStorageBehaviorRegistration(
            typeof(T),
            services => services.Decorate<IDocumentStoreClient<T>>((inner, sp) => behavior(inner, sp))));

        if (this.registeredDocumentTypes.Contains(typeof(T)))
        {
            this.ApplyClientBehaviors<T>();
        }

        return this;
    }

    /// <summary>
    /// Records that a client for <typeparamref name="T" /> has been registered and applies matching behaviors.
    /// </summary>
    /// <typeparam name="T">The document type handled by the registered client.</typeparam>
    /// <param name="providerName">The provider label shown in dashboard client selection.</param>
    /// <param name="displayName">The optional display name for the document type.</param>
    /// <param name="capabilities">The provider capabilities used by dashboard selection and query safety hints.</param>
    /// <returns>The current builder context.</returns>
    /// <example>
    /// <code>
    /// context.RegisterClient&lt;Person&gt;();
    /// </code>
    /// </example>
    public DocumentStorageBuilderContext RegisterClient<T>(
        string providerName = null,
        string displayName = null,
        DocumentStoreProviderCapabilities capabilities = null)
        where T : class, new()
    {
        if (!this.Options.IsEnabled)
        {
            return this;
        }

        var clientId = CreateClientId<T>();

        this.Services.TryAddScoped<IDocumentStoreClientFactory, DocumentStoreClientFactory>();
        this.Services.AddSingleton(new DocumentStoreClientDescriptor(
            clientId,
            typeof(T),
            displayName ?? typeof(T).PrettyName(),
            providerName ?? "Custom",
            capabilities ?? CreateDefaultCapabilities()));
        this.Services.TryAddDocumentStorageHealthCheck(tags: ["ready", "storage", "documents"]);

        this.registeredDocumentTypes.Add(typeof(T));
        this.ApplyClientBehaviors<T>();

        return this;
    }

    private void ApplyClientBehaviors(Type documentType)
    {
        var method = typeof(DocumentStorageBuilderContext)
            .GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Single(m => m.Name == nameof(ApplyClientBehaviors) &&
                m.IsGenericMethodDefinition &&
                m.GetParameters().Length == 0)
            ?.MakeGenericMethod(documentType);

        method?.Invoke(this, null);
    }

    private void ApplyClientBehaviors<T>()
        where T : class, new()
    {
        var documentType = typeof(T);
        var matchingBehaviors = this.behaviors
            .Where(behavior => behavior.DocumentType == documentType)
            .ToList();

        if (matchingBehaviors.Count == 0)
        {
            return;
        }

        var clientDescriptor = this.clientDescriptors.GetValueOrDefault(documentType);
        if (clientDescriptor is null)
        {
            clientDescriptor = this.Services.Find<IDocumentStoreClient<T>>();
            if (clientDescriptor is null)
            {
                throw new InvalidOperationException(
                    $"Cannot register behaviors for {typeof(IDocumentStoreClient<T>).PrettyName()} as it has not been registered.");
            }

            this.clientDescriptors[documentType] = clientDescriptor;
        }

        var descriptorIndex = this.Services.IndexOf<IDocumentStoreClient<T>>();
        if (descriptorIndex == -1)
        {
            return;
        }

        this.Services[descriptorIndex] = clientDescriptor;

        foreach (var descriptor in this.Services
                     .Where(s => s.ServiceType is DecoratedType &&
                         s.ServiceType.ImplementsInterface(typeof(IDocumentStoreClient<T>)))
                     .ToList())
        {
            this.Services.Remove(descriptor);
        }

        foreach (var behavior in matchingBehaviors.AsEnumerable().Reverse())
        {
            behavior.Apply(this.Services);
        }
    }

    private static Type GetDocumentType(Type behaviorType)
    {
        var documentTypes = behaviorType
            .GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDocumentStoreClient<>))
            .Select(i => i.GetGenericArguments()[0])
            .Distinct()
            .ToList();

        return documentTypes.Count switch
        {
            1 => documentTypes[0],
            0 => throw new ArgumentException(
                $"Document-store behavior '{behaviorType.PrettyName()}' must implement {typeof(IDocumentStoreClient<>).PrettyName()}.",
                nameof(behaviorType)),
            _ => throw new ArgumentException(
                $"Document-store behavior '{behaviorType.PrettyName()}' must implement exactly one closed {typeof(IDocumentStoreClient<>).PrettyName()}.",
                nameof(behaviorType))
        };
    }

    private static string CreateClientId<T>() => typeof(T).FullName?.ToLowerInvariant() ?? typeof(T).Name.ToLowerInvariant();

    private static DocumentStoreProviderCapabilities CreateDefaultCapabilities() => new()
    {
        FullMatch = DocumentQuerySupport.SupportedServerSide,
        RowKeyPrefixMatch = DocumentQuerySupport.SupportedServerSide,
        RowKeySuffixMatch = DocumentQuerySupport.Unsupported,
        FullScan = DocumentQuerySupport.SupportedServerSide,
        KeyListing = DocumentQuerySupport.SupportedServerSide,
        SupportsContinuationPaging = true,
        SupportsServerSideCount = false,
        SupportsKeyOnlyProjection = false
    };

    private sealed class DocumentStorageBehaviorRegistration(Type documentType, Action<IServiceCollection> apply)
    {
        public Type DocumentType { get; } = documentType;

        public void Apply(IServiceCollection services)
        {
            apply(services);
        }
    }
}
