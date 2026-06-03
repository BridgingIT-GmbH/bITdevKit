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
///     Provides common state and grouping behavior for endpoint modules.
/// </summary>
/// <remarks>
///     Derived types implement <see cref="Map" /> to add Minimal API routes. The base implementation supplies mapping
///     state used by the application pipeline and a helper for applying shared group metadata and authorization settings.
/// </remarks>
public abstract class EndpointsBase : IEndpoints
{
    /// <summary>
    ///     Gets or sets whether this endpoint module should be mapped.
    /// </summary>
    /// <remarks>
    ///     The default value is <c>true</c>. Mapping extensions skip disabled endpoint modules without removing them from
    ///     dependency injection.
    /// </remarks>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether this endpoint module has already been mapped.
    /// </summary>
    /// <remarks>
    ///     The mapping extension sets this value after <see cref="Map" /> succeeds so later mapping calls do not duplicate
    ///     routes for the same endpoint instance.
    /// </remarks>
    public bool IsRegistered { get; set; }

    /// <summary>
    ///     Adds this endpoint module's routes and metadata to the specified route builder.
    /// </summary>
    /// <param name="app">The route builder or route group that receives the endpoint mappings.</param>
    /// <remarks>
    ///     Implementations usually call <see cref="MapGroup" /> first and then add individual route handlers to the
    ///     returned group.
    /// </remarks>
    public abstract void Map(IEndpointRouteBuilder app);

    /// <summary>
    ///     Creates a route group using the supplied endpoint options and applies common endpoint metadata.
    /// </summary>
    /// <param name="app">The route builder that receives the group.</param>
    /// <param name="options">The group path, tag, visibility, and authorization settings to apply.</param>
    /// <returns>The configured <see cref="RouteGroupBuilder" /> that callers can use to add routes.</returns>
    /// <remarks>
    ///     The group is created at <see cref="EndpointsOptionsBase.GroupPath" /> and tagged with
    ///     <see cref="EndpointsOptionsBase.GroupTag" />. When configured, the group is excluded from API descriptions. If
    ///     authorization is enabled, role requirements take precedence over policy requirements; if neither roles nor a
    ///     policy are supplied, default authorization is required.
    ///
    ///     Example:
    ///     <code>
    ///     public override void Map(IEndpointRouteBuilder app)
    ///     {
    ///         var group = this.MapGroup(app, this.options);
    ///         group.MapGet("items", () =&gt; Results.Ok());
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