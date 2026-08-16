// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Represents fake identity provider client.
/// </summary>
public class FakeIdentityProviderClient
{
    /// <summary>
    /// Gets or sets the client id.
    /// </summary>
    public string ClientId { get; init; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Gets or sets the redirect uris.
    /// </summary>
    public IReadOnlyList<string> RedirectUris { get; init; } = [];

    /// <summary>
    /// Gets or sets the allowed scopes.
    /// </summary>
    public IReadOnlyList<string> AllowedScopes { get; init; } =
    [
        "openid",
        "profile",
        "email",
        "roles",
        "offline_access"
    ];

    /// <summary>
    /// Gets or sets the is confidential client.
    /// </summary>
    public bool IsConfidentialClient { get; init; } // for server applications, not SPAs

    /// <summary>
    /// Gets or sets the client secret.
    /// </summary>
    public string ClientSecret { get; init; } // for server applications, not SPAs
}
