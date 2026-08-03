// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Provides service-collection extensions for registering in-memory blob-store clients.
/// </summary>
/// <example>
/// <code>
/// services.AddBlobStorage()
///     .WithInMemoryClient("reports");
/// </code>
/// </example>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a named in-memory blob-store provider behind the default validating client.
    /// </summary>
    /// <param name="context">The blob-storage builder context.</param>
    /// <param name="name">The unique store/client name.</param>
    /// <param name="configure">The optional per-client options callback.</param>
    /// <param name="contextFactory">The optional in-memory context factory.</param>
    /// <param name="lifetime">The optional client lifetime override.</param>
    /// <returns>The current blob-storage builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddBlobStorage()
    ///     .WithInMemoryClient("reports");
    /// </code>
    /// </example>
    public static BlobStorageBuilderContext WithInMemoryClient(
        this BlobStorageBuilderContext context,
        string name,
        Action<BlobStoreOptions> configure = null,
        Func<IServiceProvider, InMemoryBlobStoreContext> contextFactory = null,
        ServiceLifetime? lifetime = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var sharedContext = new Lazy<InMemoryBlobStoreContext>(() => new InMemoryBlobStoreContext());
        return context.RegisterClient(
            name,
            (serviceProvider, options) => new InMemoryBlobStoreProvider(
                contextFactory?.Invoke(serviceProvider) ?? sharedContext.Value,
                options,
                serviceProvider.GetService<IContinuationTokenProtector>()),
            configure,
            InMemoryBlobStoreProvider.ProviderName,
            InMemoryBlobStoreProvider.CreateCapabilities(),
            lifetime);
    }
}
