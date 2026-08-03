// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Resolves configured blob-store clients by store name.
/// </summary>
/// <example>
/// <code>
/// var client = factory.CreateClient("reports");
/// var registrations = factory.GetRegistrations();
/// </code>
/// </example>
public interface IBlobStoreClientFactory
{
    /// <summary>
    /// Creates the configured blob-store client for a store name.
    /// </summary>
    /// <param name="name">The configured store/client name.</param>
    /// <returns>The configured blob-store client.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the name is unknown.</exception>
    /// <example>
    /// <code>
    /// var client = factory.CreateClient("reports");
    /// </code>
    /// </example>
    IBlobStoreClient CreateClient(string name);

    /// <summary>
    /// Gets provider-neutral metadata for all configured blob-store clients.
    /// </summary>
    /// <returns>The configured blob-store client registrations.</returns>
    /// <example>
    /// <code>
    /// var names = factory.GetRegistrations().Select(registration => registration.Name);
    /// </code>
    /// </example>
    IReadOnlyCollection<BlobStoreClientRegistration> GetRegistrations();
}
