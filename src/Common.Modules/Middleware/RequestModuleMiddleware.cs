// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;

/// <summary>
///     Middleware responsible for providing a module context for each HTTP request.
/// </summary>
public class RequestModuleMiddleware
{
    /// <summary>
    ///     A collection of <see cref="ActivitySource" /> instances used for tracing and diagnostics.
    /// </summary>
    private readonly IEnumerable<ActivitySource> activitySources;

    /// <summary>
    ///     Logger instance to record logs related to the request module processing.
    /// </summary>
    private readonly ILogger logger;

    /// <summary>
    ///     A collection of module accessors used to find and provide context for different modules
    ///     during an HTTP request.
    /// </summary>
    private readonly IEnumerable<IRequestModuleContextAccessor> moduleAccessors;

    /// <summary>
    ///     Represents the next delegate in the HTTP request pipeline.
    /// </summary>
    private readonly RequestDelegate next;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RequestModuleMiddleware" /> class.
    ///     Provides a middleware component that processes HTTP requests and manages module context.
    /// </summary>
    /// <remarks>
    ///     This middleware inspects incoming HTTP requests, determines the appropriate module
    ///     context, and logs relevant information. It uses activity from the ASP.NET Core pipeline
    ///     for tracing and enriches it with module-specific data. If a module context is
    ///     successfully determined and enabled, it processes the request accordingly; otherwise,
    ///     it passes the request to the next middleware component.
    /// </remarks>
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

    /// <summary>
    ///     Provides a module context for each HTTP request.
    /// </summary>
    /// <param name="httpContext">The HTTP context for the current request.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task Invoke(HttpContext httpContext)
    {
        EnsureArg.IsNotNull(httpContext, nameof(httpContext));

        var module = this.moduleAccessors.Find(httpContext.Request);
        if (module is not null)
        {
            this.logger.LogInformation("{LogKey} request module: {ModuleName}", "REQ", module.Name);

            var activity = httpContext.Features.Get<IHttpActivityFeature>()
                ?.Activity; // Enrich the request activity created by ASP.NET Core
            activity?.SetBaggage(ActivityConstants.ModuleNameTagKey, module.Name);

            httpContext.Response.Headers.AddOrUpdate(ModuleConstants.ModuleNameKey, module.Name);
            httpContext.Items.AddOrUpdate(ModuleConstants.ModuleNameKey, module.Name);

            using (this.logger.BeginScope(new Dictionary<string, object>
                   {
                       [ModuleConstants.ModuleNameKey] = module?.Name
                   }))
            {
                if (module.Enabled)
                {
                    httpContext.Features.Get<IHttpMetricsTagsFeature>()
                        ?.Tags
                        .Add(new KeyValuePair<string, object>("module_name",
                            module.Name)); // https://learn.microsoft.com/en-us/aspnet/core/log-mon/metrics/metrics?view=aspnetcore-8.0#enrich-the-aspnet-core-request-metric

                    await this.activitySources.Find(module.Name)
                        .StartActvity($"MODULE {module.Name}",
                            async (a, c) => await this.activitySources.Find(module.Name)
                                .StartActvity(
                                    $"HTTP_INBOUND {httpContext.Request.Method.ToUpperInvariant()} {httpContext.Request.Path}",
                                    async (a, c) => await this.next(httpContext).AnyContext(),
                                    ActivityKind.Server,
                                    //baggages: new Dictionary<string, string> { [ActivityConstants.ModuleNameTagKey] = module.Name },
                                    cancellationToken: c));
                }
                else
                {
                    this.logger.LogError("{LogKey} request cancelled, module not enabled (module={ModuleName})",
                        "REQ",
                        module.Name);
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