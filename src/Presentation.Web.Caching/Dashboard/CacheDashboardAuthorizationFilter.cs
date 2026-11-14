// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Caching.Dashboard;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Separates optional Dashboard read restrictions from mutation authorization and protects cookie-authenticated operational forms.</summary>
/// <example><code>group.AddEndpointFilter&lt;CacheDashboardAuthorizationFilter&gt;();</code></example>
public sealed class CacheDashboardAuthorizationFilter(CacheDashboardConfiguration configuration) : IEndpointFilter
{
    /// <inheritdoc />
    public async ValueTask<object> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        http.Response.Headers.CacheControl = "no-store";
        var action = http.GetEndpoint()?.Metadata.GetMetadata<CacheDashboardActionMetadata>()?.Action ?? CacheAccessActions.Read;
        var policy = action == CacheAccessActions.Read ? configuration.ReadPolicy : configuration.InvalidationPolicy;
        if (action != CacheAccessActions.Read || !string.IsNullOrWhiteSpace(policy))
        {
            var authorization = http.RequestServices.GetService<IAuthorizationService>();
            var provider = http.RequestServices.GetService<IAuthorizationPolicyProvider>();
            if (authorization is null || provider is null) { return Results.StatusCode(StatusCodes.Status403Forbidden); }

            var authorized = string.IsNullOrWhiteSpace(policy) ?
                await authorization.AuthorizeAsync(http.User, http, await provider.GetDefaultPolicyAsync().ConfigureAwait(false)).ConfigureAwait(false) :
                await authorization.AuthorizeAsync(http.User, http, policy).ConfigureAwait(false);
            if (!authorized.Succeeded) { return Results.StatusCode(StatusCodes.Status403Forbidden); }
        }

        if (HttpMethods.IsGet(http.Request.Method) && http.GetEndpoint()?.Metadata.GetMetadata<CacheDashboardFormPageMetadata>() is not null)
        {
            http.Items[typeof(CacheDashboardAuthorizationFilter)] = http.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(http);
        }

        if (HttpMethods.IsPost(http.Request.Method))
        {
            if (http.Request.ContentLength > 65536) { return Failure("The operation form exceeds its finite bound."); }

            http.Request.Body = new CacheHttpBoundedReadStream(http.Request.Body, 65536);
        }

        if (action != CacheAccessActions.Read)
        {
            try { await http.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(http).ConfigureAwait(false); }
            catch (Exception exception) when (exception is AntiforgeryValidationException or InvalidDataException)
            {
                return Failure("The operation form could not be validated.");
            }
        }

        return await next(context).ConfigureAwait(false);
    }

    private static Microsoft.AspNetCore.Http.IResult Failure(string message) => CacheHttpResultMapper.Failure(
        Result.Failure(new CacheError(CacheErrorCode.Validation, message)));
}

/// <summary>Classifies the current Dashboard route independently of an anonymous Dashboard host's metadata.</summary>
/// <param name="Action">Read or invalidation/control authority.</param>
/// <example><code>route.WithMetadata(new CacheDashboardActionMetadata(CacheAccessActions.Invalidate));</code></example>
public sealed record CacheDashboardActionMetadata(CacheAccessActions Action);

/// <summary>Marks a full page whose operational forms need antiforgery tokens before the shared layout starts the response.</summary>
/// <example><code>route.WithMetadata(new CacheDashboardFormPageMetadata());</code></example>
public sealed record CacheDashboardFormPageMetadata;