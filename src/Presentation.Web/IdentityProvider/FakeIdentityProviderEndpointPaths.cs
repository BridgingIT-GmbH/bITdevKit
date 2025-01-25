// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

public class FakeIdentityProviderEndpointPaths
{
    public string Authorize { get; set; } = "/authorize";

    public string Token { get; set; } = "/token";

    public string UserInfo { get; set; } = "/userinfo";

    public string Logout { get; set; } = "/logout";

    public string WellKnownConfiguration { get; set; } = "/.well-known/openid-configuration";

    public string AuthorizeCallback { get; set; } = "/authorize/callback";

    public string DebugInfo { get; set; } = "/debuginfo";
}
