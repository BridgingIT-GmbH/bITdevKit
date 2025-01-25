// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

public class FakeIdentityProviderClient
{
    public string ClientId { get; init; }

    public string Name { get; init; }

    public IReadOnlyList<string> RedirectUris { get; init; } = [];

    public IReadOnlyList<string> AllowedScopes { get; init; } =
    [
        "openid",
        "profile",
        "email",
        "roles",
        "offline_access"
    ];

    public bool IsConfidentialClient { get; init; } // for server applications, not SPAs

    public string ClientSecret { get; init; } // for server applications, not SPAs
}