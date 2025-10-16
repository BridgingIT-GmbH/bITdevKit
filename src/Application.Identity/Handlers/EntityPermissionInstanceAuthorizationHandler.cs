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

/// <summary>
/// Authorization handler for entity-level permissions that supports both hierarchical and non-hierarchical entities.
/// </summary>
/// <typeparam name="TEntity">The type of the entity being authorized.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="EntityPermissionInstanceAuthorizationHandler{TEntity, TId}"/> class.
/// </remarks>
/// <param name="loggerFactory">Factory for creating loggers.</param>
/// <param name="userAccessor">Accessor for current user information.</param>
/// <param name="evaluator">Actual evaluator for the entity permission requirement.</param>
public partial class EntityPermissionInstanceAuthorizationHandler<TEntity>( // TODO:move to Presentation.Web because dependend on ASP.NET Core IDENTITY
    ILoggerFactory loggerFactory,
    ICurrentUserAccessor userAccessor,
    IEntityPermissionEvaluator<TEntity> evaluator) // TODO: move to Presentation.Web because dependend on ASP.NET Core IDENTITY
    : AuthorizationHandler<EntityPermissionRequirement, TEntity>, IAuthorizationRequirement
    where TEntity : class, IEntity
{
    private readonly ILogger<EntityPermissionInstanceAuthorizationHandler<TEntity>> logger =
        loggerFactory.CreateLogger<EntityPermissionInstanceAuthorizationHandler<TEntity>>();

    /// <summary>
    /// Handles authorization requirement for the specified entity.
    /// </summary>
    /// <param name="context">The authorization context.</param>
    /// <param name="requirement">The permission requirement.</param>
    /// <param name="entity">The entity being authorized.</param>
    /// <returns>A task representing the authorization operation.</returns>
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, EntityPermissionRequirement requirement, TEntity entity)
    {
        logger.LogInformation("------------------------------------ Authorization handler invoked for permissions: {@Permissions} ------------------------------------", requirement?.Permissions);
        TypedLogger.LogAuthHandler(this.logger, Constants.LogKey, requirement?.Permissions);

        var userId = userAccessor.UserId;
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

        if (entity == null)
        {
            TypedLogger.LogNoEntity(this.logger, Constants.LogKey);
            return;
        }

        // Call the type instance permission check
        if (await evaluator.HasPermissionAsync(userAccessor, entity, requirement.Permissions))
        {
            context.Succeed(requirement);
        }
    }

    public static partial class TypedLogger
    {
        [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "{LogKey} auth handler (instance) - check permission requirement: permissions={Permissions}")]
        public static partial void LogAuthHandler(ILogger logger, string logKey, string[] permissions);

        [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "{LogKey} auth handler - no user identified for permission requirement check")]
        public static partial void LogNoUserIdentified(ILogger logger, string logKey);

        [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "{LogKey} auth handler - no requirement specified for permission requirement check")]
        public static partial void LogNoRequirement(ILogger logger, string logKey);

        [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "{LogKey} auth handler - no entity specified for permission requirement check")]
        public static partial void LogNoEntity(ILogger logger, string logKey);
    }
}
