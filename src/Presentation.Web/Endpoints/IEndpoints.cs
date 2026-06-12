// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.AspNetCore.Routing;

/// <summary>
///     Defines a modular Minimal API endpoint set that can be registered with dependency injection and mapped by the endpoint system.
/// </summary>
/// <remarks>
///     Implementations are discovered or registered as <see cref="IEndpoints" /> services and are mapped by
///     <c>MapEndpoints</c>. The mapper skips endpoint sets that are disabled or have already been registered, then marks
///     successfully mapped instances as registered.
///
///     Example:
///     <code>
///     public sealed class TodoEndpoints : EndpointsBase
///     {
///         public override void Map(IEndpointRouteBuilder app)
///         {
///             app.MapGet("/todos", () =&gt; Results.Ok());
///         }
///     }
///     </code>
/// </remarks>
public interface IEndpoints
{
    /// <summary>
    ///     Gets or sets whether this endpoint set should be mapped.
    /// </summary>
    /// <remarks>
    ///     <c>MapEndpoints</c> reads this flag before calling <see cref="Map" />. Disabled endpoint sets remain registered
    ///     in dependency injection but are not mapped into the route table.
    /// </remarks>
    bool Enabled { get; set; }

    /// <summary>
    ///     Gets or sets whether this endpoint set has already been mapped.
    /// </summary>
    /// <remarks>
    ///     The mapping extension sets this property to <c>true</c> after invoking <see cref="Map" />. This prevents the
    ///     same endpoint instance from being mapped more than once when <c>MapEndpoints</c> is called repeatedly.
    /// </remarks>
    bool IsRegistered { get; set; }

    /// <summary>
    ///     Maps this endpoint set into the specified endpoint route builder.
    /// </summary>
    /// <param name="app">The route builder that receives the endpoint mappings.</param>
    /// <remarks>
    ///     Implementations should declare all Minimal API routes for the endpoint set. Endpoint classes that derive from
    ///     <see cref="EndpointsBase" /> can use <see cref="EndpointsBase.MapGroup" /> to apply shared group configuration.
    /// </remarks>
    void Map(IEndpointRouteBuilder app);
}