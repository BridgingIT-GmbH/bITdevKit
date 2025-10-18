// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System.Net;
using BridgingIT.DevKit.Application.Identity;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

public class IdentityEntityPermissionManagementEndpoints(IdentityEntityPermissionManagementEndpointsOptions options = null)
    : EndpointsBase
{
    private readonly IdentityEntityPermissionManagementEndpointsOptions options = options ?? new IdentityEntityPermissionManagementEndpointsOptions();

    public override void Map(IEndpointRouteBuilder app)
    {
        if (!this.options.Enabled)
        {
            return;
        }

        var group = this.MapGroup(app, this.options);

        // User Permission Management
        group.MapPost("/users/{userId}/grant", this.GrantUserPermission)
            .WithDescription("Grants a specific permission to a user for a specific entity.")
            .Produces((int)HttpStatusCode.NoContent)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError);

        group.MapPost("/users/{userId}/revoke", this.RevokeUserPermission)
            .WithDescription("Revokes a specific permission from a user for a specific entity.")
            .Produces((int)HttpStatusCode.NoContent)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError);

        group.MapPost("/users/{userId}/revoke/all", this.RevokeAllUserPermissions)
            .WithDescription("Revokes all permissions from a user.")
            .Produces((int)HttpStatusCode.NoContent)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError);

        group.MapGet("/users/{userId}", this.GetUserGrantedPermissions)
            .WithDescription("Retrieves all granted permissions for a user for a specific entity.") // does not take the defaults into account
            .Produces<IReadOnlyCollection<string>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError);

        group.MapGet("/users", this.GetUsersGrantedPermissions)
            .WithDescription("Retrieves all granted permissions for all users for a specific entity.") // does not take the defaults into account
            .Produces<IReadOnlyCollection<EntityPermissionInfo>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError);

        // Role Permission Management
        group.MapPost("/roles/{role}/grant", this.GrantRolePermission)
            .WithDescription("Grants a specific permission to a role for a specific entity.")
            .Produces((int)HttpStatusCode.NoContent)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError);

        group.MapPost("/roles/{role}/revoke", this.RevokeRolePermission)
            .WithDescription("Revokes a specific permission from a role for a specific entity.")
            .Produces((int)HttpStatusCode.NoContent)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError);

        group.MapPost("/roles/{role}/revoke/all", this.RevokeAllRolePermissions)
            .WithDescription("Revokes all permissions from a role.")
            .Produces((int)HttpStatusCode.NoContent)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError);

        group.MapGet("/roles/{role}", this.GetRoleGrantedPermissions)
            .WithDescription("Retrieves all granted permissions for a role for a specific entity.") // does not take the defaults into account
            .Produces<IReadOnlyCollection<string>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError);

        group.MapGet("/roles", this.GetRolesGrantedPermissions)
            .WithDescription("Retrieves all granted permissions for all roles for a specific entity.") // does not take the defaults into account
            .Produces<IReadOnlyCollection<EntityPermissionInfo>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError);
    }

    private record PermissionRequestModel(string EntityType, string EntityId, string Permission);

    private async Task<Results<NoContent, BadRequest<ProblemDetails>, ProblemHttpResult>> GrantUserPermission(
        [FromServices] IEntityPermissionProvider provider,
        [FromServices] EntityPermissionOptions options,
        [FromRoute] string userId,
        [FromBody] PermissionRequestModel request)
    {
        if (userId.IsNullOrEmpty())
        {
            return TypedResults.Problem("UserId not valid.", null, (int)HttpStatusCode.BadRequest);
        }

        if (request?.EntityType.IsNullOrEmpty() == true || request?.Permission.IsNullOrEmpty() == true)
        {
            return TypedResults.Problem("EntityType or Permission not valid.", null, (int)HttpStatusCode.BadRequest);
        }

        var entityConfiguration = options.GetEntityTypeConfiguration(request.EntityType, false);
        if (entityConfiguration == null)
        {
            return TypedResults.Problem($"EntityType '{request.EntityType}' not valid.", null, (int)HttpStatusCode.NotFound);
        }

        await provider.GrantUserPermissionAsync(userId, entityConfiguration.EntityType.FullName, request.EntityId, request.Permission);

        return TypedResults.NoContent();
    }

    private async Task<Results<NoContent, BadRequest<ProblemDetails>, ProblemHttpResult>> RevokeUserPermission(
        [FromServices] IEntityPermissionProvider provider,
        [FromServices] EntityPermissionOptions options,
        [FromRoute] string userId,
        [FromBody] PermissionRequestModel request)
    {
        if (userId.IsNullOrEmpty())
        {
            return TypedResults.Problem("UserId not valid.", null, (int)HttpStatusCode.BadRequest);
        }

        var entityConfiguration = options.GetEntityTypeConfiguration(request.EntityType, false);
        if (entityConfiguration == null)
        {
            return TypedResults.Problem($"EntityType '{request.EntityType}' not valid.", null, (int)HttpStatusCode.NotFound);
        }

        await provider.RevokeUserPermissionAsync(userId, entityConfiguration.EntityType.FullName, request.EntityId, request.Permission);

        return TypedResults.NoContent();
    }

    private async Task<Results<NoContent, BadRequest<ProblemDetails>, ProblemHttpResult>> RevokeAllUserPermissions(
        [FromServices] IEntityPermissionProvider provider,
        [FromRoute] string userId)
    {
        if (userId.IsNullOrEmpty())
        {
            return TypedResults.Problem("UserId not valid.", null, (int)HttpStatusCode.BadRequest);
        }

        await provider.RevokeUserPermissionsAsync(userId);
        return TypedResults.NoContent();
    }

    private async Task<Results<Ok<IReadOnlyCollection<string>>, BadRequest<ProblemDetails>, ProblemHttpResult>> GetUserGrantedPermissions(
        [FromServices] IEntityPermissionProvider provider,
        [FromServices] EntityPermissionOptions options,
        [FromRoute] string userId,
        [FromQuery] string entityType,
        [FromQuery] string entityId)
    {
        var entityConfiguration = options.GetEntityTypeConfiguration(entityType, false);
        if (entityConfiguration == null)
        {
            return TypedResults.Problem($"EntityType '{entityType}' not valid.", null, (int)HttpStatusCode.NotFound);
        }

        var permissions = await provider.GetUserPermissionsAsync(userId, entityConfiguration.EntityType.FullName, entityId);
        return TypedResults.Ok(permissions);
    }

    private async Task<Results<Ok<IReadOnlyCollection<EntityPermissionInfo>>, BadRequest<ProblemDetails>, ProblemHttpResult>> GetUsersGrantedPermissions(
        [FromServices] IEntityPermissionProvider provider,
        [FromServices] EntityPermissionOptions options,
        [FromQuery] string entityType,
        [FromQuery] string entityId)
    {
        var entityConfiguration = options.GetEntityTypeConfiguration(entityType, false);
        if (entityConfiguration == null)
        {
            return TypedResults.Problem($"EntityType '{entityType}' not valid.", null, (int)HttpStatusCode.NotFound);
        }

        var permissions = await provider.GetUsersPermissionsAsync(entityConfiguration.EntityType.FullName, entityId);
        return TypedResults.Ok(permissions);
    }

    // Role endpoints implementations follow the same pattern
    private async Task<Results<NoContent, BadRequest<ProblemDetails>, ProblemHttpResult>> GrantRolePermission(
        [FromServices] IEntityPermissionProvider provider,
        [FromServices] EntityPermissionOptions options,
        [FromRoute] string role,
        [FromBody] PermissionRequestModel request)
    {
        if (role.IsNullOrEmpty())
        {
            return TypedResults.Problem("Role not valid.", null, (int)HttpStatusCode.BadRequest);
        }

        var entityConfiguration = options.GetEntityTypeConfiguration(request.EntityType, false);
        if (entityConfiguration == null)
        {
            return TypedResults.Problem($"EntityType '{request.EntityType}' not valid.", null, (int)HttpStatusCode.NotFound);
        }

        await provider.GrantRolePermissionAsync(role, entityConfiguration.EntityType.FullName, request.EntityId, request.Permission);

        return TypedResults.NoContent();
    }

    private async Task<Results<NoContent, BadRequest<ProblemDetails>, ProblemHttpResult>> RevokeRolePermission(
    [FromServices] IEntityPermissionProvider provider,
    [FromServices] EntityPermissionOptions options,
    [FromRoute] string role,
    [FromBody] PermissionRequestModel request)
    {
        if (role.IsNullOrEmpty())
        {
            return TypedResults.Problem("Role not valid.", null, (int)HttpStatusCode.BadRequest);
        }

        var entityConfiguration = options.GetEntityTypeConfiguration(request.EntityType, false);
        if (entityConfiguration == null)
        {
            return TypedResults.Problem($"EntityType '{request.EntityType}' not valid.", null, (int)HttpStatusCode.NotFound);
        }

        await provider.RevokeRolePermissionAsync(role, entityConfiguration.EntityType.FullName, request.EntityId, request.Permission);

        return TypedResults.NoContent();
    }

    private async Task<Results<NoContent, BadRequest<ProblemDetails>, ProblemHttpResult>> RevokeAllRolePermissions(
        [FromServices] IEntityPermissionProvider provider,
        [FromRoute] string role)
    {
        if (role.IsNullOrEmpty())
        {
            return TypedResults.Problem("Role not valid.", null, (int)HttpStatusCode.BadRequest);
        }

        await provider.RevokeRolePermissionsAsync(role);
        return TypedResults.NoContent();
    }

    private async Task<Results<Ok<IReadOnlyCollection<string>>, BadRequest<ProblemDetails>, ProblemHttpResult>> GetRoleGrantedPermissions(
        [FromServices] IEntityPermissionProvider provider,
        [FromServices] EntityPermissionOptions options,
        [FromRoute] string role,
        [FromQuery] string entityType,
        [FromQuery] string entityId)
    {
        if (role.IsNullOrEmpty() || entityType.IsNullOrEmpty())
        {
            return TypedResults.Problem("Role or EntityType not valid.", null, (int)HttpStatusCode.BadRequest);
        }

        var entityConfiguration = options.GetEntityTypeConfiguration(entityType, false);
        if (entityConfiguration == null)
        {
            return TypedResults.Problem($"EntityType '{entityType}' not valid.", null, (int)HttpStatusCode.NotFound);
        }

        var permissions = await provider.GetRolePermissionsAsync(role, entityConfiguration.EntityType.FullName, entityId);
        return TypedResults.Ok(permissions);
    }

    private async Task<Results<Ok<IReadOnlyCollection<EntityPermissionInfo>>, BadRequest<ProblemDetails>, ProblemHttpResult>> GetRolesGrantedPermissions(
        [FromServices] IEntityPermissionProvider provider,
        [FromServices] EntityPermissionOptions options,
        [FromQuery] string entityType,
        [FromQuery] string entityId)
    {
        var entityConfiguration = options.GetEntityTypeConfiguration(entityType, false);
        if (entityConfiguration == null)
        {
            return TypedResults.Problem($"EntityType '{entityType}' not valid.", null, (int)HttpStatusCode.NotFound);
        }

        var permissions = await provider.GetRolesPermissionsAsync(entityConfiguration.EntityType.FullName, entityId);
        return TypedResults.Ok(permissions);
    }
}

/// <summary>
/// Represents a request to manage entity permissions.
/// </summary>
public record EntityPermissionManagementModel(string EntityType, string EntityId, string Permission);