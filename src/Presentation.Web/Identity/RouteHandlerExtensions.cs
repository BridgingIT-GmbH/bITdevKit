// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Identity;
using BridgingIT.DevKit.Domain.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

public static class AuthorizationExtensions
{
    /// <summary>
    /// Requires entity-level permission for the specified entity type.
    /// </summary>
    /// <typeparam name="TEntity">The type of entity to evaluate permissions for.</typeparam>
    /// <param name="builder">The route handler builder.</param>
    /// <param name="permission">The permission to require (e.g., "Read", "Write", "List").</param>
    /// <example>
    /// Requiring List permission for the Customer entity in a minimal API endpoint:
    /// <code>
    /// app.MapGet("/api/customers", async (IMediator mediator) =>
    /// {
    ///     var result = await mediator.Send(new GetCitiesQuery());
    ///     return Results.Ok(result);
    /// })
    /// .RequireEntityPermission&lt;Customer&gt;(Permission.List);
    /// </code>
    /// </example>
    public static RouteHandlerBuilder RequireEntityPermission<TEntity>(
        this RouteHandlerBuilder builder,
        string permission)
        where TEntity : class, IEntity
    {
        return builder.RequireAuthorization(new AuthorizeAttribute
        {
            Policy = $"{nameof(EntityPermissionRequirement)}_{typeof(TEntity).FullName}_{permission}"
        });
    }

    /// <summary>
    /// Requires entity-level permission for the specified entity type on a group of endpoints.
    /// </summary>
    /// <typeparam name="TEntity">The type of entity to check permissions for.</typeparam>
    /// <param name="group">The route group builder.</param>
    /// <param name="permission">The permission to require (e.g., "Read", "Write", "List").</param>
    /// <example>
    /// Requiring Read permission for all endpoints in a City group:
    /// <code>
    /// app.MapGroup("/api/cities")
    ///    .RequireEntityPermission&lt;City&gt;(Permission.Read)
    ///    .MapGet("/", async (IMediator mediator) => { ... })
    ///    .MapGet("/{id}", async (IMediator mediator, string id) => { ... });
    /// </code>
    /// </example>
    public static RouteGroupBuilder RequireEntityPermission<TEntity>(
        this RouteGroupBuilder group,
        string permission)
        where TEntity : class, IEntity
    {
        return group.RequireAuthorization(new AuthorizeAttribute
        {
            Policy = $"{nameof(EntityPermissionRequirement)}_{typeof(TEntity).FullName}_{permission}"
        });
    }
}