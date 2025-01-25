// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Identity;

using System.Threading.Tasks;
using BridgingIT.DevKit.Domain.Model;
using Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

public partial class EntityPermissionTypeAuthorizationHandler<TEntity>(
    ILoggerFactory loggerFactory,
    ICurrentUserAccessor currentUserAccessor,
    IEntityPermissionEvaluator<TEntity> permissionEvaluator)
    : AuthorizationHandler<EntityPermissionRequirement, Type>
    where TEntity : class, IEntity
{
    private readonly ILogger<EntityPermissionTypeAuthorizationHandler<TEntity>> logger =
        loggerFactory.CreateLogger<EntityPermissionTypeAuthorizationHandler<TEntity>>();

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        EntityPermissionRequirement requirement,
        Type resourceType)
    {
        TypedLogger.LogAuthHandler(this.logger, Constants.LogKey, requirement?.Permission);

        var userId = currentUserAccessor.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            TypedLogger.LogNoUserIdentified(this.logger, Constants.LogKey);
            return;
        }

        if (requirement == null)
        {
            TypedLogger.LogNoRequirement(this.logger, Constants.LogKey);
            return;
        }

        if (resourceType == null)
        {
            return;
        }

        if (resourceType != typeof(TEntity))
        {
            return;
        }

        // Call the type-wide permission check
        if (await permissionEvaluator.HasPermissionAsync(
            userId,
            currentUserAccessor.Roles,
            requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }

    public static partial class TypedLogger
    {
        [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "{LogKey} auth handler (type) - check permission requirement: permission={Permission}")]
        public static partial void LogAuthHandler(ILogger logger, string logKey, string permission);

        [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "{LogKey} auth handler - no user identified for type permission check")]
        public static partial void LogNoUserIdentified(ILogger logger, string logKey);

        [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "{LogKey} auth handler - no requirement specified for type permission check")]
        public static partial void LogNoRequirement(ILogger logger, string logKey);
    }
}