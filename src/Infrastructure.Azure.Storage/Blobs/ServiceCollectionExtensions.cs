// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Infrastructure.Azure;

/// <summary>
/// Provides Azure Blob Storage registration extensions for provider-neutral blob storage.
/// </summary>
/// <example>
/// <code>
/// services.AddBlobStorage()
///     .WithAzureBlobClient("reports", blobServiceClient);
/// </code>
/// </example>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a named Azure Blob Storage client.
    /// </summary>
    /// <param name="context">The blob-storage builder context.</param>
    /// <param name="name">The named blob-store client.</param>
    /// <param name="serviceClient">The optional Azure Blob service client. When null, the client is resolved from services.</param>
    /// <param name="configure">The optional per-client blob-store options callback.</param>
    /// <param name="lifetime">The optional client lifetime override.</param>
    /// <returns>The current blob-storage builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddBlobStorage()
    ///     .WithAzureBlobClient("reports", blobServiceClient, options => options.AllowFullScans = true);
    /// </code>
    /// </example>
    public static BlobStorageBuilderContext WithAzureBlobClient(
        this BlobStorageBuilderContext context,
        string name,
        BlobServiceClient serviceClient = null,
        Action<BlobStoreOptions> configure = null,
        ServiceLifetime? lifetime = null)
    {
        EnsureArg.IsNotNull(context, nameof(context));

        return context.RegisterClient(
            name,
            (serviceProvider, options) => new AzureBlobStoreProvider(
                serviceClient ?? serviceProvider.GetRequiredService<BlobServiceClient>(),
                options,
                serviceProvider.GetService<IContinuationTokenProtector>()),
            configure,
            AzureBlobStoreProvider.ProviderName,
            AzureBlobStoreProvider.CreateCapabilities(),
            lifetime);
    }
}
