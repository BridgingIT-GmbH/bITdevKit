// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Web;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class FakeIdentityProviderApplication : WebApplicationFactory<FakeIdentityProviderTests>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var appBuilder = WebApplication.CreateBuilder();
        appBuilder.WebHost.UseTestServer();
        var config = new TestConfiguration();

        appBuilder.Services.AddRouting();
        appBuilder.Services.AddControllers();
        appBuilder.Services.AddCors();

        appBuilder.Services.AddAuthentication()
            .AddCookie("Cookies"); // needed for PersistentRefreshTokens (httpContext.SignInAsync)

        appBuilder.Services.AddFakeIdentityProvider(options =>
        {
            options.Enabled(true)
                .WithIssuer(config.Issuer)
                .WithUsers([
                    config.DefaultUser,
                    config.RegularUser
                ])
                .WithClient(
                    config.PublicClient.Name,
                    config.PublicClient.ClientId,
                    config.PublicClient.RedirectUri)
                .WithConfidentalClient(
                    config.ConfidentialClient.Name,
                    config.ConfidentialClient.ClientId,
                    config.ConfidentialClient.ClientSecret,
                    [config.ConfidentialClient.RedirectUri])
                .WithTokenLifetimes(
                    config.AccessTokenLifetime,
                    config.RefreshTokenLifetime);
        });

        appBuilder.Services.AddSingleton(config);

        var app = appBuilder.Build();

        app.UseRouting();
        app.UseCors(nameof(Presentation.Web.IdentityProvider));
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapEndpoints();

        app.Start();

        return app;
    }
}

public class FakeIdentityProviderTests : IAsyncDisposable
{
    private readonly FakeIdentityProviderApplication factory;
    private readonly HttpClient client;
    private readonly TestConfiguration testConfig;

    public FakeIdentityProviderTests()
    {
        this.factory = new FakeIdentityProviderApplication();
        this.client = this.factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        this.testConfig = this.factory.Services.GetRequiredService<TestConfiguration>();
    }

    public async ValueTask DisposeAsync()
    {
        if (this.factory != null)
        {
            await this.factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task WellKnownConfiguration_ShouldReturnValidEndpoints()
    {
        // Act
        var response = await this.client.GetAsync("/api/_system/identity/connect/.well-known/openid-configuration");
        var config = await response.Content.ReadFromJsonAsync<OpenIdConfiguration>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        config.ShouldNotBeNull();
        config.Issuer.ShouldBe("https://localhost");
        config.AuthorizationEndpoint.ShouldEndWith("/connect/authorize");
        config.TokenEndpoint.ShouldEndWith("/connect/token");
        config.UserInfoEndpoint.ShouldEndWith("/connect/userinfo");
    }

    [Fact]
    public async Task AuthorizeEndpoint_WithValidRequest_ShouldShowLoginPage()
    {
        // Arrange - Build a standard OAuth2 authorization request
        var query = new QueryBuilder
    {
        { "response_type", "code" },
        { "client_id", this.testConfig.PublicClient.ClientId },
        { "redirect_uri", this.testConfig.PublicClient.RedirectUri },
        { "scope", "openid profile" },
        { "state", "abc123" }
    };

        // Act - Request the authorization page
        var response = await this.client.GetAsync($"/api/_system/identity/connect/authorize{query}");
        var content = await response.Content.ReadAsStringAsync();

        // Assert - Verify we get the login page with our test users
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        content.ShouldContain(this.testConfig.DefaultUser.Email);
        content.ShouldContain(this.testConfig.RegularUser.Email);
    }

    [Fact]
    public async Task CompleteAuthorizationFlow_WithPublicClient_ShouldSucceed()
    {
        // Arrange - Prepare the authorization request with state validation
        var state = Guid.NewGuid().ToString("N");
        var query = new QueryBuilder
        {
            { "response_type", "code" },
            { "client_id", this.testConfig.PublicClient.ClientId },
            { "redirect_uri", this.testConfig.PublicClient.RedirectUri },
            { "scope", "openid profile" },
            { "state", state }
        };

        // Act Step 1 - Submit user selection to get authorization code
        var authorizeResponse = await this.client.GetAsync(
            $"/api/_system/identity/connect/authorize/callback{query}&email={this.testConfig.DefaultUser.Email}");

        // Verify the redirect and extract the authorization code
        authorizeResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = authorizeResponse.Headers.Location.ToString();
        var callbackUri = new Uri(location);
        var queryParams = HttpUtility.ParseQueryString(callbackUri.Query);

        var code = queryParams["code"];
        queryParams["state"].ShouldBe(state); // Verify state parameter for CSRF protection

        // Act Step 2 - Exchange code for tokens
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "authorization_code" },
            { "client_id", this.testConfig.PublicClient.ClientId },
            { "code", code },
            { "redirect_uri", this.testConfig.PublicClient.RedirectUri }
        });

        var tokenResponse = await this.client.PostAsync(
            "/api/_system/identity/connect/token", tokenRequest);
        var tokens = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();

        // Assert token response
        tokenResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        tokens.ShouldNotBeNull();
        tokens.AccessToken.ShouldNotBeNullOrEmpty();
        tokens.RefreshToken.ShouldNotBeNullOrEmpty();
        tokens.TokenType.ShouldBe("Bearer");

        // Act Step 3 - Use the access token to get user info
        var userInfoRequest = new HttpRequestMessage(HttpMethod.Get,
            "/api/_system/identity/connect/userinfo");
        userInfoRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var userInfoResponse = await this.client.SendAsync(userInfoRequest);
        var userInfo = await userInfoResponse.Content.ReadFromJsonAsync<UserInfoResponse>();

        // Assert user info matches our test user
        userInfo.ShouldNotBeNull();
        userInfo.Email.ShouldBe(this.testConfig.DefaultUser.Email);
        userInfo.Name.ShouldBe(this.testConfig.DefaultUser.Name);
        userInfo.Roles.ShouldContain("Administrators");
    }

    [Fact]
    public async Task ClientCredentialsFlow_ShouldReturnValidToken()
    {
        // Arrange - Prepare client credentials grant request
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
    {
        { "grant_type", "client_credentials" },
        { "client_id", this.testConfig.ConfidentialClient.ClientId },
        { "client_secret", this.testConfig.ConfidentialClient.ClientSecret },
        { "scope", "api" }
    });

        // Act - Request token using client credentials
        var response = await this.client.PostAsync("/api/_system/identity/connect/token", tokenRequest);
        var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>();

        // Assert - Verify we get a valid access token without refresh token
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        tokens.ShouldNotBeNull();
        tokens.AccessToken.ShouldNotBeNullOrEmpty();
        tokens.RefreshToken.ShouldBeNull(); // Client credentials don't get refresh tokens
        tokens.TokenType.ShouldBe("Bearer");
    }

    [Fact]
    public async Task InvalidClient_ShouldReturnError()
    {
        // Arrange - Prepare request with invalid client ID
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
    {
        { "grant_type", "client_credentials" },
        { "client_id", "invalid-client" },
        { "scope", "api" }
    });

        // Act - Request token with invalid client
        var response = await this.client.PostAsync(
            "/api/_system/identity/connect/token", tokenRequest);
        var error = await response.Content.ReadFromJsonAsync<OAuth2Error>();

        // Assert - Verify we get the correct error response
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        error.ShouldNotBeNull();
        error.Error.ShouldBe("invalid_grant");
    }

    [Fact]
    public async Task DebugEndpoint_ShouldReturnConfiguration()
    {
        // Act - Request debug info endpoint
        var response = await this.client.GetAsync("/api/_system/identity/connect/debuginfo");
        var debug = await response.Content.ReadFromJsonAsync<DebugInfoResponse>();

        // Assert - Verify debug info matches our test configuration
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        debug.ShouldNotBeNull();
        debug.TokenIssuer.ShouldBe(this.testConfig.Issuer);
        debug.ConfiguredUsers.Count.ShouldBe(2);
        debug.ConfiguredClients.Count.ShouldBe(2);

        // Verify endpoints are correctly configured
        debug.Endpoints.Authorization.ShouldEndWith("/connect/authorize");
        debug.Endpoints.Token.ShouldEndWith("/connect/token");
        debug.Endpoints.UserInfo.ShouldEndWith("/connect/userinfo");
    }

    [Fact]
    public async Task RefreshTokenFlow_ShouldProvideNewTokens()
    {
        // First, get initial tokens through authorization code flow
        var initialTokens = await GetTokensThroughAuthorizationFlow();

        // Now test the refresh token functionality
        var refreshRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "refresh_token" },
            { "client_id", this.testConfig.PublicClient.ClientId },
            { "refresh_token", initialTokens.RefreshToken }
        });

        var response = await this.client.PostAsync(
            "/api/_system/identity/connect/token", refreshRequest);
        var newTokens = await response.Content.ReadFromJsonAsync<TokenResponse>();

        // Verify we get new tokens that are different from the original ones
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        newTokens.AccessToken.ShouldNotBe(initialTokens.AccessToken);
        newTokens.RefreshToken.ShouldNotBe(initialTokens.RefreshToken);
    }

    [Fact]
    public async Task ConfidentialClient_WithInvalidSecret_ShouldFail()
    {
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", this.testConfig.ConfidentialClient.ClientId },
            { "client_secret", "wrong-secret" },
            { "scope", "api" }
        });

        var response = await this.client.PostAsync(
            "/api/_system/identity/connect/token", tokenRequest);
        var error = await response.Content.ReadFromJsonAsync<OAuth2Error>();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        error.Error.ShouldBe("invalid_client");
    }

    [Fact]
    public async Task UserInfo_WithInvalidToken_ShouldReturn401()
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            "/api/_system/identity/connect/userinfo");
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", "invalid-token");

        var response = await this.client.SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_WithRedirectUri_ShouldRedirectProperly()
    {
        var query = new QueryBuilder
    {
        { "post_logout_redirect_uri", this.testConfig.PublicClient.RedirectUri },
        { "state", "logout-state" }
    };

        var response = await this.client.GetAsync(
            $"/api/_system/identity/connect/logout{query}");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = response.Headers.Location.ToString();
        location.ShouldStartWith(this.testConfig.PublicClient.RedirectUri);
        location.ShouldContain("state=logout-state");
    }

    [Fact]
    public async Task AuthorizeEndpoint_WithInvalidRedirectUri_ShouldReturnError()
    {
        var query = new QueryBuilder
        {
            { "response_type", "code" },
            { "client_id", this.testConfig.PublicClient.ClientId },
            { "redirect_uri", "https://malicious-site.com" },
            { "scope", "openid profile" },
            { "state", "abc123" }
        };

        var response = await this.client.GetAsync(
            $"/api/_system/identity/connect/authorize{query}");
        var error = await response.Content.ReadFromJsonAsync<OAuth2Error>();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        error.Error.ShouldBe("invalid_request");
    }

    [Theory]
    [InlineData("openid")]
    [InlineData("openid profile")]
    [InlineData("openid profile email")]
    [InlineData("openid profile email roles")]
    public async Task TokenEndpoint_WithValidScopes_ShouldSucceed(string scope)
    {
        // Get authorization code first
        var code = await GetAuthorizationCode(scope);

        // Exchange code for tokens
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
    {
        { "grant_type", "authorization_code" },
        { "client_id", this.testConfig.PublicClient.ClientId },
        { "code", code },
        { "redirect_uri", this.testConfig.PublicClient.RedirectUri }
    });

        var response = await this.client.PostAsync(
            "/api/_system/identity/connect/token", tokenRequest);
        var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        tokens.Scope.ShouldBe(scope);
    }

    // Helper method to go through authorization flow and get tokens
    private async Task<TokenResponse> GetTokensThroughAuthorizationFlow()
    {
        var state = Guid.NewGuid().ToString("N");
        var query = new QueryBuilder
    {
        { "response_type", "code" },
        { "client_id", this.testConfig.PublicClient.ClientId },
        { "redirect_uri", this.testConfig.PublicClient.RedirectUri },
        { "scope", "openid profile offline_access" },
        { "state", state }
    };

        var authorizeResponse = await this.client.GetAsync(
            $"/api/_system/identity/connect/authorize/callback{query}&email={this.testConfig.DefaultUser.Email}");

        var location = authorizeResponse.Headers.Location.ToString();
        var code = HttpUtility.ParseQueryString(new Uri(location).Query)["code"];

        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
    {
        { "grant_type", "authorization_code" },
        { "client_id", this.testConfig.PublicClient.ClientId },
        { "code", code },
        { "redirect_uri", this.testConfig.PublicClient.RedirectUri }
    });

        var tokenResponse = await this.client.PostAsync(
            "/api/_system/identity/connect/token", tokenRequest);
        return await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();
    }

    // Helper method to get authorization code
    private async Task<string> GetAuthorizationCode(string scope)
    {
        var query = new QueryBuilder
    {
        { "response_type", "code" },
        { "client_id", this.testConfig.PublicClient.ClientId },
        { "redirect_uri", this.testConfig.PublicClient.RedirectUri },
        { "scope", scope },
        { "state", "test-state" }
    };

        var response = await this.client.GetAsync(
            $"/api/_system/identity/connect/authorize/callback{query}&email={this.testConfig.DefaultUser.Email}");

        var location = response.Headers.Location.ToString();
        return HttpUtility.ParseQueryString(new Uri(location).Query)["code"];
    }
}

public class TestConfiguration
{
    // Standard test users for all scenarios
    public FakeUser DefaultUser { get; init; } = new("admin@example.com", "Admin User", ["Administrators", "Users"], isDefault: true);

    public FakeUser RegularUser { get; init; } = new("user@example.com", "Regular User", ["Users"]);

    // Client configurations for different OAuth flows
    public ClientConfig PublicClient { get; init; } = new()
    {
        ClientId = "public-client",
        Name = "Public Test Client",
        RedirectUri = "https://localhost/callback"
    };

    public ClientConfig ConfidentialClient { get; init; } = new()
    {
        ClientId = "confidential-client",
        Name = "Confidential Test Client",
        ClientSecret = "secret123",
        RedirectUri = "https://localhost/server-callback"
    };

    public string Issuer { get; init; } = "https://localhost";

    public TimeSpan AccessTokenLifetime { get; init; } = TimeSpan.FromMinutes(5);

    public TimeSpan RefreshTokenLifetime { get; init; } = TimeSpan.FromHours(1);
}

public class ClientConfig
{
    public string ClientId { get; init; }

    public string Name { get; init; }

    public string ClientSecret { get; init; }

    public string RedirectUri { get; init; }
}