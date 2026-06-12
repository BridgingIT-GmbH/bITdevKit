// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.AspNetCore.Builder;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.Extensions.DependencyInjection;
using Routing;

/// <summary>
///     Provides application builder extensions for mapping registered endpoint sets.
/// </summary>
/// <remarks>
///     The extension resolves all <see cref="IEndpoints" /> instances from the application service provider and maps only
///     those that are enabled and not yet registered. The same endpoint instance is marked as registered after mapping so
///     repeated calls do not duplicate routes.
/// </remarks>
public static class ApplicationApplicationExtensions
{
    /// <summary>
    ///     Maps all registered endpoint sets into the application or into a supplied route group.
    /// </summary>
    /// <param name="app">The built web application that owns the service provider and endpoint data sources.</param>
    /// <param name="routeGroupBuilder">
    ///     An optional route group that receives the endpoint mappings. When omitted, endpoints are mapped directly into
    ///     <paramref name="app" />.
    /// </param>
    /// <returns>The original <paramref name="app" /> instance for startup pipeline chaining.</returns>
    /// <remarks>
    ///     The method reads <see cref="IEndpoints.Enabled" /> and <see cref="IEndpoints.IsRegistered" /> before invoking
    ///     <see cref="IEndpoints.Map" />. It does not throw when no endpoint services are registered; in that case no routes
    ///     are added.
    ///
    ///     Example:
    ///     <code>
    ///     app.MapEndpoints();
    ///
    ///     var api = app.MapGroup("/api");
    ///     app.MapEndpoints(api);
    ///     </code>
    /// </remarks>
    public static IApplicationBuilder MapEndpoints(this WebApplication app, RouteGroupBuilder routeGroupBuilder = null)
    {
        EnsureArg.IsNotNull(app, nameof(app));

        var endpoints = app.Services.GetService<IEnumerable<IEndpoints>>();
        IEndpointRouteBuilder builder = routeGroupBuilder is null ? app : routeGroupBuilder;

        foreach (var endpoint in endpoints.SafeNull().Where(e => e.Enabled && !e.IsRegistered))
        {
            endpoint.Map(builder);
            endpoint.IsRegistered = true;
        }

        return app;
    }
}