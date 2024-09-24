// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using Model;

public class EntityCannotBeDeletedAgainRule<TEntity>(TEntity entity) : DomainRuleBase
    where TEntity : class, IEntity, IAuditable
{
    private readonly bool? deleted = entity?.AuditState?.IsDeleted();

    public override string Message => "An already deleted entity cannot be deleted again.";

    public override Task<bool> ApplyAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(this.deleted is null || this.deleted == false);
    }
}