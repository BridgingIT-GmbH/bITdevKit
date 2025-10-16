// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Identity;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

public class EntityPermissionAuthorizationRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EntityPermissionRequirement"/> class.
    /// </summary>
    /// <param name="permission">The permission value that is required for authorization.</param>
    public EntityPermissionAuthorizationRequirement(Type entityType, string permission)
    {
        this.EntityType = entityType;
        this.Permissions = [permission];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityPermissionRequirement"/> class.
    /// </summary>
    /// <param name="permissions">The permission value that is required for authorization.</param>
    public EntityPermissionAuthorizationRequirement(Type entityType, string[] permissions)
    {
        this.EntityType = entityType;
        this.Permissions = permissions;
    }

    /// <summary>
    /// Gets the permissions that any of is required for authorization.
    /// </summary>
    /// <value>
    /// A string representing any of the required permissions.
    /// </value>
    public string[] Permissions { get; init; }

    public string Permission { get; }

    public Type EntityType { get; }
}

public class EntityPermissionAuthorizationRequirementHandler<TEntity>(
        ILoggerFactory loggerFactory)
        //ICurrentUserAccessor userAccessor,
        //IEntityPermissionEvaluator<TEntity> evaluator)
        : AuthorizationHandler<EntityPermissionAuthorizationRequirement>
    where TEntity : class, IEntity
{
    private readonly ILogger<EntityPermissionAuthorizationRequirementHandler<TEntity>> logger =
        loggerFactory.CreateLogger<EntityPermissionAuthorizationRequirementHandler<TEntity>>();

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        EntityPermissionAuthorizationRequirement requirement)
    {
        logger.LogInformation("++++++++++++++++++++++++++++++++++ Authorization handler invoked for permissions: {@Permissions} ++++++++++++++++++++++++++++++++++", requirement?.Permissions);

        context.Succeed(requirement);

        return Task.CompletedTask;
    }
}