// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Storage;
using Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Provides service-collection extensions for registering document-store clients.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    private const string DocumentStorageMcpDispatcherServiceTypeName = "BridgingIT.DevKit.Presentation.Web.McpDispatcher";

    /// <summary>
    /// Starts a top-level fluent document-storage registration flow.
    /// </summary>
    /// <param name="services">The service collection to update.</param>
    /// <param name="configure">An optional callback used to configure document-storage registration.</param>
    /// <param name="configuration">The optional configuration root available to provider extensions.</param>
    /// <returns>The document-storage builder used to register clients and behaviors.</returns>
    /// <example>
    /// <code>
    /// services.AddDocumentStorage(o => o.Enabled(true))
    ///     .WithBehavior&lt;LoggingDocumentStoreClientBehavior&lt;Person&gt;&gt;()
    ///     .WithProvider&lt;Person&gt;(sp =>
    ///         new InMemoryDocumentStoreProvider(sp.GetRequiredService&lt;ILoggerFactory&gt;()));
    /// </code>
    /// </example>
    public static DocumentStorageBuilderContext AddDocumentStorage(
        this IServiceCollection services,
        Action<DocumentStorageOptions> configure = null,
        IConfiguration configuration = null)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        var options = new DocumentStorageOptions();
        configure?.Invoke(options);

        services.Replace(ServiceDescriptor.Singleton(options));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IDocumentStorageDiagnosticsService, DocumentStorageDiagnosticsService>();
        services.TryAddDocumentStorageMcpHandler();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, DocumentRetentionBackgroundService>());
        services.TryAddSingleton(serviceProvider => serviceProvider
            .GetServices<IHostedService>()
            .OfType<DocumentRetentionBackgroundService>()
            .Single());

        if (options.IsEnabled)
        {
            services.TryAddSingleton(new DocumentStorageFeature { IsEnabled = true });
        }

        return new DocumentStorageBuilderContext(services, options, configuration);
    }

    private static void TryAddDocumentStorageMcpHandler(this IServiceCollection services)
    {
        if (!services.Any(descriptor => string.Equals(
            descriptor.ServiceType.FullName,
            DocumentStorageMcpDispatcherServiceTypeName,
            StringComparison.Ordinal)))
        {
            return;
        }

        services.TryAddEnumerable(ServiceDescriptor.Transient<IMcpHandler, DocumentStorageMcpHandler>());
    }

    /// <summary>
    /// Registers a custom document-store provider within a top-level document-storage registration flow.
    /// </summary>
    /// <typeparam name="T">The document type handled by the client.</typeparam>
    /// <param name="context">The document-storage builder context.</param>
    /// <param name="providerFactory">The factory used to create the container-owned persistence provider.</param>
    /// <param name="lifetime">The optional service lifetime override for this client.</param>
    /// <param name="capabilities">The optional provider capabilities used by dashboard selection and query safety hints.</param>
    /// <returns>The current document-storage builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddDocumentStorage()
    ///     .WithBehavior&lt;LoggingDocumentStoreClientBehavior&lt;Person&gt;&gt;()
    ///     .WithProvider&lt;Person&gt;(sp =>
    ///         new InMemoryDocumentStoreProvider(sp.GetRequiredService&lt;ILoggerFactory&gt;()));
    /// </code>
    /// </example>
    public static DocumentStorageBuilderContext WithProvider<T>(
        this DocumentStorageBuilderContext context,
        Func<IServiceProvider, IDocumentStoreProvider> providerFactory,
        ServiceLifetime? lifetime = null,
        DocumentStoreProviderCapabilities capabilities = null,
        DocumentStoreOptions documentStoreOptions = null,
        string name = "default",
        bool isDefault = true)
        where T : class, new()
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(providerFactory, nameof(providerFactory));

        if (!context.Options.IsEnabled)
        {
            return context;
        }

        return context.RegisterProvider<T>(providerFactory, "Custom", capabilities: capabilities,
            documentStoreOptions: documentStoreOptions, name: name, isDefault: isDefault, lifetime: lifetime);
    }

    /// <summary>Adds gzip compression to the payload pipeline for one document type.</summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="context">The Document Storage builder.</param>
    /// <returns>The current builder.</returns>
    /// <example><code>services.AddDocumentStorage().WithCompressionTransform&lt;Person&gt;();</code></example>
    public static DocumentStorageBuilderContext WithCompressionTransform<T>(this DocumentStorageBuilderContext context)
        where T : class, new() => context.WithTransform<T>(_ => new CompressionDocumentPayloadTransform(), "gzip");

    /// <summary>Adds key-provider-backed encryption to the payload pipeline for one document type.</summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="context">The Document Storage builder.</param>
    /// <returns>The current builder.</returns>
    /// <example><code>services.AddDocumentStorage().WithEncryptionTransform&lt;Person&gt;();</code></example>
    public static DocumentStorageBuilderContext WithEncryptionTransform<T>(this DocumentStorageBuilderContext context)
        where T : class, new() => context.WithTransform<T>(serviceProvider =>
            new EncryptionDocumentPayloadTransform(serviceProvider.GetRequiredService<IEncryptionKeyProvider>()), "aes-cbc-pkcs7");
}
