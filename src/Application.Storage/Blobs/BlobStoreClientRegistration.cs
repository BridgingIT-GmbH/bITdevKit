// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Describes one named blob-store client registration.
/// </summary>
/// <example>
/// <code>
/// var registration = new BlobStoreClientRegistration
/// {
///     Name = "reports",
///     ProviderName = "Custom",
///     Capabilities = new BlobStoreProviderCapabilities()
/// };
/// </code>
/// </example>
public sealed class BlobStoreClientRegistration
{
    /// <summary>
    /// Gets or initializes the configured store/client name.
    /// </summary>
    /// <example>
    /// <code>
    /// var name = registration.Name;
    /// </code>
    /// </example>
    public string Name { get; init; }

    /// <summary>
    /// Gets or initializes the provider label for diagnostics.
    /// </summary>
    /// <example>
    /// <code>
    /// var provider = registration.ProviderName;
    /// </code>
    /// </example>
    public string ProviderName { get; init; }

    /// <summary>
    /// Gets or initializes the provider capabilities for diagnostics and tooling.
    /// </summary>
    /// <example>
    /// <code>
    /// var supportsPrefix = registration.Capabilities.SupportsPrefixListing;
    /// </code>
    /// </example>
    public BlobStoreProviderCapabilities Capabilities { get; init; } = new();

    /// <summary>
    /// Gets or initializes the service lifetime used when resolving the registered client.
    /// </summary>
    /// <example>
    /// <code>
    /// var lifetime = registration.Lifetime;
    /// </code>
    /// </example>
    public ServiceLifetime Lifetime { get; init; } = ServiceLifetime.Scoped;

}
