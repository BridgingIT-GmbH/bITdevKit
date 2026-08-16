// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Represents fake identity provider endpoint paths.
/// </summary>
public class FakeIdentityProviderEndpointPaths
{
    /// <summary>
    /// Gets or sets the authorize.
    /// </summary>
    public string Authorize { get; set; } = "/authorize";

    /// <summary>
    /// Gets or sets the token.
    /// </summary>
    public string Token { get; set; } = "/token";

    /// <summary>
    /// Gets or sets the user info.
    /// </summary>
    public string UserInfo { get; set; } = "/userinfo";

    /// <summary>
    /// Gets or sets the logout.
    /// </summary>
    public string Logout { get; set; } = "/logout";

    /// <summary>
    /// Gets or sets the well known configuration.
    /// </summary>
    public string WellKnownConfiguration { get; set; } = "/.well-known/openid-configuration";

    /// <summary>
    /// Gets or sets the authorize callback.
    /// </summary>
    public string AuthorizeCallback { get; set; } = "/authorize/callback";

    /// <summary>
    /// Gets or sets the debug info.
    /// </summary>
    public string DebugInfo { get; set; } = "/debuginfo";
}
