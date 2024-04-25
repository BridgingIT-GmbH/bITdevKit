// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Provides correlation ids to each request to allow log entries grouping.
/// </summary>
public class CorrelationIdProviderMiddleware
{
    private const string CorrelationKey = "CorrelationId";
    private const string FlowKey = "FlowId";
    private const string TraceKey = "TraceId";
    private readonly ILogger logger;
    private readonly RequestDelegate next;
    private readonly IHttpMetricsTagsFeature httpMetricsTags;

    public CorrelationIdProviderMiddleware(
        ILogger<CorrelationIdProviderMiddleware> logger,
        RequestDelegate next,
        IHttpMetricsTagsFeature httpMetricsTags = null)
    {
        EnsureArg.IsNotNull(logger, nameof(logger));
        EnsureArg.IsNotNull(next, nameof(next));

        this.logger = logger;
        this.next = next;
        this.httpMetricsTags = httpMetricsTags;
    }

    public async Task Invoke(HttpContext httpContext)
    {
        EnsureArg.IsNotNull(httpContext, nameof(httpContext));

        httpContext.Request.Headers.TryGetValue(CorrelationKey, out var correlationId);
        //var metricsFeature = httpContext.Features.Get<IHttpMetricsTagsFeature>(); // Enrich the metrics created by ASP.NET Core

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = httpContext.Request.Query[CorrelationKey];
        }

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = GuidGenerator.CreateSequential().ToString("N"); // TODO: or generate a shorter id? https://nima-ara-blog.azurewebsites.net/generating-ids-in-csharp/
        }

        httpContext.Response.Headers.AddOrUpdate(CorrelationKey, correlationId);
        httpContext.Items.AddOrUpdate(CorrelationKey, correlationId.ToString());

        var flowId = GuidGenerator.Create(
                httpContext.Features.Get<IEndpointFeature>()?.Endpoint?.Metadata?.GetMetadata<ControllerActionDescriptor>()?.AttributeRouteInfo?.Template).ToString("N");
        // TODO: is the ControllerActionDescriptor also available when using .net60 minimal apis? if not the flowId will be 00000000000000000000000000000000
        flowId = flowId != "00000000000000000000000000000000" ? flowId : GuidGenerator.Create(httpContext.Request.Path).ToString("N");
        httpContext.Response.Headers.AddOrUpdate(FlowKey, flowId);
        httpContext.Items.AddOrUpdate(FlowKey, flowId.ToString());

        var activity = httpContext.Features.Get<IHttpActivityFeature>()?.Activity; // Enrich the request activity created by ASP.NET Core
        if (activity is not null)
        {
            httpContext.Response.Headers.AddOrUpdate(TraceKey, activity.TraceId.ToString());
        }

        //metricsFeature?.Tags.Add(new KeyValuePair<string, object>(TraceKey, activity?.TraceId.ToString())); // https://learn.microsoft.com/en-us/aspnet/core/log-mon/metrics/metrics?view=aspnetcore-8.0#enrich-the-aspnet-core-request-metric
        //metricsFeature?.Tags.Add(new KeyValuePair<string, object>(CorrelationKey, correlationId));
        //metricsFeature?.Tags.Add(new KeyValuePair<string, object>(FlowKey, flowId));

        using (this.logger.BeginScope(new Dictionary<string, object>
        {
            [TraceKey] = activity?.TraceId.ToString(),
            [CorrelationKey] = correlationId.ToString(),
            [FlowKey] = flowId
        }))
        {
            activity?.SetBaggage(ActivityConstants.CorrelationIdTagKey, correlationId);
            activity?.SetBaggage(ActivityConstants.FlowIdTagKey, flowId);

            await this.next(httpContext); // continue pipeline
        }
    }
}