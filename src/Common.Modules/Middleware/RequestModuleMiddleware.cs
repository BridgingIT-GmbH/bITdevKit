// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using EnsureThat;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides a module context for each HTTP request.
/// </summary>
public class RequestModuleMiddleware
{
    private readonly ILogger logger;
    private readonly IEnumerable<IRequestModuleContextAccessor> moduleAccessors;
    private readonly IEnumerable<ActivitySource> activitySources;
    private readonly RequestDelegate next;

    public RequestModuleMiddleware(
        ILogger<RequestModuleMiddleware> logger,
        RequestDelegate next,
        IEnumerable<IRequestModuleContextAccessor> moduleAccessors = null,
        IEnumerable<ActivitySource> activitySources = null)
    {
        EnsureArg.IsNotNull(logger, nameof(logger));
        EnsureArg.IsNotNull(next, nameof(next));

        this.logger = logger;
        this.next = next;
        this.moduleAccessors = moduleAccessors;
        this.activitySources = activitySources;
    }

    public async Task Invoke(HttpContext httpContext)
    {
        EnsureArg.IsNotNull(httpContext, nameof(httpContext));

        var module = this.moduleAccessors.Find(httpContext.Request);
        if (module is not null)
        {
            this.logger.LogInformation("{LogKey} request module: {ModuleName}", "REQ", module.Name);

            var activity = httpContext.Features.Get<IHttpActivityFeature>()?.Activity; // Enrich the request activity created by ASP.NET Core
            activity?.SetBaggage(ActivityConstants.ModuleNameTagKey, module.Name);

            httpContext.Response.Headers.AddOrUpdate(ModuleConstants.ModuleNameKey, module.Name);
            httpContext.Items.AddOrUpdate(ModuleConstants.ModuleNameKey, module.Name);

            using (this.logger.BeginScope(new Dictionary<string, object>
            {
                [ModuleConstants.ModuleNameKey] = module?.Name,
            }))
            {
                if (module.Enabled)
                {
                    httpContext.Features.Get<IHttpMetricsTagsFeature>()?.Tags.Add(new KeyValuePair<string, object>("module_name", module.Name));  // https://learn.microsoft.com/en-us/aspnet/core/log-mon/metrics/metrics?view=aspnetcore-8.0#enrich-the-aspnet-core-request-metric

                    await this.activitySources.Find(module.Name).StartActvity(
                        $"MODULE {module.Name}",
                        async (a, c) => await this.activitySources.Find(module.Name).StartActvity(
                            $"HTTP_INBOUND {httpContext.Request.Method.ToUpperInvariant()} {httpContext.Request.Path}",
                            async (a, c) => await this.next(httpContext).AnyContext(),
                            kind: ActivityKind.Server,
                            //baggages: new Dictionary<string, string> { [ActivityConstants.ModuleNameTagKey] = module.Name },
                            cancellationToken: c));
                }
                else
                {
                    this.logger.LogError("{LogKey} request cancelled, module not enabled (module={ModuleName})", "REQ", module.Name);
                    throw new ModuleNotEnabledException(module.Name);
                }
            }
        }
        else
        {
            await this.next(httpContext); // continue pipeline
        }
    }
}