// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.IntegrationTests;

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>Test-host-only authentication; production authentication is unchanged.</summary>
/// <example><code>client.DefaultRequestHeaders.Add("X-Test-User", "fictional-user");</code></example>
public sealed class TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>The isolated test scheme name.</summary>
    /// <example><code>var scheme = TestAuthenticationHandler.SCHEME;</code></example>
    public const string SCHEME = "NdjsonTest";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var user = this.Request.Headers["X-Test-User"].ToString();
        if (string.IsNullOrEmpty(user)) { return Task.FromResult(AuthenticateResult.NoResult()); }

        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, user), new Claim("sub", user),
            new Claim(ClaimTypes.Name, "NDJSON fictional user"),
            new Claim(ClaimTypes.Role, this.Request.Headers["X-Test-Admin"] == "true" ? Role.Administrators : "User")
        ], SCHEME));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SCHEME)));
    }
}
