// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

public class EntityCannotBeSoftDeletedAgainRule<TEntity>(TEntity entity) : RuleBase
    where TEntity : class, IEntity, ISoftDeletable
{
    private readonly bool? deleted = entity?.Deleted;

    public override string Message => "An already deleted entity cannot be deleted again.";

    protected override Result Execute()
    {
        return Result.SuccessIf(this.deleted is null or false);
    }
}