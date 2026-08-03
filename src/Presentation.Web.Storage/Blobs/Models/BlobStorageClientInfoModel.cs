// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage.Models;

using BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Represents a registered Blob Storage client exposed through maintenance endpoints.
/// </summary>
/// <example>
/// <code>
/// var model = new BlobStorageClientInfoModel { Name = "reports", ProviderName = "InMemory" };
/// </code>
/// </example>
public class BlobStorageClientInfoModel
{
    /// <summary>
    /// Gets or sets the configured blob client name.
    /// </summary>
    /// <example>
    /// <code>
    /// var name = model.Name;
    /// </code>
    /// </example>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the provider name for diagnostics.
    /// </summary>
    /// <example>
    /// <code>
    /// var provider = model.ProviderName;
    /// </code>
    /// </example>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the provider-neutral capabilities.
    /// </summary>
    /// <example>
    /// <code>
    /// var supportsPrefix = model.Capabilities.SupportsPrefixListing;
    /// </code>
    /// </example>
    public BlobStoreProviderCapabilities Capabilities { get; set; } = new();
}
