// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Client;

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

/// <summary>
/// Provides a persistent authentication state provider that refreshes the token when it expires
/// </summary>
public class PersistentAuthenticationStateProvider(
    HttpClient httpClient,
    NavigationManager navigationManager,
    IAccessTokenProvider tokenProvider,
    IConfiguration configuration,
    ILogger<PersistentAuthenticationStateProvider> logger,
    IJSRuntime jsRuntime) : AuthenticationStateProvider
{
    private readonly IAccessTokenProvider tokenProvider = tokenProvider;
    private static OpenIdConfiguration cachedConfiguration;
    private static readonly SemaphoreSlim Semaphore = new(1, 1);

    /// <summary>
    /// Gets the current authentication state
    /// </summary>
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var config = await this.GetOpenIdConfigurationAsync();
            var refreshToken = await this.GetStoredRefreshTokenAsync();
            var parameters = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = configuration["Authentication:ClientId"]
            };

            // Only add refresh token from storage if present
            if (!string.IsNullOrEmpty(refreshToken))
            {
                parameters["refresh_token"] = refreshToken;
            }

            var tokenResponse = await httpClient.PostAsync(config.TokenEndpoint, new FormUrlEncodedContent(parameters));
            if (tokenResponse.IsSuccessStatusCode)
            {
                var response = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();
                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(response.AccessToken);
                var identity = new ClaimsIdentity(jwt.Claims, "jwt", JwtRegisteredClaimNames.Name, ClaimTypes.Role);
                var user = new ClaimsPrincipal(identity);

                logger.LogInformation("Token refresh successful");

                return new AuthenticationState(user);
            }
            else
            {
                logger.LogWarning("Token refresh failed with status: {StatusCode}", tokenResponse.StatusCode);
                navigationManager.NavigateTo("authentication/login");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Token refresh failed");
            navigationManager.NavigateTo("authentication/login");
        }

        return new AuthenticationState(
            new ClaimsPrincipal(new ClaimsIdentity()));
    }

    /// <summary>
    /// Triggers a refresh of the authentication state
    /// </summary>
    public void TriggerRefresh()
    {
        this.NotifyAuthenticationStateChanged(this.GetAuthenticationStateAsync());
    }

    private async Task<OpenIdConfiguration> GetOpenIdConfigurationAsync()
    {
        if (cachedConfiguration != null)
        {
            return cachedConfiguration;
        }

        try
        {
            await Semaphore.WaitAsync();
            if (cachedConfiguration != null)
            {
                return cachedConfiguration;
            }

            var authority = configuration["Authentication:Authority"]?.TrimEnd('/');
            cachedConfiguration = await httpClient.GetFromJsonAsync<OpenIdConfiguration>(
                $"{authority}/.well-known/openid-configuration");

            return cachedConfiguration;
        }
        finally
        {
            Semaphore.Release();
        }
    }

    private async ValueTask<string> GetStoredRefreshTokenAsync()
    {
        try
        {
            logger.LogDebug("Getting stored refresh token");

            // Get and store the entries
            var storageEntries = await jsRuntime.EvalAsync<string[]>(
                "Object.keys(sessionStorage).filter(k => k.startsWith('oidc.user:'))");
            if (storageEntries.Length == 0)
            {
                logger.LogDebug("No OIDC entries found in session storage");
                return null;
            }

            var userData = await jsRuntime.InvokeAsync<string>("sessionStorage.getItem", storageEntries[0]);
            if (string.IsNullOrEmpty(userData))
            {
                logger.LogDebug("No user data found in session storage");
                return null;
            }

            var userDataObj = JsonSerializer.Deserialize<JsonElement>(userData);
            if (userDataObj.TryGetProperty("refresh_token", out var refreshToken))
            {
                logger.LogDebug("Found refresh token");
                return refreshToken.GetString();
            }

            logger.LogDebug("No refresh token found in user data");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving refresh token from session storage");
        }

        return null;
    }
}

/// <summary>
/// Represents open id configuration.
/// </summary>
public class OpenIdConfiguration
{
    /// <summary>
    /// Gets or sets the token endpoint.
    /// </summary>
    [JsonPropertyName("token_endpoint")]
    public string TokenEndpoint { get; set; }
}

/// <summary>
/// Represents token response.
/// </summary>
public class TokenResponse
{
    /// <summary>
    /// Gets or sets the access token.
    /// </summary>
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; }

    /// <summary>
    /// Gets or sets the refresh token.
    /// </summary>
    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; }

    /// <summary>
    /// Gets or sets the expires in.
    /// </summary>
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    /// <summary>
    /// Gets or sets the refresh expires in.
    /// </summary>
    [JsonPropertyName("refresh_expires_in")]
    public int RefreshExpiresIn { get; set; }

    /// <summary>
    /// Gets or sets the token type.
    /// </summary>
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// Gets or sets the id token.
    /// </summary>
    [JsonPropertyName("id_token")]
    public string IdToken { get; set; }

    /// <summary>
    /// Gets or sets the session state.
    /// </summary>
    [JsonPropertyName("session_state")]
    public string SessionState { get; set; }

    /// <summary>
    /// Gets or sets the scope.
    /// </summary>
    [JsonPropertyName("scope")]
    public string Scope { get; set; }
}
