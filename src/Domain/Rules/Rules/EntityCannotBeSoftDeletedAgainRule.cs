// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using BridgingIT.DevKit.Domain.Model;

public class EntityCannotBeSoftDeletedAgainRule<TEntity>(TEntity entity)
    : DomainRuleBase
    where TEntity : class, IEntity, ISoftDeletable
{
    private readonly bool? deleted = entity?.Deleted;

    public override string Message => "An already deleted entity cannot be deleted again.";

    public override Task<bool> ApplyAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(this.deleted is null || this.deleted == false);
}