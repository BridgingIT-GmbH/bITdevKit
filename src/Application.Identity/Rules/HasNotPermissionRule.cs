// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Identity;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;

/// <summary>
/// Represents has not permission rule.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public class HasNotPermissionRule<TEntity> : AsyncRuleBase
    where TEntity : class, IEntity
{
    private readonly ICurrentUserAccessor userAccessor;
    private readonly IEntityPermissionEvaluator<TEntity> permissionEvaluator;
    private readonly object entityId;
    private readonly TEntity entity;
    private readonly string permission;
    private readonly string[] permissions;
    private readonly bool bypassCache;

    // Single permission constructors
    /// <summary>
    /// Initializes a new instance of the <c>HasNotPermissionRule</c> class.
    /// </summary>
    /// <param name="currentUserAccessor">The current user accessor used by the operation.</param>
    /// <param name="permissionEvaluator">The permission evaluator used by the operation.</param>
    /// <param name="permission">The permission used by the operation.</param>
    /// <param name="bypassCache">The bypass cache used by the operation.</param>
    public HasNotPermissionRule(
        ICurrentUserAccessor currentUserAccessor,
        IEntityPermissionEvaluator<TEntity> permissionEvaluator,
        string permission,
        bool bypassCache = false) // entity wide
    {
        EnsureArg.IsNotNullOrEmpty(permission, nameof(permission));

        this.userAccessor = currentUserAccessor;
        this.permissionEvaluator = permissionEvaluator;
        this.permission = permission;
        this.bypassCache = bypassCache;
        this.bypassCache = bypassCache;
    }

    /// <summary>
    /// Initializes a new instance of the <c>HasNotPermissionRule</c> class.
    /// </summary>
    /// <param name="currentUserAccessor">The current user accessor used by the operation.</param>
    /// <param name="permissionEvaluator">The permission evaluator used by the operation.</param>
    /// <param name="entity">The entity involved in the operation.</param>
    /// <param name="permission">The permission used by the operation.</param>
    /// <param name="bypassCache">The bypass cache used by the operation.</param>
    public HasNotPermissionRule(
        ICurrentUserAccessor currentUserAccessor,
        IEntityPermissionEvaluator<TEntity> permissionEvaluator,
        TEntity entity,
        string permission,
        bool bypassCache = false)
    {
        EnsureArg.IsNotNull(entity, nameof(entity));
        EnsureArg.IsNotNullOrEmpty(permission, nameof(permission));

        this.userAccessor = currentUserAccessor;
        this.permissionEvaluator = permissionEvaluator;
        this.entity = entity;
        this.permission = permission;
        this.bypassCache = bypassCache;
    }

    /// <summary>
    /// Initializes a new instance of the <c>HasNotPermissionRule</c> class.
    /// </summary>
    /// <param name="currentUserAccessor">The current user accessor used by the operation.</param>
    /// <param name="permissionEvaluator">The permission evaluator used by the operation.</param>
    /// <param name="entityId">The entity identifier.</param>
    /// <param name="permission">The permission used by the operation.</param>
    /// <param name="bypassCache">The bypass cache used by the operation.</param>
    public HasNotPermissionRule(
        ICurrentUserAccessor currentUserAccessor,
        IEntityPermissionEvaluator<TEntity> permissionEvaluator,
        object entityId,
        string permission,
        bool bypassCache = false)
    {
        EnsureArg.IsNotNull(entityId, nameof(entityId));
        EnsureArg.IsNotNullOrEmpty(permission, nameof(permission));

        this.userAccessor = currentUserAccessor;
        this.permissionEvaluator = permissionEvaluator;
        this.entityId = entityId;
        this.permission = permission;
        this.bypassCache = bypassCache;
    }

    // Multiple permissions constructors
    /// <summary>
    /// Initializes a new instance of the <c>HasNotPermissionRule</c> class.
    /// </summary>
    /// <param name="currentUserAccessor">The current user accessor used by the operation.</param>
    /// <param name="permissionEvaluator">The permission evaluator used by the operation.</param>
    /// <param name="permissions">The permissions used by the operation.</param>
    /// <param name="bypassCache">The bypass cache used by the operation.</param>
    public HasNotPermissionRule(
        ICurrentUserAccessor currentUserAccessor,
        IEntityPermissionEvaluator<TEntity> permissionEvaluator,
        string[] permissions,
        bool bypassCache = false) // entity wide
    {
        EnsureArg.IsNotNull(permissions, nameof(permissions));
        EnsureArg.IsTrue(permissions.Length > 0, nameof(permissions));

        this.userAccessor = currentUserAccessor;
        this.permissionEvaluator = permissionEvaluator;
        this.permissions = permissions;
        this.bypassCache = bypassCache;
    }

    /// <summary>
    /// Initializes a new instance of the <c>HasNotPermissionRule</c> class.
    /// </summary>
    /// <param name="currentUserAccessor">The current user accessor used by the operation.</param>
    /// <param name="permissionEvaluator">The permission evaluator used by the operation.</param>
    /// <param name="entity">The entity involved in the operation.</param>
    /// <param name="permissions">The permissions used by the operation.</param>
    /// <param name="bypassCache">The bypass cache used by the operation.</param>
    public HasNotPermissionRule(
        ICurrentUserAccessor currentUserAccessor,
        IEntityPermissionEvaluator<TEntity> permissionEvaluator,
        TEntity entity,
        string[] permissions,
        bool bypassCache = false)
    {
        EnsureArg.IsNotNull(entity, nameof(entity));
        EnsureArg.IsNotNull(permissions, nameof(permissions));
        EnsureArg.IsTrue(permissions.Length > 0, nameof(permissions));

        this.userAccessor = currentUserAccessor;
        this.permissionEvaluator = permissionEvaluator;
        this.entity = entity;
        this.permissions = permissions;
        this.bypassCache = bypassCache;
    }

    /// <summary>
    /// Initializes a new instance of the <c>HasNotPermissionRule</c> class.
    /// </summary>
    /// <param name="currentUserAccessor">The current user accessor used by the operation.</param>
    /// <param name="permissionEvaluator">The permission evaluator used by the operation.</param>
    /// <param name="entityId">The entity identifier.</param>
    /// <param name="permissions">The permissions used by the operation.</param>
    /// <param name="bypassCache">The bypass cache used by the operation.</param>
    public HasNotPermissionRule(
        ICurrentUserAccessor currentUserAccessor,
        IEntityPermissionEvaluator<TEntity> permissionEvaluator,
        object entityId,
        string[] permissions,
        bool bypassCache = false)
    {
        EnsureArg.IsNotNull(entityId, nameof(entityId));
        EnsureArg.IsNotNull(permissions, nameof(permissions));
        EnsureArg.IsTrue(permissions.Length > 0, nameof(permissions));

        this.userAccessor = currentUserAccessor;
        this.permissionEvaluator = permissionEvaluator;
        this.entityId = entityId;
        this.permissions = permissions;
        this.bypassCache = bypassCache;
    }

    /// <inheritdoc/>
    public override string Message
    {
        get
        {
            var permissionText = this.permissions != null
                ? $"any of [{string.Join(", ", this.permissions)}] permissions"
                : $"{this.permission} permission";

            if (this.entity != null)
            {
                return $"Unauthorized: User must not have {permissionText} for entity {typeof(TEntity).Name} with id {this.entity.Id}";
            }
            else if (this.entityId != null)
            {
                return $"Unauthorized: User must not have {permissionText} for entity {typeof(TEntity).Name} with id {this.entityId}";
            }

            return $"Unauthorized: User must not have {permissionText} for entity {typeof(TEntity).Name}";
        }
    }

    /// <inheritdoc/>
    public override async Task<Result> ExecuteAsync(CancellationToken cancellationToken)
    {
        if (this.permissions != null) // Multiple permissions case
        {
            bool hasAnyPermission;
            if (this.entity != null)
            {
                hasAnyPermission = await this.permissionEvaluator.HasPermissionAsync(
                    this.userAccessor, this.entity, this.permissions, this.bypassCache, cancellationToken);
            }
            else if (this.entityId != null)
            {
                hasAnyPermission = await this.permissionEvaluator.HasPermissionAsync(
                    this.userAccessor, this.entityId, this.permissions, this.bypassCache, cancellationToken);
            }
            else
            {
                hasAnyPermission = await this.permissionEvaluator.HasPermissionAsync(
                    this.userAccessor, this.permissions, this.bypassCache, cancellationToken);
            }

            return Result.SuccessIf(!hasAnyPermission, new UnauthorizedError(this.Message));
        }
        else // Single permission case
        {
            if (this.entity != null)
            {
                return Result.SuccessIf(!await this.permissionEvaluator.HasPermissionAsync(
                    this.userAccessor, this.entity, this.permission, this.bypassCache, cancellationToken),
                    new UnauthorizedError(this.Message));
            }
            else if (this.entityId != null)
            {
                return Result.SuccessIf(!await this.permissionEvaluator.HasPermissionAsync(
                    this.userAccessor, this.entityId, this.permission, this.bypassCache, cancellationToken),
                    new UnauthorizedError(this.Message));
            }

            return Result.SuccessIf(!await this.permissionEvaluator.HasPermissionAsync(
                this.userAccessor, this.permission, this.bypassCache, cancellationToken),
                new UnauthorizedError(this.Message));
        }
    }
}
