// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Adds opt-in Storage Permalink behaviors to storage registrations.
/// </summary>
/// <example>
/// <code>
/// services.AddBlobStorage().WithPermalinks("reports");
/// </code>
/// </example>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Enables permalinks for one named Blob Storage client.
    /// </summary>
    public static BlobStorageBuilderContext WithPermalinks(this BlobStorageBuilderContext context, string clientName = "default")
    {
        ArgumentNullException.ThrowIfNull(context);

        var normalized = DocumentStorageBuilderContext.NormalizeName(clientName);

        return context.WithBehavior((inner, serviceProvider, name) => string.Equals(name, normalized, StringComparison.OrdinalIgnoreCase)
            ? new BlobStorePermalinkBehavior(inner, name, serviceProvider.GetRequiredService<IStoragePermalinkRegistry>(), serviceProvider.GetRequiredService<IStoragePermalinkChangeQueue>())
            : inner);
    }

    /// <summary>
    /// Enables permalinks for one named typed Document Storage client.
    /// </summary>
    public static DocumentStorageBuilderContext WithPermalinks<T>(this DocumentStorageBuilderContext context, string clientName = "default") where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(context);

        var normalized = DocumentStorageBuilderContext.NormalizeName(clientName);

        return context.WithBehavior<T, IDocumentStoreClient<T>>((inner, serviceProvider) =>
            inner is IDocumentStoreClientIdentity identity && string.Equals(identity.ClientName, normalized, StringComparison.OrdinalIgnoreCase)
                ? new DocumentStorePermalinkBehavior<T>(inner, normalized, serviceProvider.GetRequiredService<IStoragePermalinkRegistry>(), serviceProvider.GetRequiredService<IStoragePermalinkChangeQueue>())
                : inner);
    }
}
