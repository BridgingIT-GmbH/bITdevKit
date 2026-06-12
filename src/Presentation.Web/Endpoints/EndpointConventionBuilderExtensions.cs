// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.AspNetCore.Builder;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;

/// <summary>
///     Provides endpoint convention builder extensions for bITdevKit endpoint options.
/// </summary>
/// <remarks>
///     These extensions complement the ASP.NET Core Minimal API metadata extensions. They keep route-name prefix handling
///     close to endpoint naming so callers can use endpoint options directly when assigning route names.
/// </remarks>
public static class EndpointConventionBuilderExtensions
{
    /// <summary>
    ///     Assigns an endpoint name after applying the route name prefix configured on endpoint options.
    /// </summary>
    /// <typeparam name="TBuilder">The concrete endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint convention builder being configured.</param>
    /// <param name="options">The endpoint options that may contain a route name prefix.</param>
    /// <param name="name">The endpoint-specific route name.</param>
    /// <returns>The original <paramref name="builder" /> instance for fluent chaining.</returns>
    /// <remarks>
    ///     The method delegates route-name composition to <see cref="EndpointsBase.BuildRouteName" /> and then calls the
    ///     standard ASP.NET Core <c>WithName</c> extension with the computed name.
    ///
    ///     Example:
    ///     <code>
    ///     group.MapGet("orders", GetOrders)
    ///         .WithName(options, "List");
    ///     </code>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="builder" /> or <paramref name="options" /> is <c>null</c>.
    /// </exception>
    public static TBuilder WithName<TBuilder>(this TBuilder builder, EndpointsOptionsBase options, string name)
        where TBuilder : class, IEndpointConventionBuilder
    {
        EnsureArg.IsNotNull(builder, nameof(builder));
        EnsureArg.IsNotNull(options, nameof(options));

        return builder.WithName(EndpointsBase.BuildRouteName(options, name));
    }
}