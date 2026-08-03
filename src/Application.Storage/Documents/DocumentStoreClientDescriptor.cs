// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Describes one typed document-store client registered for dashboard selection.
/// </summary>
/// <param name="clientId">The stable client identifier used by dashboard requests.</param>
/// <param name="documentType">The CLR document type handled by the client.</param>
/// <param name="documentTypeName">The display name for the document type.</param>
/// <param name="providerName">The provider kind used by the client registration.</param>
/// <param name="capabilities">The query capabilities supported by the selected provider.</param>
/// <example>
/// <code>
/// var descriptor = new DocumentStoreClientDescriptor(
///     "myapp.person",
///     typeof(Person),
///     "Person",
///     "Entity Framework",
///     new DocumentStoreProviderCapabilities { RowKeyPrefixMatch = DocumentQuerySupport.SupportedServerSide });
/// </code>
/// </example>
public sealed class DocumentStoreClientDescriptor(
    string clientId,
    Type documentType,
    string documentTypeName,
    string providerName,
    DocumentStoreProviderCapabilities capabilities = null,
    string name = "default",
    bool isDefault = true,
    ServiceLifetime lifetime = ServiceLifetime.Scoped,
    DocumentTypeIdentity typeIdentity = default,
    IReadOnlyList<string> transformIdentifiers = null)
{
    /// <summary>
    /// Gets the stable client identifier used by dashboard requests.
    /// </summary>
    /// <example>
    /// <code>
    /// var id = descriptor.ClientId;
    /// </code>
    /// </example>
    public string ClientId { get; } = clientId;

    /// <summary>
    /// Gets the CLR document type handled by this client.
    /// </summary>
    /// <example>
    /// <code>
    /// var type = descriptor.DocumentType;
    /// </code>
    /// </example>
    public Type DocumentType { get; } = documentType;

    /// <summary>
    /// Gets the display name for the document type.
    /// </summary>
    /// <example>
    /// <code>
    /// var label = descriptor.DocumentTypeName;
    /// </code>
    /// </example>
    public string DocumentTypeName { get; } = documentTypeName;

    /// <summary>
    /// Gets the provider kind used by the client registration.
    /// </summary>
    /// <example>
    /// <code>
    /// var provider = descriptor.ProviderName;
    /// </code>
    /// </example>
    public string ProviderName { get; } = providerName;

    /// <summary>
    /// Gets the query capabilities supported by the selected provider.
    /// </summary>
    /// <example>
    /// <code>
    /// var supportsSuffix = descriptor.Capabilities.RowKeySuffixMatch != DocumentQuerySupport.Unsupported;
    /// </code>
    /// </example>
    public DocumentStoreProviderCapabilities Capabilities { get; } = capabilities ?? new DocumentStoreProviderCapabilities();

    /// <summary>Gets the normalized case-insensitive client name used by keyed dependency injection and operational surfaces.</summary>
    /// <example><code>var name = descriptor.Name;</code></example>
    public string Name { get; } = DocumentStorageBuilderContext.NormalizeName(name);

    /// <summary>Gets whether direct unkeyed <c>IDocumentStoreClient&lt;T&gt;</c> injection resolves this registration.</summary>
    /// <example><code>var isDefault = descriptor.IsDefault;</code></example>
    public bool IsDefault { get; } = isDefault;

    /// <summary>Gets the dependency-injection lifetime shared by the keyed provider and client graph.</summary>
    /// <example><code>var lifetime = descriptor.Lifetime;</code></example>
    public ServiceLifetime Lifetime { get; } = lifetime;

    /// <summary>Gets the stable persisted namespace and continuation-token type identity.</summary>
    /// <example><code>var typeIdentity = descriptor.TypeIdentity;</code></example>
    public DocumentTypeIdentity TypeIdentity { get; } = string.IsNullOrWhiteSpace(typeIdentity.Value)
        ? DocumentTypeIdentity.For(documentType)
        : typeIdentity;

    /// <summary>Gets the non-sensitive payload transform identifiers configured for this client.</summary>
    /// <example><code>foreach (var id in descriptor.TransformIdentifiers) { Console.WriteLine(id); }</code></example>
    public IReadOnlyList<string> TransformIdentifiers { get; } = transformIdentifiers?.ToArray() ?? [];
}
