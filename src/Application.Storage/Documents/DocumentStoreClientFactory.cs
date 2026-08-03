// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Resolves keyed typed clients and dashboard accessors without constructing or caching them.
/// </summary>
/// <param name="serviceProvider">The service provider used to resolve typed clients.</param>
/// <param name="descriptors">The registered document-store client descriptors.</param>
/// <example>
/// <code>
/// var accessor = factory.Create("myapp.person");
/// var page = await accessor.ListPageAsync(query, cancellationToken);
/// </code>
/// </example>
public sealed class DocumentStoreClientFactory(
    IServiceProvider serviceProvider,
    IEnumerable<DocumentStoreClientDescriptor> descriptors) : IDocumentStoreClientFactory
{
    private readonly IReadOnlyList<DocumentStoreClientDescriptor> descriptors = descriptors
        ?.OrderBy(e => e.DocumentTypeName, StringComparer.OrdinalIgnoreCase)
        .ThenBy(e => e.ProviderName, StringComparer.OrdinalIgnoreCase)
        .ToArray() ?? [];

    /// <inheritdoc />
    public IReadOnlyList<DocumentStoreClientDescriptor> GetDescriptors() => this.descriptors;

    /// <inheritdoc />
    public IDocumentStoreClientAccessor Create(string clientId)
    {
        var descriptor = this.descriptors.FirstOrDefault(e =>
            string.Equals(e.ClientId, clientId, StringComparison.OrdinalIgnoreCase));
        if (descriptor is null)
        {
            return null;
        }

        var key = new DocumentStoreServiceKey(descriptor.DocumentType, descriptor.Name);
        return serviceProvider.GetKeyedService<IDocumentStoreClientAccessor>(key);
    }

    /// <inheritdoc />
    public IDocumentStoreClient<T> CreateClient<T>(string name) where T : class, new() =>
        serviceProvider.GetKeyedService<IDocumentStoreClient<T>>(
            new DocumentStoreServiceKey(typeof(T), DocumentStorageBuilderContext.NormalizeName(name)));
}
