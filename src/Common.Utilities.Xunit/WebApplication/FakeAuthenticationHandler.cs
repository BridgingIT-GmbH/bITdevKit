// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class FakeAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions,
    ILoggerFactory logger,
    UrlEncoder encoder,
    FakeAuthenticationHandlerOptions options = null)
    : AuthenticationHandler<AuthenticationSchemeOptions>(schemeOptions, logger, encoder)
{
    private readonly FakeAuthenticationHandlerOptions options = options ?? new FakeAuthenticationHandlerOptions();

    public static string SchemeName => "Fake";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new List<Claim>();
        if (this.options.Claims != null)
        {
            claims.AddRange(this.options.Claims);
        }

        var identity = new ClaimsIdentity(claims, this.Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, this.Scheme.Name);

        return await Task.FromResult(AuthenticateResult.Success(ticket)).ConfigureAwait(false);
    }
}