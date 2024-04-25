// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using BridgingIT.DevKit.Domain.Model;

public class EntityCannotBeDeletedAgainRule<TEntity> : IBusinessRule
    where TEntity : class, IEntity, IAuditable
{
    private readonly bool? deleted;

    public EntityCannotBeDeletedAgainRule(TEntity entity)
    {
        this.deleted = entity?.AuditState?.IsDeleted();
    }

    public string Message => "An already deleted entity cannot be deleted again.";

    public Task<bool> IsSatisfiedAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(this.deleted is null || this.deleted == false);
}