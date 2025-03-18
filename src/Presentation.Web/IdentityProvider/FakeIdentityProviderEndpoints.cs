// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System.Net;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.IdentityProvider.Pages;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using IResult = Microsoft.AspNetCore.Http.IResult;

public class FakeIdentityProviderEndpoints(
    ILogger<FakeIdentityProviderEndpoints> logger,
    FakeIdentityProviderEndpointsOptions options,
    IFakeIdentityProvider identityProvider,
    IUserInfoService userInfoService) : EndpointsBase
{
    public override void Map(IEndpointRouteBuilder app)
    {
        options ??= new FakeIdentityProviderEndpointsOptions();

        if (!options.Enabled)
        {
            return;
        }

        var group = this.MapGroup(app, options)
            .DisableAntiforgery()
            .RequireCors(nameof(IdentityProvider));
        var paths = options.EndpointPaths;

        group.MapGet("/", this.HandleIndex)
            .WithDescription("Shows the dashboard index page.")
            .Produces<string>((int)HttpStatusCode.OK)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest).ExcludeFromDescription();

        group.MapGet(paths.WellKnownConfiguration, this.GetConfiguration)
            .WithDescription("Returns the OpenID Connect discovery document.")
            .Produces<OpenIdConfiguration>().AllowAnonymous();

        app.MapGet(paths.WellKnownConfiguration, this.GetConfiguration)
            .WithDescription("Returns the OpenID Connect discovery document (root level).")
            .Produces<OpenIdConfiguration>().AllowAnonymous().ExcludeFromDescription();

        group.MapGet(paths.Authorize, this.HandleAuthorize)
            .WithDescription("Shows the signin page for user selection.")
            .Produces<string>((int)HttpStatusCode.OK)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest);

        group.MapGet(paths.AuthorizeCallback, this.HandleAuthorizeCallBack)
            .WithDescription("Handles the user selection and generates authorization code.")
            .Produces((int)HttpStatusCode.Redirect)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest);

        group.MapPost(paths.Token, this.HandleTokenRequest)
            .WithDescription("Issues tokens for various grant types.")
            .Accepts<IFormCollection>("application/x-www-form-urlencoded")
            .Produces<TokenResponse>()
            .Produces<OAuth2Error>((int)HttpStatusCode.BadRequest);

        group.MapGet(paths.UserInfo, this.GetUserInfo)
            .WithDescription("Returns information about the authenticated user.")
            .Produces<UserInfoResponse>()
            .Produces<ProblemDetails>((int)HttpStatusCode.Unauthorized);

        group.MapGet(paths.Logout, this.HandleLogout)
            .WithDescription("Handles user logout.")
            .Produces((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.Redirect);

        group.MapGet(paths.DebugInfo, this.GetDebugInfo)
            .WithDescription("Returns debug information about the identity provider configuration.")
            .Produces<DebugInfoResponse>();
    }

    private Task<IResult> HandleIndex()
    {
        return Task.FromResult<IResult>(
            Results.Extensions.RazorSlice<Pages.Index>());
    }

    private Task<IResult> HandleAuthorize(
        [FromQuery] string response_type,
        [FromQuery] string client_id,
        [FromQuery] string redirect_uri,
        [FromQuery] string scope,
        [FromQuery] string state)
    {
        response_type = response_type?.Trim();
        client_id = client_id?.Trim();
        redirect_uri = redirect_uri?.Trim();
        scope = scope.Distinct();
        state = state?.Trim();

        if (response_type != "code")
        {
            return Task.FromResult<IResult>(TypedResults.BadRequest(new OAuth2Error
            {
                Error = "unsupported_response_type",
                ErrorDescription = "Only 'code' response type is supported"
            }));
        }

        if (options.Clients.SafeAny())
        {
            var client = options.Clients.FirstOrDefault(c => c.ClientId == client_id);
            if (client == null)
            {
                return Task.FromResult<IResult>(TypedResults.BadRequest(new OAuth2Error
                {
                    Error = "invalid_client",
                    ErrorDescription = $"Invalid client '{client_id}'"
                }));
            }

            if (!client.RedirectUris.Contains(redirect_uri))
            {
                return Task.FromResult<IResult>(TypedResults.BadRequest(new OAuth2Error
                {
                    Error = "invalid_request",
                    ErrorDescription = $"Invalid redirect URI '{redirect_uri}' for client '{client.Name}' ({client.ClientId}). Valid URIs are: {string.Join(", ", client.RedirectUris)}"
                }));
            }
        }

        var request = new AuthorizeRequest
        {
            ResponseType = response_type,
            ClientId = client_id,
            RedirectUri = redirect_uri,
            Scope = scope,
            State = state
        };

        return Task.FromResult<IResult>(
            Results.Extensions.RazorSlice<Signin, SigninViewModel>(new SigninViewModel { Request = request, Options = options }));
    }

    private Task<IResult> HandleAuthorizeCallBack(
        [FromQuery] string email,
        [FromQuery] string password,
        [FromQuery] string response_type,
        [FromQuery] string client_id,
        [FromQuery] string redirect_uri,
        [FromQuery] string scope,
        [FromQuery] string state)
    {
        try
        {
            email = email?.Trim();
            password = password?.Trim();
            response_type = response_type?.Trim();
            client_id = client_id?.Trim();
            redirect_uri = redirect_uri?.Trim();
            scope = scope.Distinct();
            state = state?.Trim();

            if (options.Clients.SafeAny())
            {
                var client = options.Clients.FirstOrDefault(c => c.ClientId == client_id);
                if (client == null)
                {
                    return Task.FromResult<IResult>(TypedResults.BadRequest(new OAuth2Error
                    {
                        Error = "invalid_client",
                        ErrorDescription = $"Invalid client '{client_id}'"
                    }));
                }

                if (!client.RedirectUris.Contains(redirect_uri))
                {
                    return Task.FromResult<IResult>(TypedResults.BadRequest(new OAuth2Error
                    {
                        Error = "invalid_request",
                        ErrorDescription = $"Invalid redirect URI '{redirect_uri}' for client '{client.Name}' ({client.ClientId}). Valid URIs are: {string.Join(", ", client.RedirectUris)}"
                    }));
                }
            }

            var request = new AuthorizeRequest
            {
                ResponseType = response_type,
                ClientId = client_id,
                RedirectUri = redirect_uri,
                Scope = scope,
                State = state
            };

            var code = identityProvider.GenerateAuthorizationCode(email, password, request);
            var redirectUrl = $"{redirect_uri}?code={code}&state={state}";

            return Task.FromResult<IResult>(TypedResults.Redirect(redirectUrl).WithOAuthHeaders());
        }
        catch (OAuth2Exception ex)
        {
            return Task.FromResult<IResult>(TypedResults.BadRequest(new OAuth2Error
            {
                Error = ex.Error,
                ErrorDescription = ex.Description
            }));
        }
    }

    private async Task<IResult> HandleTokenRequest(
        HttpContext httpContext,
        [FromForm] string grant_type,
        [FromForm] string client_id,
        [FromForm] string client_secret = null,
        [FromForm] string code = null,
        [FromForm] string redirect_uri = null,
        [FromForm] string refresh_token = null,
        [FromForm] string username = null,
        [FromForm] string password = null,
        [FromForm] string scope = null)
    {
        try
        {
            grant_type = grant_type?.Trim().EmptyToNull();
            client_id = client_id?.Trim().EmptyToNull();
            client_secret = client_secret?.Trim().EmptyToNull();
            code = code?.Trim();
            redirect_uri = redirect_uri?.Trim().EmptyToNull();
            refresh_token = refresh_token?.Trim().EmptyToNull();
            username = username?.Trim().EmptyToNull();
            password = password?.Trim().EmptyToNull();
            scope = scope.Distinct();

            var response = await (grant_type switch
            {
                "authorization_code" => identityProvider.HandleAuthorizationCodeGrantAsync(code, client_id, client_secret, scope, httpContext),
                "password" => identityProvider.HandlePasswordGrantAsync(client_id, username, password, scope),
                "client_credentials" => identityProvider.HandleClientCredentialsGrantAsync(client_id, client_secret, scope),
                "refresh_token" => identityProvider.HandleRefreshTokenGrantAsync(refresh_token, client_id, scope, httpContext),
                _ => throw new OAuth2Exception("unsupported_grant_type", "The grant type is not supported")
            });

            return TypedResults.Ok(response).WithOAuthHeaders();
        }
        catch (OAuth2Exception ex)
        {
            return TypedResults.BadRequest(new OAuth2Error
            {
                Error = ex.Error,
                ErrorDescription = ex.Description
            }).WithOAuthHeaders();
        }
    }

    private IResult GetUserInfo(HttpRequest request)
    {
        var authHeader = request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader))
        {
            return TypedResults.Unauthorized();
        }

        var parts = authHeader.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !parts[0].Equals("Bearer", StringComparison.OrdinalIgnoreCase))
        {
            return TypedResults.Unauthorized();
        }

        var token = parts[1];

        try
        {
            var userInfo = userInfoService.GetUserInfo(token);
            return TypedResults.Ok(userInfo).WithOAuthHeaders();
        }
        catch (OAuth2Exception ex)
        {
            return TypedResults.BadRequest(new OAuth2Error
            {
                Error = ex.Error,
                ErrorDescription = ex.Description
            }).WithOAuthHeaders();
        }
        catch (UnauthorizedAccessException)
        {
            return TypedResults.Unauthorized().WithOAuthHeaders();
        }
    }

    private async Task<IResult> HandleLogout(
        HttpContext httpContext,
        [FromQuery] string id_token_hint,
        [FromQuery] string post_logout_redirect_uri,
        [FromQuery] string state)
    {
        id_token_hint = id_token_hint?.Trim();
        post_logout_redirect_uri = post_logout_redirect_uri?.Trim();
        state = state?.Trim();

        if (options.EnablePersistentRefreshTokens)
        {
            try
            {
                await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogError(ex, "Failed to sign out user");
            }
        }

        if (!string.IsNullOrEmpty(post_logout_redirect_uri))
        {
            var redirectUrl = post_logout_redirect_uri;

            if (!string.IsNullOrEmpty(state))
            {
                redirectUrl = $"{redirectUrl}?state={state}";
            }

            return TypedResults.Redirect(redirectUrl).WithOAuthHeaders();
        }

        return TypedResults.Ok().WithOAuthHeaders();
    }

    private IResult GetConfiguration()
    {
        var baseUrl = options.Issuer.TrimEnd('/');
        var response = TypedResults.Ok(new OpenIdConfiguration
        {
            Issuer = baseUrl,
            AuthorizationEndpoint = $"{baseUrl}{options.GroupPath}{options.EndpointPaths.Authorize}",
            TokenEndpoint = $"{baseUrl}{options.GroupPath}{options.EndpointPaths.Token}",
            UserInfoEndpoint = $"{baseUrl}{options.GroupPath}{options.EndpointPaths.UserInfo}",
            EndSessionEndpoint = $"{baseUrl}{options.GroupPath}{options.EndpointPaths.Logout}"
        });

        return response.WithOAuthHeaders();
    }

    private IResult GetDebugInfo()
    {
        var debugInfo = new DebugInfoResponse
        {
            TokenIssuer = options.Issuer,
            TokenProvider = options.TokenProvider.ToString(),
            ConfiguredClients = [.. options.Clients.Select(client => new DevClientDebugInfo
            {
                ClientId = client.ClientId,
                Name = client.Name,
                RedirectUris = client.RedirectUris,
                AllowedScopes = client.AllowedScopes
            })],
            ConfiguredUsers = [.. options.Users.Select(user => new UserDebugInfo
            {
                Id = user.Id,
                Email = user.Email,
                Name = user.Name,
                Roles = user.Roles,
                IsDefault = user.IsDefault
            })],
            Endpoints = new EndpointDebugInfo
            {
                Authorization = $"{options.Issuer.TrimEnd('/')}{options.GroupPath}{options.EndpointPaths.Authorize}",
                Token = $"{options.Issuer.TrimEnd('/')}{options.GroupPath}{options.EndpointPaths.Token}",
                UserInfo = $"{options.Issuer.TrimEnd('/')}{options.GroupPath}{options.EndpointPaths.UserInfo}",
                Logout = $"{options.Issuer.TrimEnd('/')}{options.GroupPath}{options.EndpointPaths.Logout}"
            }
        };

        return TypedResults.Ok(debugInfo).WithOAuthHeaders();
    }
}