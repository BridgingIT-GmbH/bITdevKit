// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Creates dashboard accessors for registered typed document-store clients.
/// </summary>
/// <example>
/// <code>
/// var descriptor = factory.GetDescriptors().FirstOrDefault();
/// var accessor = factory.Create(descriptor.ClientId);
/// </code>
/// </example>
public interface IDocumentStoreClientFactory
{
    /// <summary>
    /// Gets the registered typed document-store clients available for selection.
    /// </summary>
    /// <returns>The registered client descriptors.</returns>
    /// <example>
    /// <code>
    /// foreach (var descriptor in factory.GetDescriptors())
    /// {
    ///     Console.WriteLine(descriptor.DocumentTypeName);
    /// }
    /// </code>
    /// </example>
    IReadOnlyList<DocumentStoreClientDescriptor> GetDescriptors();

    /// <summary>
    /// Creates an accessor for the selected client id.
    /// </summary>
    /// <param name="clientId">The selected client id.</param>
    /// <returns>The selected client accessor, or <c>null</c> when the id is unknown.</returns>
    /// <example>
    /// <code>
    /// var accessor = factory.Create("myapp.person");
    /// </code>
    /// </example>
    IDocumentStoreClientAccessor Create(string clientId);

    /// <summary>Resolves a named typed document client.</summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="name">The registered client name.</param>
    /// <returns>The named client, or null when it is not registered.</returns>
    /// <example><code>var archive = factory.CreateClient&lt;Person&gt;("archive");</code></example>
    IDocumentStoreClient<T> CreateClient<T>(string name) where T : class, new();
}
