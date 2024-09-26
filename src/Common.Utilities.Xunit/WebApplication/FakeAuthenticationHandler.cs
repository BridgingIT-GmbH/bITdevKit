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

/// <summary>
/// Provides a fake authentication handler for use in testing scenarios.
/// This class simulates the behavior of an authentication handler by
/// generating authentication tickets based on predefined claims.
/// </summary>
/// <remarks>
/// This handler can be used to bypass actual authentication mechanisms
/// in testing environments, enabling more controlled testing scenarios.
/// </remarks>
public class FakeAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions,
    ILoggerFactory logger,
    UrlEncoder encoder,
    FakeAuthenticationHandlerOptions options = null)
    : AuthenticationHandler<AuthenticationSchemeOptions>(schemeOptions, logger, encoder)
{
    /// <summary>
    /// Represents options for the FakeAuthenticationHandler.
    /// Holds configuration values specific to the FakeAuthenticationHandler,
    /// including claims to be included in the generated authentication ticket.
    /// </summary>
    private readonly FakeAuthenticationHandlerOptions options = options ?? new FakeAuthenticationHandlerOptions();

    /// <summary>
    /// The unique scheme name used by the FakeAuthenticationHandler.
    /// This is utilized to configure the authentication scheme to use
    /// a fake authentication handler instead of the default handler.
    /// </summary>
    public static string SchemeName => "Fake";

    /// <summary>
    /// Authenticates the request using the provided claims from the FakeAuthenticationHandlerOptions.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous authentication operation.
    /// The task result contains the AuthenticateResult with the authentication ticket.
    /// </returns>
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