// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Provides service-collection extensions for registering named blob-store clients.
/// </summary>
/// <example>
/// <code>
/// services.AddBlobStorage()
///     .WithClient("reports", sp => provider);
/// </code>
/// </example>
public static partial class ServiceCollectionExtensions
{
    private const string McpDispatcherServiceTypeName = "BridgingIT.DevKit.Presentation.Web.McpDispatcher";

    /// <summary>
    /// Starts a top-level fluent blob-storage registration flow.
    /// </summary>
    /// <param name="services">The service collection to update.</param>
    /// <param name="configure">An optional callback used to configure blob-storage registration.</param>
    /// <param name="configuration">The optional configuration root available to provider extensions.</param>
    /// <returns>The blob-storage builder used to register named clients.</returns>
    /// <example>
    /// <code>
    /// services.AddBlobStorage(options => options.Enabled(true))
    ///     .WithClient("reports", sp => provider);
    /// </code>
    /// </example>
    public static BlobStorageBuilderContext AddBlobStorage(
        this IServiceCollection services,
        Action<BlobStorageOptions> configure = null,
        IConfiguration configuration = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new BlobStorageOptions();
        configure?.Invoke(options);

        services.Replace(ServiceDescriptor.Singleton(options));
        services.TryAddBlobStorageDiagnostics();
        services.TryAddBlobStorageMcpHandler();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, BlobRetentionBackgroundService>());
        services.TryAddSingleton(sp => sp
            .GetServices<IHostedService>()
            .OfType<BlobRetentionBackgroundService>()
            .Single());

        return new BlobStorageBuilderContext(services, options, configuration);
    }

    /// <summary>
    /// Registers a named custom blob-store provider behind the default validating client.
    /// </summary>
    /// <param name="context">The blob-storage builder context.</param>
    /// <param name="name">The unique store/client name.</param>
    /// <param name="providerFactory">The provider factory.</param>
    /// <param name="configure">The optional per-client options callback.</param>
    /// <param name="providerName">The provider label used for diagnostics and continuation-token binding.</param>
    /// <param name="capabilities">The provider capabilities exposed for diagnostics.</param>
    /// <param name="lifetime">The optional client lifetime override.</param>
    /// <returns>The current blob-storage builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddBlobStorage()
    ///     .WithClient("reports", sp => provider, providerName: "Custom");
    /// </code>
    /// </example>
    public static BlobStorageBuilderContext WithClient(
        this BlobStorageBuilderContext context,
        string name,
        Func<IServiceProvider, IBlobStoreProvider> providerFactory,
        Action<BlobStoreOptions> configure = null,
        string providerName = null,
        BlobStoreProviderCapabilities capabilities = null,
        ServiceLifetime? lifetime = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.RegisterClient(
            name,
            providerFactory,
            configure,
            providerName,
            capabilities,
            lifetime);
    }

    private static void TryAddBlobStorageMcpHandler(this IServiceCollection services)
    {
        if (!services.Any(descriptor => string.Equals(
            descriptor.ServiceType.FullName,
            McpDispatcherServiceTypeName,
            StringComparison.Ordinal)))
        {
            return;
        }

        services.TryAddEnumerable(ServiceDescriptor.Transient<IMcpHandler, BlobStorageMcpHandler>());
    }
}
