// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System.Diagnostics;
using Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

/// <summary>
///     Provides correlation ids to each request to allow log entries grouping.
/// </summary>
/// <remarks>
///     Register this middleware after routing so flow identifiers can use endpoint route patterns.
///     Identifiers are cached in the current <see cref="HttpContext"/> and remain stable when the
///     pipeline is registered more than once or re-executed by exception handling middleware.
///     An inbound correlation identifier is accepted from the header first and then the query string
///     when it is a single value of at most 128 ASCII letters, digits, hyphens, underscores, periods,
///     or colons. Invalid values are silently replaced with a generated identifier.
/// </remarks>
/// <example><code>app.UseRequestCorrelation();</code></example>
public class CorrelationIdProviderMiddleware
{
    private const int GeneratedIdLength = 12;
    private const string CorrelationKey = CorrelationId.HeaderName;
    private const string FlowKey = "FlowId";
    private const string TraceKey = "TraceId";
    private static readonly object RequestStateKey = new();
    private readonly ILogger logger;
    private readonly RequestDelegate next;

    /// <summary>
    /// Initializes a new instance of the <see cref="CorrelationIdProviderMiddleware"/> class.
    /// </summary>
    /// <param name="logger">The structured logger.</param>
    /// <param name="next">The next middleware in the request pipeline.</param>
    /// <example><code>app.UseMiddleware&lt;CorrelationIdProviderMiddleware&gt;();</code></example>
    public CorrelationIdProviderMiddleware(
        ILogger<CorrelationIdProviderMiddleware> logger,
        RequestDelegate next)
    {
        EnsureArg.IsNotNull(logger, nameof(logger));
        EnsureArg.IsNotNull(next, nameof(next));

        this.logger = logger;
        this.next = next;
    }

    /// <summary>
    /// Resolves the request correlation identifier and makes it ambient for the remaining pipeline.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <returns>A task that represents request pipeline execution.</returns>
    /// <example><code>await middleware.Invoke(httpContext);</code></example>
    public async Task Invoke(HttpContext httpContext)
    {
        EnsureArg.IsNotNull(httpContext, nameof(httpContext));

        var state = GetOrCreateRequestState(httpContext);
        ApplyRequestState(httpContext, state);

        var activity = httpContext.Features.Get<IHttpActivityFeature>()?.Activity
            ?? Activity.Current;
        var traceId = activity?.TraceId.ToString();
        if (!string.IsNullOrWhiteSpace(traceId))
        {
            httpContext.Response.Headers.AddOrUpdate(TraceKey, traceId);
        }

        using (CorrelationId.BeginScope(state.CorrelationId))
        using (this.logger.BeginScope(new Dictionary<string, object>
        {
            [TraceKey] = traceId,
            [CorrelationKey] = state.CorrelationId,
            [FlowKey] = state.FlowId
        }))
        {
            activity?.SetBaggage(
                ActivityConstants.CorrelationIdTagKey,
                state.CorrelationId
            );
            activity?.SetBaggage(ActivityConstants.FlowIdTagKey, state.FlowId);

            await this.next(httpContext); // continue pipeline
        }
    }

    private static RequestCorrelationState GetOrCreateRequestState(
        HttpContext httpContext)
    {
        if (httpContext.Items.TryGetValue(RequestStateKey, out var value)
            && value is RequestCorrelationState state)
        {
            return state;
        }

        state = new RequestCorrelationState(
            ResolveCorrelationId(httpContext),
            CreateFlowId(httpContext)
        );
        httpContext.Items[RequestStateKey] = state;

        return state;
    }

    private static void ApplyRequestState(
        HttpContext httpContext,
        RequestCorrelationState state)
    {
        httpContext.Items[CorrelationKey] = state.CorrelationId;
        httpContext.Items[FlowKey] = state.FlowId;
        httpContext.Response.Headers.AddOrUpdate(
            CorrelationKey,
            state.CorrelationId
        );
        httpContext.Response.Headers.AddOrUpdate(FlowKey, state.FlowId);
    }

    private static string ResolveCorrelationId(HttpContext httpContext)
    {
        if (TryGetValidCorrelationId(
                httpContext.Request.Headers[CorrelationKey],
                out var correlationId)
            || TryGetValidCorrelationId(
                httpContext.Request.Query[CorrelationKey],
                out correlationId))
        {
            return correlationId;
        }

        return KeyGenerator.CreateLowercase(GeneratedIdLength);
    }

    private static bool TryGetValidCorrelationId(
        StringValues values,
        out string correlationId)
    {
        correlationId = null;
        if (values.Count != 1)
        {
            return false;
        }

        var candidate = values[0];
        if (!CorrelationId.IsValid(candidate))
        {
            return false;
        }

        correlationId = candidate;
        return true;
    }

    private static string CreateFlowId(HttpContext httpContext)
    {
        var flowKey = $"{httpContext.Request.Method.ToUpperInvariant()} {ResolveRoute(httpContext)}";
        var flowGuid = GuidGenerator.Create(flowKey);

        return HashHelper.Compute(flowGuid.ToByteArray())[..GeneratedIdLength];
    }

    private static string ResolveRoute(HttpContext httpContext)
    {
        var endpoint = httpContext.GetEndpoint();
        var routePattern = (endpoint as RouteEndpoint)?.RoutePattern.RawText;
        var controller = endpoint?.Metadata.GetMetadata<ControllerActionDescriptor>();

        if (controller is not null
            && string.IsNullOrWhiteSpace(controller.AttributeRouteInfo?.Template))
        {
            return $"{routePattern ?? httpContext.Request.Path.Value}"
                + $"|controller={controller.ControllerName}"
                + $"|action={controller.ActionName}";
        }

        return routePattern
            ?? controller?.AttributeRouteInfo?.Template
            ?? httpContext.Request.Path.Value
            ?? string.Empty;
    }

    private sealed record RequestCorrelationState(
        string CorrelationId,
        string FlowId);
}
