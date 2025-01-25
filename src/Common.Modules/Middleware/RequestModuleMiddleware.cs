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
        var moduleName = module?.Name ?? ModuleConstants.UnknownModuleName;

        if (module is not null)
        {
            this.logger.LogInformation("{LogKey} request module: {ModuleName}", "REQ", moduleName);

            // Enrich the request activity created by ASP.NET Core
            var activity = httpContext.Features.Get<IHttpActivityFeature>()?.Activity;
            activity?.SetBaggage(ActivityConstants.ModuleNameTagKey, moduleName);

            httpContext.Response.Headers.AddOrUpdate(ModuleConstants.ModuleNameKey, moduleName);
            httpContext.Items.AddOrUpdate(ModuleConstants.ModuleNameKey, moduleName);

            using (this.logger.BeginScope(new Dictionary<string, object>
            {
                [ModuleConstants.ModuleNameKey] = module.Name
            }))
            {
                if (module.Enabled)
                {
                    // https://learn.microsoft.com/en-us/aspnet/core/log-mon/metrics/metrics?view=aspnetcore-8.0#enrich-the-aspnet-core-request-metric
                    httpContext.Features.Get<IHttpMetricsTagsFeature>()?.Tags.Add(new KeyValuePair<string, object>("module_name", moduleName));

                    await this.activitySources.Find(moduleName)
                        .StartActvity($"MODULE {moduleName}",
                            async (a, c) => await this.activitySources.Find(moduleName)
                                .StartActvity(
                                    $"HTTP_INBOUND {httpContext.Request.Method.ToUpperInvariant()} {httpContext.Request.Path}",
                                    async (a, c) => await this.next(httpContext).AnyContext(),
                                    ActivityKind.Server,
                                    tags: new Dictionary<string, string>
                                    {
                                        ["request.module.origin"] = moduleName,
                                        ["request.method"] = httpContext.Request.Method,
                                        ["request.path"] = httpContext.Request.Path
                                    },
                                    baggages: new Dictionary<string, string>
                                    {
                                        [ActivityConstants.ModuleNameTagKey] = moduleName,
                                    },
                                    cancellationToken: c));
                }
                else
                {
                    this.logger.LogError("{LogKey} request cancelled, module not enabled (module={ModuleName})", "REQ", moduleName);

                    throw new ModuleNotEnabledException(moduleName);
                }
            }
        }
        else
        {
            await this.next(httpContext); // continue pipeline
        }
    }
}