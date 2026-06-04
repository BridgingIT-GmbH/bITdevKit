// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

using System.Net;
using System.Security.Claims;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.Dashboard.Pages;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using IResult = Microsoft.AspNetCore.Http.IResult;

public class DashboardEndpoints(
    //ILogger<DashboardEndpoints> logger,
    DashboardEndpointsOptions options,
    FakeIdentityProviderEndpointsOptions fakeIdentityProviderOptions = null,
    IFakeIdentityProvider fakeIdentityProvider = null,
    ITokenService tokenService = null,
    ILogger<DashboardEndpoints> logger = null) : EndpointsBase
{
    public override void Map(IEndpointRouteBuilder app)
    {
        options ??= new DashboardEndpointsOptions();

        if (!options.Enabled)
        {
            return;
        }

        var group = this.MapGroup(app, options)
            .WithTags("_bdk.Dashboard");
            //.DisableAntiforgery();
            //.RequireCors(nameof(BridgingIT.DevKit.Presentation.Web.Dashboard));
        var paths = options.EndpointPaths;

        group.MapGet("/", this.HandleIndex)
            .WithName("_bdk.Dashboard.Index")
            .WithSummary("Dashboard Index")
            .WithDescription("Shows the dashboard index page.")
            .Produces<string>((int)HttpStatusCode.OK);

        group.MapGet(paths.Identity, (HttpContext httpContext) => this.HandleIdentity(httpContext))
            .WithName("_bdk.Dashboard.Identity")
            .WithSummary("Dashboard Identity")
            .WithDescription("Shows the dashboard identity page.")
            .Produces<string>((int)HttpStatusCode.OK);

        group.MapPost(paths.IdentityClientCredentialsLogin, (HttpContext httpContext, [FromForm] string returnUrl) => this.HandleIdentityClientCredentialsLogin(httpContext, returnUrl))
            .WithName("_bdk.Dashboard.IdentityClientCredentialsLogin")
            .WithSummary("Dashboard Identity Client Credentials Login")
            .WithDescription("Signs in to the dashboard using the fake identity provider client-credentials flow.")
            .Produces((int)HttpStatusCode.Redirect)
            .DisableAntiforgery();

        group.MapGet(paths.Metrics, this.HandleMetrics)
            .WithName("_bdk.Dashboard.Metrics")
            .WithSummary("Dashboard Metrics")
            .WithDescription("Shows the dashboard metrics page.")
            .Produces<string>((int)HttpStatusCode.OK);

        group.MapGet(paths.MetricsContent, this.HandleMetricsContent)
            .WithName("_bdk.Dashboard.MetricsContent")
            .WithSummary("Dashboard Metrics Content")
            .WithDescription("Shows the dashboard metrics content fragment.")
            .Produces<string>((int)HttpStatusCode.OK);
    }

    private Task<IResult> HandleIndex()
    {
        return Task.FromResult(
            Results.RazorSlice<Pages.DashboardIndex>());
    }

    private IResult HandleIdentity(HttpContext httpContext)
    {
        return Results.RazorSlice<Pages.DashboardIdentity>();
    }

    private Task<IResult> HandleMetrics()
    {
        return Task.FromResult(
            Results.RazorSlice<Pages.DashboardMetrics>());
    }

    private Task<IResult> HandleMetricsContent()
    {
        return Task.FromResult(
            Results.RazorSlice<Pages.DashboardMetricsContent>());
    }

    private async Task<IResult> HandleIdentityClientCredentialsLogin(HttpContext httpContext, string returnUrl)
    {
        var redirectUrl = NormalizeDashboardReturnUrl(httpContext, returnUrl);
        var client = this.GetClientCredentialsClient();

        if (fakeIdentityProviderOptions is null || client is null || fakeIdentityProvider is null || tokenService is null)
        {
            return Results.Redirect(AppendQuery(redirectUrl, "identity_error", "Fake identity provider client credentials are not available."));
        }

        try
        {
            var tokenResponse = await fakeIdentityProvider.HandleClientCredentialsGrantAsync(
                client.ClientId,
                client.ClientSecret,
                "api").AnyContext();

            var validation = tokenService.ValidateToken(tokenResponse.AccessToken);
            if (!validation.IsValid)
            {
                return Results.Redirect(AppendQuery(redirectUrl, "identity_error", validation.Error ?? "The generated service token could not be validated."));
            }

            var claims = validation.Claims?.ToList() ?? [];
            if (!claims.Any(claim => claim.Type == ClaimTypes.NameIdentifier))
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, client.ClientId ?? string.Empty));
            }

            if (!claims.Any(claim => claim.Type == ClaimTypes.Name))
            {
                claims.Add(new Claim(ClaimTypes.Name, client.Name ?? client.ClientId ?? "Service Client"));
            }

            claims.Add(new Claim("access_token", tokenResponse.AccessToken ?? string.Empty));
            claims.Add(new Claim("client_credentials", "true"));

            await httpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    IssuedUtc = DateTimeOffset.UtcNow,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, tokenResponse.ExpiresIn)),
                    AllowRefresh = false
                }).AnyContext();

            return Results.Redirect(AppendQuery(redirectUrl, "identity_message", $"Signed in with client credentials for {client.Name}."));
        }
        catch (OAuth2Exception ex)
        {
            logger?.LogWarning(ex, "Dashboard fake identity provider client-credentials login failed.");
            return Results.Redirect(AppendQuery(redirectUrl, "identity_error", ex.Description ?? ex.Error));
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Dashboard fake identity provider client-credentials login failed.");
            return Results.Redirect(AppendQuery(redirectUrl, "identity_error", "Client credentials login failed."));
        }
    }

    private FakeIdentityProviderClient GetClientCredentialsClient()
    {
        return fakeIdentityProviderOptions?.Clients?.FirstOrDefault(client => client.IsConfidentialClient)
            ?? fakeIdentityProviderOptions?.Clients?.FirstOrDefault();
    }

    private static string BuildLocalReturnUrl(HttpContext httpContext, string path)
    {
        var pathBase = httpContext?.Request.PathBase.Value?.TrimEnd('/') ?? string.Empty;
        return $"{pathBase}/{path.Trim('/')}";
    }

    private string NormalizeDashboardReturnUrl(HttpContext httpContext, string returnUrl)
    {
        var fallback = BuildLocalReturnUrl(httpContext, CombinePath(options?.GroupPath, options?.EndpointPaths?.Identity));
        if (string.IsNullOrWhiteSpace(returnUrl) || !returnUrl.StartsWith("/", StringComparison.Ordinal) || returnUrl.StartsWith("//", StringComparison.Ordinal))
        {
            return fallback;
        }

        return returnUrl;
    }

    private static string AppendQuery(string url, string name, string value)
    {
        var separator = url.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{url}{separator}{Uri.EscapeDataString(name)}={Uri.EscapeDataString(value ?? string.Empty)}";
    }

    private static string CombinePath(string groupPath, string endpointPath)
    {
        return $"/{(groupPath ?? string.Empty).Trim('/')}/{(endpointPath ?? string.Empty).Trim('/')}";
    }
}
