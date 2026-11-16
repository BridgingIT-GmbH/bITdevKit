// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage.Models;

using BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Describes a registered typed Document Storage client exposed through maintenance endpoints.
/// </summary>
/// <example><code>var model = new DocumentStorageClientInfoModel { ClientId = "orders", Name = "orders", DocumentTypeName = "OrderDocument", ProviderName = "InMemory" };</code></example>
public class DocumentStorageClientInfoModel
{
    /// <summary>Gets or sets the opaque registered client identifier used in maintenance routes.</summary>
    /// <example><code>var clientId = model.ClientId;</code></example>
    public string ClientId { get; set; }

    /// <summary>Gets or sets the configured document client name.</summary>
    /// <example><code>var name = model.Name;</code></example>
    public string Name { get; set; }

    /// <summary>Gets or sets the registered document type name.</summary>
    /// <example><code>var typeName = model.DocumentTypeName;</code></example>
    public string DocumentTypeName { get; set; }

    /// <summary>Gets or sets the provider name for diagnostics.</summary>
    /// <example><code>var providerName = model.ProviderName;</code></example>
    public string ProviderName { get; set; }

    /// <summary>Gets or sets the provider-neutral document storage capabilities.</summary>
    /// <example><code>var supportsConditions = model.Capabilities.SupportsConditionalWrite;</code></example>
    public DocumentStoreProviderCapabilities Capabilities { get; set; } = new();
}
