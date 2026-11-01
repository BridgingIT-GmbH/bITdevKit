// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Observes bulk insertion intent and returned counts without enumerating inputs or fabricating property deltas.</summary>
/// <example><code>bulk.WithBehavior&lt;EntityBulkInserterAuditingBehavior&lt;Customer&gt;&gt;();</code></example>
public sealed class EntityBulkInserterAuditingBehavior<TEntity>(IEntityBulkInserter<TEntity> inner, IAuditContext audit = null, IServiceScopeFactory scopes = null)
    : IEntityBulkInserter<TEntity> where TEntity : class, IEntity
{
    /// <inheritdoc />
    public Task<Result<long>> InsertAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        if (audit is null && scopes is not null) return this.InScopeAsync(entities, cancellationToken);
        if (audit?.IsEnabled != true) return inner.InsertAsync(entities, cancellationToken);
        return AuditBehaviorExecutor.RunAsync(() => audit.BeginScope(builder => builder.For<TEntity>("BulkInsert").Kind("BulkInsert")),
            () => inner.InsertAsync(entities, cancellationToken), new() { Feature = "BulkInserter" },
            (scope, result) =>
            {
                if (result.IsSuccess) scope.SetDetail("bulk.observedCount", AuditValue.From(result.Value));
            }, starting: scope =>
            {
                scope.SetDetail("bulk.operationId", AuditValue.From(Guid.NewGuid().ToString("D")));
                if (entities?.TryGetNonEnumeratedCount(out var count) == true) scope.SetDetail("bulk.requestedCount", AuditValue.From((long)count));
            });
    }

    private async Task<Result<long>> InScopeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var observer = new EntityBulkInserterAuditingBehavior<TEntity>(inner, scope.ServiceProvider.GetService<IAuditContext>());
        return await observer.InsertAsync(entities, cancellationToken).ConfigureAwait(false);
    }
}
