// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

public class EntityCannotBeDeletedAgainRule<TEntity>(TEntity entity) : DomainRuleBase
    where TEntity : class, IEntity, IAuditable
{
    private readonly bool? deleted = entity?.AuditState?.IsDeleted();

    public override string Message => "An already deleted entity cannot be deleted again.";

    protected override Result ExecuteRule()
    {
        if (this.deleted is null or false)
        {
            return Result.Success();
        }

        return Result.Failure()
            .WithError(new DomainRuleError(nameof(EntityCannotBeDeletedAgainRule<TEntity>), this.Message));
    }
}