// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;

/// <summary>
///     Handles dashboard authorization failures without redirecting to host application access-denied pages.
/// </summary>
/// <example>
/// <code>
/// services.AddSingleton&lt;IAuthorizationMiddlewareResultHandler, DashboardAuthorizationMiddlewareResultHandler&gt;();
/// </code>
/// </example>
public sealed class DashboardAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler defaultHandler = new();
    private readonly DashboardEndpointsOptions options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DashboardAuthorizationMiddlewareResultHandler" /> class.
    /// </summary>
    /// <param name="options">The dashboard endpoint options.</param>
    /// <example>
    /// <code>
    /// var handler = new DashboardAuthorizationMiddlewareResultHandler(new DashboardEndpointsOptions());
    /// </code>
    /// </example>
    public DashboardAuthorizationMiddlewareResultHandler(DashboardEndpointsOptions options)
    {
        this.options = options ?? new DashboardEndpointsOptions();
    }

    /// <inheritdoc />
    public Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden && this.IsDashboardRequest(context.Request))
        {
            context.Response.Redirect(this.CreateDashboardAccessDeniedUrl(context));

            return Task.CompletedTask;
        }

        return this.defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }

    private bool IsDashboardRequest(HttpRequest request)
    {
        var dashboardPath = new PathString(DashboardPath.Combine(this.options.GroupPath));

        return request.Path.StartsWithSegments(dashboardPath, StringComparison.OrdinalIgnoreCase);
    }

    private string CreateDashboardAccessDeniedUrl(HttpContext context)
    {
        var accessDeniedPath = DashboardPath.Combine(
            context.Request.PathBase,
            this.options.GroupPath,
            this.options.EndpointPaths.AccessDenied);

        var returnUrl = $"{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}";

        return $"{accessDeniedPath}{QueryString.Create("ReturnUrl", returnUrl)}";
    }
}
