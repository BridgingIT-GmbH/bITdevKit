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
    private readonly HttpClient httpClient = httpClient;
    private readonly NavigationManager navigationManager = navigationManager;
    private readonly IAccessTokenProvider tokenProvider = tokenProvider;
    private readonly ILogger<PersistentAuthenticationStateProvider> logger = logger;
    private readonly IJSRuntime jsRuntime = jsRuntime;
    private readonly IConfiguration configuration = configuration;
    private static OpenIdConfiguration cachedConfiguration;
    private static readonly SemaphoreSlim semaphore = new(1, 1);

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
                ["client_id"] = this.configuration["Authentication:ClientId"]
            };

            // Only add refresh token from storage if present
            if (!string.IsNullOrEmpty(refreshToken))
            {
                parameters["refresh_token"] = refreshToken;
            }

            var tokenResponse = await this.httpClient.PostAsync(config.TokenEndpoint, new FormUrlEncodedContent(parameters));
            if (tokenResponse.IsSuccessStatusCode)
            {
                var response = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();
                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(response.AccessToken);
                var identity = new ClaimsIdentity(jwt.Claims, "jwt", JwtRegisteredClaimNames.Name, ClaimTypes.Role);
                var user = new ClaimsPrincipal(identity);

                this.logger.LogInformation("Token refresh successful");

                return new AuthenticationState(user);
            }
            else
            {
                this.logger.LogWarning("Token refresh failed with status: {StatusCode}", tokenResponse.StatusCode);
                this.navigationManager.NavigateTo("authentication/login");
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Token refresh failed");
            this.navigationManager.NavigateTo("authentication/login");
        }

        return new AuthenticationState(
            new ClaimsPrincipal(new ClaimsIdentity()));
    }

    /// <summary>
    /// Triggers a refresh of the authentication state
    /// </summary>
    public void TriggerRefresh()
    {
        this.NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private async Task<OpenIdConfiguration> GetOpenIdConfigurationAsync()
    {
        if (cachedConfiguration != null)
        {
            return cachedConfiguration;
        }

        try
        {
            await semaphore.WaitAsync();
            if (cachedConfiguration != null)
            {
                return cachedConfiguration;
            }

            var authority = this.configuration["Authentication:Authority"]?.TrimEnd('/');
            cachedConfiguration = await this.httpClient.GetFromJsonAsync<OpenIdConfiguration>(
                $"{authority}/.well-known/openid-configuration");

            return cachedConfiguration;
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async ValueTask<string> GetStoredRefreshTokenAsync()
    {
        try
        {
            this.logger.LogDebug("Getting stored refresh token");

            // Get and store the entries
            var storageEntries = await this.jsRuntime.EvalAsync<string[]>(
                "Object.keys(sessionStorage).filter(k => k.startsWith('oidc.user:'))");
            if (storageEntries.Length == 0)
            {
                this.logger.LogDebug("No OIDC entries found in session storage");
                return null;
            }

            var userData = await this.jsRuntime.InvokeAsync<string>("sessionStorage.getItem", storageEntries[0]);
            if (string.IsNullOrEmpty(userData))
            {
                this.logger.LogDebug("No user data found in session storage");
                return null;
            }

            var userDataObj = JsonSerializer.Deserialize<JsonElement>(userData);
            if (userDataObj.TryGetProperty("refresh_token", out var refreshToken))
            {
                this.logger.LogDebug("Found refresh token");
                return refreshToken.GetString();
            }

            this.logger.LogDebug("No refresh token found in user data");
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error retrieving refresh token from session storage");
        }

        return null;
    }
}

public class OpenIdConfiguration
{
    [JsonPropertyName("token_endpoint")]
    public string TokenEndpoint { get; set; }
}

public class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; }

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("refresh_expires_in")]
    public int RefreshExpiresIn { get; set; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";

    [JsonPropertyName("id_token")]
    public string IdToken { get; set; }

    [JsonPropertyName("session_state")]
    public string SessionState { get; set; }

    [JsonPropertyName("scope")]
    public string Scope { get; set; }
}