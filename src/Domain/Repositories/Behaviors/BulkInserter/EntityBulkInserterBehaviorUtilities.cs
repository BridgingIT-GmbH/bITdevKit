// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

internal static class EntityBulkInserterBehaviorUtilities
{
    internal static IReadOnlyList<TEntity> Materialize<TEntity>(IEnumerable<TEntity> entities)
        where TEntity : class
    {
        if (entities is IReadOnlyList<TEntity> items && items.All(entity => entity is not null))
        {
            return items;
        }

        return (entities ?? []).Where(entity => entity is not null).ToArray();
    }
}
