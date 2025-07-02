namespace BridgingIT.DevKit.Presentation.Web;

using System.Security.Claims;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

public interface IFakeIdentityProvider
{
    string GenerateAuthorizationCode(string email, string password, AuthorizeRequest request);

    Task<TokenResponse> HandleAuthorizationCodeGrantAsync(string code, string clientId, string clientSecret, string scope, HttpContext httpContext);

    Task<TokenResponse> HandleClientCredentialsGrantAsync(string clientId, string clientSecret, string scope);

    Task<TokenResponse> HandlePasswordGrantAsync(string clientId, string username, string password, string scope);

    Task<TokenResponse> HandleRefreshTokenGrantAsync(string refreshToken, string clientId, string scope, HttpContext httpContext);
}

public class FakeIdentityProvider : IFakeIdentityProvider
{
    private readonly ILogger<FakeIdentityProvider> logger;
    private readonly FakeIdentityProviderEndpointsOptions options;
    private readonly ITokenService tokenService;
    private readonly IAuthorizationCodeService authorizationCodeService;
    private readonly IPasswordValidator passwordValidator;

    public FakeIdentityProvider(
        ILogger<FakeIdentityProvider> logger,
        FakeIdentityProviderEndpointsOptions options,
        ITokenService tokenService,
        IAuthorizationCodeService authorizationCodeService,
        IPasswordValidator passwordValidator)
    {
        this.logger = logger;
        this.options = options ?? new FakeIdentityProviderEndpointsOptions();
        this.tokenService = tokenService;
        this.authorizationCodeService = authorizationCodeService;
        this.passwordValidator = passwordValidator;
    }

    public string GenerateAuthorizationCode(string email, string password, AuthorizeRequest request)
    {
        var user = this.options.Users?.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase))
            ?? throw new OAuth2Exception("invalid_grant", "Invalid credentials");

        if (!this.passwordValidator.ValidatePassword(user, password))
        {
            throw new OAuth2Exception("invalid_grant", "Invalid credentials");
        }

        if (!user.IsEnabled)
        {
            throw new OAuth2Exception("invalid_grant", "User is disabled");
        }

        return this.authorizationCodeService.GenerateCode(user, request);
    }

    public async Task<TokenResponse> HandleAuthorizationCodeGrantAsync(string code, string clientId, string clientSecret, string scope, HttpContext httpContext)
    {
        if (this.options.Clients.SafeAny() && !clientId.IsNullOrEmpty())
        {
            var client = this.options.Clients.FirstOrDefault(c => c.ClientId == clientId)
                ?? throw new OAuth2Exception("invalid_grant", $"Invalid client '{clientId}'");

            if (client.IsConfidentialClient && (clientSecret.IsNullOrEmpty() || clientSecret != client.ClientSecret))
            {
                throw new OAuth2Exception("invalid_client", "Invalid client credentials");
            }
        }

        if (code.IsNullOrEmpty())
        {
            throw new OAuth2Exception("invalid_grant", "Invalid authorization code (code)");
        }

        this.logger.LogDebug("Validation authentication code {AuthCode}", code);
        var data = this.authorizationCodeService.ValidateCode(code);
        if (data == null)
        {
            throw new OAuth2Exception("invalid_grant", "Invalid authorization code (data)");
        }

        var user = this.options.Users?.FirstOrDefault(u => u.Id == data.UserId)
            ?? throw new OAuth2Exception("invalid_grant", "Invalid credentials");

        if (!user.IsEnabled)
        {
            throw new OAuth2Exception("invalid_grant", "User is disabled");
        }

        var tokenResponse = this.CreateTokenResponse(user, clientId, data.Scope);

        if (this.options.EnablePersistentRefreshTokens && httpContext != null)
        {
            await this.SignInUserAsync(httpContext, user, tokenResponse);
        }

        return tokenResponse;
    }

    public Task<TokenResponse> HandlePasswordGrantAsync(string clientId, string username, string password, string scope)
    {
        if (this.options.Clients.SafeAny() && !clientId.IsNullOrEmpty())
        {
            var client = this.options.Clients.FirstOrDefault(c => c.ClientId == clientId) ?? throw new OAuth2Exception("invalid_grant", $"Invalid client '{clientId}'");
        }

        var user = (this.options.Users?.FirstOrDefault(u => u.Email.Equals(username, StringComparison.OrdinalIgnoreCase))) ?? throw new OAuth2Exception("invalid_grant", "Invalid credentials");
        if (string.IsNullOrEmpty(user.Password) || !this.passwordValidator.ValidatePassword(user, password))
        {
            throw new OAuth2Exception("invalid_grant", "Invalid credentials");
        }

        if (!user.IsEnabled)
        {
            throw new OAuth2Exception("invalid_grant", "User is disabled");
        }

        return Task.FromResult(this.CreateTokenResponse(user, clientId, scope));
    }

    public Task<TokenResponse> HandleClientCredentialsGrantAsync(string clientId, string clientSecret, string scope)
    {
        if (this.options.Clients.SafeAny() && !clientId.IsNullOrEmpty())
        {
            var client = this.options.Clients.FirstOrDefault(c => c.ClientId == clientId) ?? throw new OAuth2Exception("invalid_grant", $"Invalid client '{clientId}'");
            if (client.IsConfidentialClient)
            {
                if (string.IsNullOrEmpty(clientSecret) || clientSecret != client.ClientSecret)
                {
                    throw new OAuth2Exception("invalid_client", "Invalid client credentials");
                }
            }
        }

        return Task.FromResult(new TokenResponse
        {
            AccessToken = this.tokenService.GenerateServiceToken(clientId, scope),
            TokenType = "Bearer",
            ExpiresIn = (int)this.options.AccessTokenLifetime.TotalSeconds,
            Scope = scope
        });
    }

    public async Task<TokenResponse> HandleRefreshTokenGrantAsync(string refreshToken, string clientId, string scope, HttpContext httpContext)
    {
        if (this.options.Clients.SafeAny() && !clientId.IsNullOrEmpty())
        {
            var client = this.options.Clients.FirstOrDefault(c => c.ClientId == clientId) ?? throw new OAuth2Exception("invalid_grant", $"Invalid client '{clientId}'");
        }

        if (this.options.EnablePersistentRefreshTokens && refreshToken.IsNullOrEmpty())
        {
            httpContext?.Request?.Cookies?.TryGetValue(".AspNetCore.Identity", out refreshToken);
        }

        var validation = this.tokenService.ValidateRefreshToken(refreshToken);
        if (!validation.IsValid)
        {
            throw new OAuth2Exception("invalid_grant", "Invalid refresh token");
        }

        scope ??= validation.Claims?.FirstOrDefault(c => c.Type == "scope")?.Value;
        var userId = validation.Claims?.FirstOrDefault(c => c.Type == "sub")?.Value;
        var user = (this.options.Users?.FirstOrDefault(u => u.Id == userId)) ?? throw new OAuth2Exception("invalid_grant", "Invalid credentials");
        if (!user.IsEnabled)
        {
            throw new OAuth2Exception("invalid_grant", "User is disabled");
        }

        var tokenResponse = this.CreateTokenResponse(user, clientId, scope);

        if (this.options.EnablePersistentRefreshTokens && httpContext != null)
        {
            await this.SignInUserAsync(httpContext, user, tokenResponse);
        }

        return tokenResponse;
    }

    private TokenResponse CreateTokenResponse(FakeUser user, string clientId, string scope)
    {
        scope ??= "openid profile email offline_access";
        var isOidcRequest = scope?.Contains("openid", StringComparison.OrdinalIgnoreCase) == true;
        var response = new TokenResponse
        {
            AccessToken = this.tokenService.GenerateAccessToken(user, clientId, scope),
            RefreshToken = this.tokenService.GenerateRefreshToken(user, clientId, scope),
            TokenType = "Bearer",
            ExpiresIn = (int)this.options.AccessTokenLifetime.TotalSeconds,
            RefreshExpiresIn = (int)this.options.RefreshTokenLifetime.TotalSeconds,
            Scope = scope,
            SessionState = Guid.NewGuid().ToString("N")
        };

        if (isOidcRequest)
        {
            response.IdToken = this.tokenService.GenerateIdToken(user, clientId, null);
        }

        return response;
    }

    private async Task SignInUserAsync(HttpContext httpContext, FakeUser user, TokenResponse tokenResponse)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email),
            new("refresh_token", tokenResponse.RefreshToken)
        };

        if (user.Roles.SafeAny())
        {
            claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        try
        {
            await httpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.Add(this.options.RefreshTokenLifetime),
                    IssuedUtc = DateTimeOffset.UtcNow,
                    AllowRefresh = true
                });
        }
        catch (InvalidOperationException ex)
        {
            this.logger.LogError(ex, "Failed to sign in user");
        }
    }
}