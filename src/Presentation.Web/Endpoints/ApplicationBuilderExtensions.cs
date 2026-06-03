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
///     Provides application pipeline helpers for mapping registered endpoint modules.
/// </summary>
/// <remarks>
///     These extensions are intended to be called after endpoint modules have been registered in dependency injection
///     with one of the <c>AddEndpoints</c> overloads. Mapping is performed against the application route builder or an
///     optional route group.
/// </remarks>
public static class ApplicationApplicationExtensions
{
    /// <summary>
    ///     Maps all enabled, not-yet-registered <see cref="IEndpoints" /> services to the application or route group.
    /// </summary>
    /// <param name="app">The built web application whose service provider contains endpoint registrations.</param>
    /// <param name="routeGroupBuilder">
    ///     An optional route group that receives the endpoint mappings. When omitted, endpoints are mapped directly on
    ///     <paramref name="app" />.
    /// </param>
    /// <returns>The same <see cref="IApplicationBuilder" /> instance so startup calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="app" /> is <c>null</c>.</exception>
    /// <remarks>
    ///     The method resolves <see cref="IEnumerable{T}" /> of <see cref="IEndpoints" /> from dependency injection and
    ///     ignores missing registrations, <c>null</c> entries, disabled endpoint instances, and endpoints already marked as
    ///     registered. For every endpoint that is mapped successfully, <see cref="IEndpoints.IsRegistered" /> is set to
    ///     <c>true</c> to prevent duplicate mapping on later calls.
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