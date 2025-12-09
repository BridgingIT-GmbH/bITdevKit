// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System.Net;
using BridgingIT.DevKit.Application.Identity;
using Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

public class IdentityEntityPermissionEvaluationEndpoints(IdentityEntityPermissionEvaluationEndpointsOptions options = null) : EndpointsBase
{
    private readonly IdentityEntityPermissionEvaluationEndpointsOptions options = options ?? new IdentityEntityPermissionEvaluationEndpointsOptions();

    public override void Map(IEndpointRouteBuilder app)
    {
        if (!this.options.Enabled)
        {
            return;
        }

        var group = this.MapGroup(app, this.options);

        group.MapGet("/{permission}", this.HasRequiredPermission)
            .WithName("System.HasRequiredPermission")
            .WithDescription("Checks if the current user has the required permission for the entity type.")
            .Produces<EntityPermissionModel>()
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError);

        group.MapGet(string.Empty, this.GetEffectivePermissions)
            .WithName("System.GetEffectivePermissions")
            .WithDescription("Gets all effective permissions for the current user and the entity type.")
            .Produces<IEnumerable<EntityPermissionModel>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError);
    }

    private async Task<Results<Ok<EntityPermissionModel>, BadRequest, ProblemHttpResult>> HasRequiredPermission(
        [FromServices] IServiceProvider serviceProvider,
        [FromServices] EntityPermissionOptions options,
        [FromServices] ICurrentUserAccessor currentUserAccessor,
        [FromRoute] string permission, // permissionrequirement
        [FromQuery] string entityType, // fullname required
        [FromQuery] string entityId, // optional, otherwise type wide
        CancellationToken cancellationToken = default)
    {
        if (currentUserAccessor?.UserId.IsNullOrEmpty() == true)
        {
            return TypedResults.Problem("Current user not valid.", null, (int)HttpStatusCode.BadRequest);
        }

        if (permission.IsNullOrEmpty())
        {
            return TypedResults.Problem("Permission not valid.", null, (int)HttpStatusCode.BadRequest);
        }

        if (entityType.IsNullOrEmpty())
        {
            return TypedResults.Problem("EntityType not valid.", null, (int)HttpStatusCode.BadRequest);
        }

        var entityConfiguration = options.GetEntityTypeConfiguration(entityType, false);
        if (entityConfiguration == null)
        {
            return TypedResults.Problem($"EntityType '{entityType}' not valid.", null, (int)HttpStatusCode.NotFound);
        }

        var evaluatorType = typeof(IEntityPermissionEvaluator<>).MakeGenericType(entityConfiguration.EntityType);
        var evaluator = serviceProvider.GetService(evaluatorType);
        var method = evaluatorType.GetMethod("HasPermissionAsync",
            [
                typeof(string),           // userId
                typeof(string[]),         // roles
                typeof(object),           // entityId
                typeof(string),           // permission
                typeof(bool),             // bypassCache
                typeof(CancellationToken)
            ]);

        var result = await (Task<bool>)method.Invoke(
            evaluator,
            [
                currentUserAccessor.UserId,
                currentUserAccessor.Roles,
                entityId,
                permission,
                this.options.BypassCache,
                cancellationToken
            ]);

        return TypedResults.Ok(
            new EntityPermissionModel(entityType, entityId, permission, default, result));
    }

    private async Task<Results<Ok<IEnumerable<EntityPermissionModel>>, BadRequest, ProblemHttpResult>> GetEffectivePermissions(
        [FromServices] IServiceProvider serviceProvider,
        [FromServices] EntityPermissionOptions options,
        [FromServices] ICurrentUserAccessor currentUserAccessor,
        [FromQuery] string entityType, // fullname required
        [FromQuery] string entityId, // optional, otherwise type wide
        CancellationToken cancellationToken = default)
    {
        if (currentUserAccessor?.UserId.IsNullOrEmpty() == true)
        {
            return TypedResults.Problem("Current user not valid.", null, (int)HttpStatusCode.BadRequest);
        }

        if (entityType.IsNullOrEmpty())
        {
            return TypedResults.Problem("EntityType not valid.", null, (int)HttpStatusCode.BadRequest);
        }

        var entityConfiguration = options.GetEntityTypeConfiguration(entityType, false);
        if (entityConfiguration == null)
        {
            return TypedResults.Problem($"EntityType '{entityType}' not valid.", null, (int)HttpStatusCode.NotFound);
        }

        var evaluatorType = typeof(IEntityPermissionEvaluator<>).MakeGenericType(entityConfiguration.EntityType);
        var evaluator = serviceProvider.GetService(evaluatorType);
        var method = evaluatorType.GetMethod("GetPermissionsAsync",
            [
                typeof(string),           // userId
                typeof(string[]),         // roles
                typeof(object),           // entityId
                typeof(CancellationToken)
            ]);

        var result = await (Task<IReadOnlyCollection<EntityPermissionInfo>>)method.Invoke(
            evaluator,
            [
                currentUserAccessor.UserId,
                currentUserAccessor.Roles,
                entityId,
                cancellationToken
            ]);

        return TypedResults.Ok(result?.Select(r =>
            new EntityPermissionModel(r.EntityType, entityId, r.Permission, r.Source, true)));
    }
}

/// <summary>
/// Respoonse Model for the entity permission information.
/// </summary>
public record EntityPermissionModel(string EntityType, string EntityId, string Permission, string Source, bool HasAccess);