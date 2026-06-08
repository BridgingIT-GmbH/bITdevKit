// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

/// <summary>
///     Provides shared endpoint registration state and route group configuration for Minimal API endpoint modules.
/// </summary>
/// <remarks>
///     Derived endpoint modules implement <see cref="Map" /> to add their routes to an <see cref="IEndpointRouteBuilder" />.
///     The application's endpoint mapper uses <see cref="Enabled" /> and <see cref="IsRegistered" /> to avoid mapping disabled
///     endpoint modules or registering the same instance more than once.
/// </remarks>
public abstract class EndpointsBase : IEndpoints
{
    /// <summary>
    ///     Gets or sets whether this endpoint module participates in application endpoint registration.
    /// </summary>
    /// <remarks>
    ///     The value defaults to <c>true</c>. When the application maps registered <see cref="IEndpoints" /> instances, endpoint
    ///     modules with this value set to <c>false</c> are skipped and their <see cref="Map" /> method is not called.
    /// </remarks>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether this endpoint module has already been mapped by the application.
    /// </summary>
    /// <remarks>
    ///     The application endpoint mapper sets this value after calling <see cref="Map" />. It is used together with
    ///     <see cref="Enabled" /> to prevent duplicate route registration for the same endpoint instance.
    /// </remarks>
    public bool IsRegistered { get; set; }

    /// <summary>
    ///     Maps the routes owned by the derived endpoint module to the supplied route builder.
    /// </summary>
    /// <param name="app">The application or route group builder that receives the endpoint routes.</param>
    /// <remarks>
    ///     Implementations typically check their endpoint options before mapping routes, create a route group through
    ///     <see cref="MapGroup" />, and then add Minimal API handlers and metadata to that group. The application endpoint
    ///     mapper calls this method only for endpoint instances that are enabled and not yet registered.
    /// </remarks>
    public abstract void Map(IEndpointRouteBuilder app);

    /// <summary>
    ///     Creates a route group using the configured group path, tag, API description, and authorization options.
    /// </summary>
    /// <param name="app">The route builder that receives the new group.</param>
    /// <param name="options">The endpoint options that define group metadata and authorization requirements.</param>
    /// <returns>
    ///     A configured <see cref="RouteGroupBuilder" /> that callers can use to map individual routes.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="app" /> or <paramref name="options" /> is <c>null</c>.
    /// </exception>
    /// <remarks>
    ///     The group is created at <see cref="EndpointsOptionsBase.GroupPath" /> and tagged with
    ///     <see cref="EndpointsOptionsBase.GroupTag" />. When API description exclusion is enabled, the group is hidden from
    ///     generated descriptions. When authorization is required, role-based authorization takes precedence over policy-based
    ///     authorization; empty or whitespace role names are ignored. If no roles or policy are configured, the group requires
    ///     the default authorization policy.
    ///
    ///     Example:
    ///     <code>
    ///     public override void Map(IEndpointRouteBuilder app)
    ///     {
    ///         var group = this.MapGroup(app, this.options);
    ///
    ///         group.MapGet("/status", () =&gt; Results.Ok());
    ///     }
    ///     </code>
    /// </remarks>
    public RouteGroupBuilder MapGroup(IEndpointRouteBuilder app, EndpointsOptionsBase options)
    {
        EnsureArg.IsNotNull(app, nameof(app));
        EnsureArg.IsNotNull(options, nameof(options));

        var group = app.MapGroup(options.GroupPath)
            .WithTags(options.GroupTag);

        if (options.ExcludeFromDescription)
        {
            group.ExcludeFromDescription();
        }

        if (options.RequireAuthorization)
        {
            var roles = options.RequireRoles
                .SafeNull()
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .ToArray();

            if (roles.SafeAny())
            {
                group.RequireAuthorization(
                    new AuthorizeAttribute
                    {
                        Roles = string.Join(",", roles)
                    });
            }
            else if (!options.RequirePolicy.IsNullOrEmpty())
            {
                group.RequireAuthorization(
                    new AuthorizeAttribute(options.RequirePolicy));
            }
            else
            {
                group.RequireAuthorization();
            }
        }

        return group;
    }
}