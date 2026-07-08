// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

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
    DocumentStoreProviderCapabilities capabilities = null)
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
}
