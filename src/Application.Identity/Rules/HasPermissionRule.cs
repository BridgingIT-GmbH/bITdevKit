// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Application.Identity;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;

public class HasPermissionRule<TEntity>(
    ICurrentUserAccessor currentUserAccessor,
    IEntityPermissionEvaluator<TEntity> permissionEvaluator,
    string permission) : AsyncRuleBase
    where TEntity : class, IEntity
{
    public override string Message => $"Unauthorized: User must have {permission} permission";

    protected override async Task<Result> ExecuteAsync(CancellationToken cancellationToken)
    {
        return Result.SuccessIf(await permissionEvaluator.HasPermissionAsync(
            currentUserAccessor, typeof(TEntity), Permission.Write, cancellationToken: cancellationToken), new UnauthorizedError(this.Message));
    }
}