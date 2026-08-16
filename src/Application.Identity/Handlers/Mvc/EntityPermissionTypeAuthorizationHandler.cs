// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Identity;

using BridgingIT.DevKit.Domain.Model;
using Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

/// <summary>
/// Represents entity permission type authorization handler.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <param name="loggerFactory">The factory used to create loggers.</param>
/// <param name="userAccessor">The user accessor used by the operation.</param>
/// <param name="evaluator">The evaluator used by the operation.</param>
public partial class EntityPermissionTypeAuthorizationHandler<TEntity>(
    ILoggerFactory loggerFactory,
    ICurrentUserAccessor userAccessor,
    IEntityPermissionEvaluator<TEntity> evaluator)
    : AuthorizationHandler<EntityPermissionRequirement, Type>, IAuthorizationRequirement
    where TEntity : class, IEntity
{
    // HANDLER NOT USED IN MINIMAL API SCENARIO (RequireEntityPermission)
    private readonly ILogger<EntityPermissionTypeAuthorizationHandler<TEntity>> logger =
        loggerFactory.CreateLogger<EntityPermissionTypeAuthorizationHandler<TEntity>>();

    /// <inheritdoc/>
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, EntityPermissionRequirement requirement, Type entityType)
    {
        //logger.LogInformation("------------------------------------ Authorization handler invoked for permissions: {@Permissions} ------------------------------------", requirement?.Permissions);
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

        if (entityType == null)
        {
            return;
        }

        if (entityType != typeof(TEntity))
        {
            return;
        }

        // Call the type-wide permission check
        if (await evaluator.HasPermissionAsync(userAccessor, requirement.Permissions))
        {
            context.Succeed(requirement);
        }
    }

    /// <summary>
    /// Represents typed logger.
    /// </summary>
    public static partial class TypedLogger
    {
        /// <summary>
        /// Writes a log entry for the auth handler operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="permissions">The permissions used by the operation.</param>
        [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "[{LogKey}] auth handler (type) - check permission requirement: permissions={Permissions}")]
        public static partial void LogAuthHandler(ILogger logger, string logKey, string[] permissions);

        /// <summary>
        /// Writes a log entry for the no user identified operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "[{LogKey}] auth handler - no user identified for type permission check")]
        public static partial void LogNoUserIdentified(ILogger logger, string logKey);

        /// <summary>
        /// Writes a log entry for the no requirement operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "[{LogKey}] auth handler - no requirement specified for type permission check")]
        public static partial void LogNoRequirement(ILogger logger, string logKey);
    }
}
