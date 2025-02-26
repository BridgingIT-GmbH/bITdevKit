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
///     Represents a base class for api endpoints in the application.
/// </summary>
public abstract class EndpointsBase : IEndpoints
{
    /// <summary>
    ///     Gets or sets a value indicating whether the endpoint is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether the endpoint is registered.
    /// </summary>
    public bool IsRegistered { get; set; }

    /// <summary>
    ///     Maps the endpoint to the specified <see cref="IEndpointRouteBuilder" />.
    /// </summary>
    /// <param name="app">The <see cref="IEndpointRouteBuilder" /> to map the endpoint to.</param>
    public abstract void Map(IEndpointRouteBuilder app);

    /// <summary>
    ///    Maps a group of endpoints to the specified <see cref="IEndpointRouteBuilder" />.
    /// </summary>
    /// <param name="app"></param>
    /// <param name="options"></param>
    public RouteGroupBuilder MapGroup(IEndpointRouteBuilder app, EndpointsOptionsBase options)
    {
        var group = app.MapGroup(options.GroupPath)
            .WithTags(options.GroupTag);

        if (options.ExcludeFromDescription)
        {
            group.ExcludeFromDescription();
        }

        if (options.RequireAuthorization)
        {
            if (options.RequiredRoles.SafeAny())
            {
                group.RequireAuthorization(
                    new AuthorizeAttribute
                    {
                        Roles = string.Join(",", options.RequiredRoles.Where(g => !g.IsNullOrEmpty()))
                    });
            }
            else if (!options.RequiredPolicy.IsNullOrEmpty())
            {
                group.RequireAuthorization(
                    new AuthorizeAttribute(options.RequiredPolicy));
            }
            else
            {
                group.RequireAuthorization();
            }
        }

        return group;
    }
}