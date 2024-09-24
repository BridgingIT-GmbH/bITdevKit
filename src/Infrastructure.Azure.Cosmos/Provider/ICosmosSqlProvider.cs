// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using System.Linq.Expressions;

public interface ICosmosSqlProvider<TItem>
{
    Task<TItem> ReadItemAsync(string id, object partitionKey = null, CancellationToken cancellationToken = default);

    Task<TItem> CreateItemAsync(
        TItem item,
        object partitionKeyValue = null,
        CancellationToken cancellationToken = default);

    Task<TItem> UpsertItemAsync(
        TItem item,
        object partitionKeyValue = null,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<TItem>> ReadItemsAsync(
        Expression<Func<TItem, bool>> expression,
        int? skip = null,
        int? take = null,
        Expression<Func<TItem, object>> orderExpression = null,
        bool orderDescending = false,
        object partitionKeyValue = null,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<TItem>> ReadItemsAsync(
        IEnumerable<Expression<Func<TItem, bool>>> expressions = null,
        int? skip = null,
        int? take = null,
        Expression<Func<TItem, object>> orderExpression = null,
        bool orderDescending = false,
        object partitionKeyValue = null,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteItemAsync(
        string id,
        object partitionKeyValue = null,
        CancellationToken cancellationToken = default);
}