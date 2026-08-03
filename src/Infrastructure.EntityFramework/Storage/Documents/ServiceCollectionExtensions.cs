// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Storage;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides Entity Framework document-store service registration extensions.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers an Entity Framework backed document-store client within a top-level document-storage registration flow.
    /// </summary>
    /// <typeparam name="T">The document payload type.</typeparam>
    /// <typeparam name="TContext">The EF Core context that implements <see cref="IDocumentStoreContext" />.</typeparam>
    /// <param name="context">The document-storage builder context.</param>
    /// <param name="provider">An optional pre-built provider instance.</param>
    /// <param name="lifetime">The optional client lifetime override.</param>
    /// <param name="configure">An optional callback used to customize provider lease and retry options.</param>
    /// <param name="documentStoreOptions">The optional document-store query safety options.</param>
    /// <returns>The current document-storage builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddDocumentStorage(o => o.Enabled(true))
    ///     .WithBehavior&lt;LoggingDocumentStoreClientBehavior&lt;Person&gt;&gt;()
    ///     .WithEntityFrameworkClient&lt;Person, AppDbContext&gt;();
    /// </code>
    /// </example>
    public static DocumentStorageBuilderContext WithEntityFrameworkClient<T, TContext>(
        this DocumentStorageBuilderContext context,
        EntityFrameworkDocumentStoreProvider<TContext> provider = null,
        ServiceLifetime? lifetime = null,
        Action<EntityFrameworkDocumentStoreProviderOptions> configure = null,
        DocumentStoreOptions documentStoreOptions = null,
        string name = "default",
        bool isDefault = true)
        where T : class, new()
        where TContext : DbContext, IDocumentStoreContext
    {
        EnsureArg.IsNotNull(context, nameof(context));

        if (!context.Options.IsEnabled)
        {
            return context;
        }

        return context.RegisterProvider<T>(
            serviceProvider => provider ?? CreateProvider<TContext>(serviceProvider, configure, documentStoreOptions),
            "Entity Framework",
            capabilities: provider?.Capabilities ?? CreateEntityFrameworkCapabilities(),
            documentStoreOptions: documentStoreOptions,
            name: name,
            isDefault: isDefault,
            lifetime: lifetime);
    }

    private static EntityFrameworkDocumentStoreProvider<TContext> CreateProvider<TContext>(
        IServiceProvider serviceProvider,
        Action<EntityFrameworkDocumentStoreProviderOptions> configure,
        DocumentStoreOptions documentStoreOptions)
        where TContext : DbContext, IDocumentStoreContext
    {
        var options = new EntityFrameworkDocumentStoreProviderOptions
        {
            LoggerFactory = serviceProvider.GetService<ILoggerFactory>()
        };
        configure?.Invoke(options);

        return new EntityFrameworkDocumentStoreProvider<TContext>(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            documentStoreOptions);
    }

    private static DocumentStoreProviderCapabilities CreateEntityFrameworkCapabilities() => new()
    {
        FullMatch = DocumentQuerySupport.SupportedEfficiently,
        RowKeyPrefixMatch = DocumentQuerySupport.SupportedServerSide,
        RowKeySuffixMatch = DocumentQuerySupport.SupportedServerSide,
        FullScan = DocumentQuerySupport.SupportedServerSide,
        KeyListing = DocumentQuerySupport.SupportedEfficiently,
        SupportsContinuationPaging = true,
        SupportsServerSideCount = true,
        SupportsKeyOnlyProjection = true,
        SupportsConditionalWrite = true,
        SupportsConditionalDelete = true,
        SupportsAtomicPropertyUpdate = true,
        SupportsLogicalExpiration = true,
        SupportsRetention = true
    };
}
