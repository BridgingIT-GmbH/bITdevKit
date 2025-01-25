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
public partial class FakeAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions,
    ILoggerFactory logger,
    UrlEncoder encoder,
    FakeAuthenticationOptions options = null)
    : AuthenticationHandler<AuthenticationSchemeOptions>(schemeOptions, logger, encoder)
{
    private readonly FakeAuthenticationOptions options = options ?? new FakeAuthenticationOptions();

    public static string SchemeName => "Fake";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        TypedLogger.LogStartingAuthentication(this.Logger, "IDN");

        // Try to get Authorization header
        this.Context.Request.Headers.TryGetValue("Authorization", out var authorizationHeader);
        if (authorizationHeader.Count != 0)
        {
            var header = authorizationHeader[0].Split(' ');
            if (header.Length != 2 || header[0] != "FakeUser")
            {
                TypedLogger.LogInvalidAuthorizationHeader(this.Logger, "IDN");
                return AuthenticateResult.Fail("Invalid authorization header format");
            }

            var email = header[1];
            var user = this.options.Users?.FirstOrDefault(u => u.Email == email);
            if (user != null)
            {
                if (!user.IsEnabled)
                {
                    TypedLogger.LogFakeUserDisabled(this.Logger, "IDN", email);
                    return AuthenticateResult.Fail("User disabled");
                }

                var claims = this.CreateUserClaims(user);
                TypedLogger.LogFakeUserAuthenticated(this.Logger, "IDN", email);

                return await Task.FromResult(
                    this.CreateAuthenticationTicket(claims)).AnyContext();
            }

            TypedLogger.LogFakeUserNotFound(this.Logger, "IDN", email);
            return AuthenticateResult.Fail("User not found");
        }

        // Try to use default user if no header
        var defaultUser = this.options.Users?.FirstOrDefault(u => u.IsDefault);
        if (defaultUser != null)
        {
            if (!defaultUser.IsEnabled)
            {
                TypedLogger.LogFakeUserDisabled(this.Logger, "IDN", defaultUser.Email);
                return AuthenticateResult.Fail("User disabled");
            }

            var claims = this.CreateUserClaims(defaultUser);
            TypedLogger.LogFakeUserAuthenticated(this.Logger, "IDN", defaultUser.Email);

            return await Task.FromResult(
                this.CreateAuthenticationTicket(claims)).AnyContext();
        }

        return AuthenticateResult.Fail("No authorization header found and no default user configured");
    }

    private List<Claim> CreateUserClaims(FakeUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email)
        };

        if (user.Roles?.Any() == true)
        {
            claims.AddRange(user.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
        }

        if (user.Claims?.Any() == true)
        {
            claims.AddRange(user.Claims.Select(c => new Claim(c.Key, c.Value)));
        }

        // Add any additional configured claims
        if (this.options.Claims?.Any() == true)
        {
            claims.AddRange(this.options.Claims);
        }

        return claims;
    }

    private AuthenticateResult CreateAuthenticationTicket(IEnumerable<Claim> claims)
    {
        var identity = new ClaimsIdentity(claims, this.Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, this.Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }

    public static partial class TypedLogger
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "{LogKey} authentication - start fake authentication")]
        public static partial void LogStartingAuthentication(ILogger logger, string logKey);

        [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "{LogKey} authentication - invalid authorization header format. Expected: 'FakeUser [email]'")]
        public static partial void LogInvalidAuthorizationHeader(ILogger logger, string logKey);

        [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "{LogKey} authentication - fake user authenticated: {Email}")]
        public static partial void LogFakeUserAuthenticated(ILogger logger, string logKey, string Email);

        [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "{LogKey} authentication - fake user not found: {Email}")]
        public static partial void LogFakeUserNotFound(ILogger logger, string logKey, string Email);

        [LoggerMessage(EventId = 6, Level = LogLevel.Warning, Message = "{LogKey} authentication - fake user disabled: {Email}")]
        public static partial void LogFakeUserDisabled(ILogger logger, string logKey, string Email);
    }
}