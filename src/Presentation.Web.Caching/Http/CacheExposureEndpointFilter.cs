// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Caching;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Application.Caching;
using Microsoft.AspNetCore.Http;

/// <summary>Checks all explicitly selected HTTP collections after authorization and before body parsing or requester dispatch.</summary>
/// <example><code>route.AddEndpointFilter&lt;CacheExposureEndpointFilter&gt;();</code></example>
public sealed class CacheExposureEndpointFilter(CacheWebConfiguration configuration, ICacheHttpAccessContextFactory accessFactory,
    CacheManagementAccessValidator authorization) : IEndpointFilter
{
    /// <inheritdoc />
    public ValueTask<object> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        var route = context.HttpContext.Request.RouteValues["collection"]?.ToString();
        var targets = route is null ? context.HttpContext.Request.Query["collections"].ToArray() : [route];
        if (route is null && context.HttpContext.Request.Path.Value?.EndsWith("/collections", StringComparison.OrdinalIgnoreCase) == true)
        {
            return next(context);
        }

        var validation = configuration.ValidateTargets(targets);
        if (validation.IsFailure) { return ValueTask.FromResult<object>(validation.MapHttpNoContent()); }

        var action = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<CacheHttpActionMetadata>()?.Action ?? CacheAccessActions.Read;
        var access = accessFactory.Create(context.HttpContext, action);
        foreach (var target in targets)
        {
            var allowed = authorization.Validate(access, target, action);
            if (allowed.IsFailure) { return ValueTask.FromResult<object>(allowed.MapHttpNoContent()); }
        }

        if (HttpMethods.IsPost(context.HttpContext.Request.Method) &&
            context.HttpContext.Request.ContentType?.Split(';')[0].Trim().Equals("application/json", StringComparison.OrdinalIgnoreCase) != true)
        {
            return ValueTask.FromResult<object>(Result.Failure(new CacheError(CacheErrorCode.Validation,
                "Structured cache requests require the application/json media type.")).MapHttpNoContent());
        }

        return next(context);
    }
}

/// <summary>Identifies the host-authorized external action before any request body is read.</summary>
/// <param name="Action">The current read, publication, or invalidation authority.</param>
/// <example><code>route.WithMetadata(new CacheHttpActionMetadata(CacheAccessActions.Read));</code></example>
public sealed record CacheHttpActionMetadata(CacheAccessActions Action);