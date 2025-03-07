// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Identity;
using BridgingIT.DevKit.Application.Identity;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;

public class HasPermissionRule<TEntity> : AsyncRuleBase
    where TEntity : class, IEntity
{
    private readonly ICurrentUserAccessor currentUserAccessor;
    private readonly IEntityPermissionEvaluator<TEntity> permissionEvaluator;
    private readonly object entityId;
    private readonly TEntity entity;
    private readonly string permission;

    public HasPermissionRule(
        ICurrentUserAccessor currentUserAccessor,
        IEntityPermissionEvaluator<TEntity> permissionEvaluator,
        string permission) // entity wide
    {
        EnsureArg.IsNotNullOrEmpty(permission, nameof(permission));

        this.currentUserAccessor = currentUserAccessor;
        this.permissionEvaluator = permissionEvaluator;
        this.permission = permission;
    }

    public HasPermissionRule(
        ICurrentUserAccessor currentUserAccessor,
        IEntityPermissionEvaluator<TEntity> permissionEvaluator,
        TEntity entity,
        string permission)
    {
        EnsureArg.IsNotNull(entity, nameof(entity));
        EnsureArg.IsNotNullOrEmpty(permission, nameof(permission));

        this.currentUserAccessor = currentUserAccessor;
        this.permissionEvaluator = permissionEvaluator;
        this.entity = entity;
        this.permission = permission;
    }

    public HasPermissionRule(
        ICurrentUserAccessor currentUserAccessor,
        IEntityPermissionEvaluator<TEntity> permissionEvaluator,
        object entityId,
        string permission)
    {
        EnsureArg.IsNotNull(entityId, nameof(entityId));
        EnsureArg.IsNotNullOrEmpty(permission, nameof(permission));

        this.currentUserAccessor = currentUserAccessor;
        this.permissionEvaluator = permissionEvaluator;
        this.entityId = entityId;
        this.permission = permission;
    }

    public override string Message
    {
        get
        {
            if (this.entity != null)
            {
                return $"Unauthorized: User must have {this.permission} permission for entity {typeof(TEntity).Name} with id {this.entity.Id}";
            }
            else if (this.entityId != null)
            {
                return $"Unauthorized: User must have {this.permission} permission for entity {typeof(TEntity).Name} with id {this.entityId}";
            }

            return $"Unauthorized: User must have {this.permission} permission for entity {typeof(TEntity).Name}";
        }
    }

    public override async Task<Result> ExecuteAsync(CancellationToken cancellationToken)
    {
        if (this.entity != null)
        {
            return Result.SuccessIf(await this.permissionEvaluator.HasPermissionAsync(
                this.currentUserAccessor, this.entity, this.permission, cancellationToken: cancellationToken), new UnauthorizedError(this.Message));
        }
        else if (this.entityId != null)
        {
            return Result.SuccessIf(await this.permissionEvaluator.HasPermissionAsync(
                this.currentUserAccessor, this.entityId, this.permission, cancellationToken: cancellationToken), new UnauthorizedError(this.Message));
        }

        return Result.SuccessIf(await this.permissionEvaluator.HasPermissionAsync(
            this.currentUserAccessor, typeof(TEntity), this.permission, cancellationToken: cancellationToken), new UnauthorizedError(this.Message));
    }
}