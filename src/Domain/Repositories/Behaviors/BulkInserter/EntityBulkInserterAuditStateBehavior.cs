// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using BridgingIT.DevKit.Domain.Model;

/// <summary>
/// Initializes and stamps audit state for every entity in a bulk-insert batch.
/// </summary>
/// <example>
/// <code>
/// services.AddEntityFrameworkBulkInserter&lt;Order, AppDbContext&gt;()
///     .WithBehavior&lt;EntityBulkInserterAuditStateBehavior&lt;Order&gt;&gt;();
/// </code>
/// </example>
public class EntityBulkInserterAuditStateBehavior<TEntity>(
    IEntityBulkInserter<TEntity> inner,
    EntityBulkInserterAuditStateBehaviorOptions options = null,
    ICurrentUserAccessor currentUserAccessor = null) : IEntityBulkInserter<TEntity>
    where TEntity : class, IEntity, IAuditable
{
    private readonly EntityBulkInserterAuditStateBehaviorOptions options = options ?? new EntityBulkInserterAuditStateBehaviorOptions();
    private readonly ICurrentUserAccessor currentUserAccessor = currentUserAccessor ?? new NullCurrentUserAccessor();

    /// <inheritdoc />
    public Task<Result<long>> InsertAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        var items = EntityBulkInserterBehaviorUtilities.Materialize(entities);
        var by = this.GetByValue();

        foreach (var entity in items)
        {
            entity.AuditState ??= new AuditState();
            entity.AuditState.SetCreated(by);
        }

        return inner.InsertAsync(items, cancellationToken);
    }

    private string GetByValue() => this.options.ByType switch
    {
        AuditStateByType.ByUserName => this.currentUserAccessor.UserName,
        AuditStateByType.ByEmail => this.currentUserAccessor.Email,
        _ => this.currentUserAccessor.UserId,
    };
}
