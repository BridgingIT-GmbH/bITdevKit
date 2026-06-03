// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.AspNetCore.Routing;

/// <summary>
///     Defines a module that contributes Minimal API routes to an endpoint route builder.
/// </summary>
/// <remarks>
///     Implementations usually group related routes for a feature or system component. Instances are registered in
///     dependency injection and later mapped by <c>MapEndpoints</c>, which honors <see cref="Enabled" /> and updates
///     <see cref="IsRegistered" /> after mapping.
/// </remarks>
public interface IEndpoints
{
    /// <summary>
    ///     Gets or sets whether this endpoint module should be mapped by the application pipeline.
    /// </summary>
    /// <remarks>
    ///     The mapping extension skips implementations with this value set to <c>false</c>. The value does not remove an
    ///     endpoint module from dependency injection; it only controls whether routes are mapped.
    /// </remarks>
    bool Enabled { get; set; }

    /// <summary>
    ///     Gets or sets whether this endpoint module has already contributed its routes to the route builder.
    /// </summary>
    /// <remarks>
    ///     The mapping extension uses this flag to avoid invoking <see cref="Map" /> multiple times for the same endpoint
    ///     instance. Implementations normally do not need to set it themselves.
    /// </remarks>
    bool IsRegistered { get; set; }

    /// <summary>
    ///     Adds this module's routes and metadata to the specified route builder.
    /// </summary>
    /// <param name="app">The route builder or route group that receives the endpoint mappings.</param>
    /// <remarks>
    ///     Implementations should define all routes for the module during this call. The caller is responsible for deciding
    ///     whether the module is enabled and for marking it as registered after mapping.
    /// </remarks>
    void Map(IEndpointRouteBuilder app);
}