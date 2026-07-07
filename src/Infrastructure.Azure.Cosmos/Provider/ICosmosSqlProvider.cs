// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using System.Linq.Expressions;
using Common;

/// <summary>
/// Interface defining the contract for a provider working with Azure Cosmos DB SQL API.
/// </summary>
/// <typeparam name="TItem">The type of the items managed by this provider.</typeparam>
public interface ICosmosSqlProvider<TItem>
{
    /// <summary>
    /// Reads an item from the Cosmos DB based on the provided item id and optional partition key.
    /// </summary>
    /// <typeparam name="TItem">The type of the item to read.</typeparam>
    /// <param name="id">The identifier of the item to read.</param>
    /// <param name="partitionKey">The optional partition key to locate the item. Default is null.</param>
    /// <param name="cancellationToken">Optional: The cancellation token to observe while waiting for the task to complete. Default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A task that represents the asynchronous read operation. The task result contains the item if found, or null otherwise.</returns>
    /// <example>
    /// <code>
    /// var item = await cosmosSqlProvider.ReadItemAsync("item-id");
    /// </code>
    /// </example>
    Task<TItem> ReadItemAsync(string id, object partitionKey = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously creates a new item in the Cosmos DB container.
    /// </summary>
    /// <param name="item">The item to be created.</param>
    /// <param name="partitionKeyValue">Optional partition key value for the item.</param>
    /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
    /// <returns>The newly created item.</returns>
    /// <example>
    /// var newItem = await cosmosSqlProvider.CreateItemAsync(item);
    /// </example>
    Task<TItem> CreateItemAsync(
        TItem item,
        object partitionKeyValue = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates an item in the Cosmos DB collection.
    /// </summary>
    /// <param name="item">The item to upsert in the database.</param>
    /// <param name="partitionKeyValue">
    /// (Optional) The partition key value. If null, the default partition key will be used.
    /// </param>
    /// <param name="cancellationToken">
    /// (Optional) A token to cancel the operation.
    /// </param>
    /// <returns>The task object representing the asynchronous operation, with a result of the upserted item.</returns>
    /// <example>
    /// <code>
    /// var itemToUpdate = new MyItem { Id = "1", Name = "Test" };
    /// var result = await myCosmosSqlProvider.UpsertItemAsync(itemToUpdate);
    /// </code>
    /// </example>
    Task<TItem> UpsertItemAsync(
        TItem item,
        object partitionKeyValue = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously reads items from the Cosmos database based on the provided parameters.
    /// </summary>
    /// <param name="expression">A lambda expression used to filter the items.</param>
    /// <param name="skip">The number of items to skip before starting to read. Optional.</param>
    /// <param name="take">The maximum number of items to read. Optional.</param>
    /// <param name="orderExpression">A lambda expression used to specify the property to order by. Optional.</param>
    /// <param name="orderDescending">If set to true, the order is descending; otherwise, the order is ascending. Default is false.</param>
    /// <param name="partitionKeyValue">The partition key value for the query. Optional.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation. Optional.</param>
    /// <returns>An enumerable collection of items that match the query parameters.</returns>
    /// <example>
    /// <code>
    /// var items = await cosmosSqlProvider.ReadItemsAsync(
    /// e => e.Type == "type",
    /// orderExpression: e => e.Timestamp,
    /// orderDescending: true
    /// );
    /// </code>
    /// </example>
    Task<IEnumerable<TItem>> ReadItemsAsync(
        Expression<Func<TItem, bool>> expression,
        int? skip = null,
        int? take = null,
        Expression<Func<TItem, object>> orderExpression = null,
        bool orderDescending = false,
        object partitionKeyValue = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously reads a collection of items from the Cosmos database based on specified expressions and optional parameters.
    /// </summary>
    /// <param name="expressions">Optional expressions to filter the items.</param>
    /// <param name="skip">Optional number of items to skip.</param>
    /// <param name="take">Optional number of items to take.</param>
    /// <param name="orderExpression">Optional expression to order the items.</param>
    /// <param name="orderDescending">Indicates whether to order items in descending order.</param>
    /// <param name="partitionKeyValue">Optional partition key value.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing a collection of items that match the specified criteria.</returns>
    /// <example>
    /// var items = await cosmosSqlProvider.ReadItemsAsync(
    /// expressions: new List<Expression<Func<MyItem, bool>>> { x => x.IsActive },
    /// skip: 0,
    /// take: 10,
    /// orderExpression: x => x.CreatedDate,
    /// orderDescending: true);
    /// </example>
    Task<IEnumerable<TItem>> ReadItemsAsync(
        IEnumerable<Expression<Func<TItem, bool>>> expressions = null,
        int? skip = null,
        int? take = null,
        Expression<Func<TItem, object>> orderExpression = null,
        bool orderDescending = false,
        object partitionKeyValue = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one Cosmos feed page without draining the whole query result.
    /// </summary>
    /// <param name="expression">The expression used to filter items.</param>
    /// <param name="take">The maximum item count hint for the feed page.</param>
    /// <param name="orderExpression">The expression used to order items.</param>
    /// <param name="orderDescending">A value indicating whether to order descending.</param>
    /// <param name="partitionKeyValue">The optional Cosmos partition key value.</param>
    /// <param name="continuationToken">The provider-native Cosmos continuation token.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A Result containing one Cosmos feed page.</returns>
    /// <example>
    /// <code>
    /// var page = await provider.ReadItemsPageResultAsync(x => x.Type == "type", take: 100);
    /// </code>
    /// </example>
    Task<Result<CosmosSqlPage<TItem>>> ReadItemsPageResultAsync(
        Expression<Func<TItem, bool>> expression,
        int? take = null,
        Expression<Func<TItem, object>> orderExpression = null,
        bool orderDescending = false,
        object partitionKeyValue = null,
        string continuationToken = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one Cosmos feed page without draining the whole query result.
    /// </summary>
    /// <param name="expressions">The expressions used to filter items.</param>
    /// <param name="take">The maximum item count hint for the feed page.</param>
    /// <param name="orderExpression">The expression used to order items.</param>
    /// <param name="orderDescending">A value indicating whether to order descending.</param>
    /// <param name="partitionKeyValue">The optional Cosmos partition key value.</param>
    /// <param name="continuationToken">The provider-native Cosmos continuation token.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A Result containing one Cosmos feed page.</returns>
    /// <example>
    /// <code>
    /// var page = await provider.ReadItemsPageResultAsync(expressions, take: 100);
    /// </code>
    /// </example>
    Task<Result<CosmosSqlPage<TItem>>> ReadItemsPageResultAsync(
        IEnumerable<Expression<Func<TItem, bool>>> expressions = null,
        int? take = null,
        Expression<Func<TItem, object>> orderExpression = null,
        bool orderDescending = false,
        object partitionKeyValue = null,
        string continuationToken = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one projected Cosmos feed page without loading unneeded item fields.
    /// </summary>
    /// <typeparam name="TResult">The projection result type.</typeparam>
    /// <param name="expression">The expression used to filter items.</param>
    /// <param name="projection">The projection expression.</param>
    /// <param name="take">The maximum item count hint for the feed page.</param>
    /// <param name="orderExpression">The expression used to order items.</param>
    /// <param name="orderDescending">A value indicating whether to order descending.</param>
    /// <param name="partitionKeyValue">The optional Cosmos partition key value.</param>
    /// <param name="continuationToken">The provider-native Cosmos continuation token.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A Result containing one projected Cosmos feed page.</returns>
    /// <example>
    /// <code>
    /// var page = await provider.ReadItemsPageResultAsync(x => x.Type == "type", x => new { x.Id });
    /// </code>
    /// </example>
    Task<Result<CosmosSqlPage<TResult>>> ReadItemsPageResultAsync<TResult>(
        Expression<Func<TItem, bool>> expression,
        Expression<Func<TItem, TResult>> projection,
        int? take = null,
        Expression<Func<TItem, object>> orderExpression = null,
        bool orderDescending = false,
        object partitionKeyValue = null,
        string continuationToken = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one projected Cosmos feed page without loading unneeded item fields.
    /// </summary>
    /// <typeparam name="TResult">The projection result type.</typeparam>
    /// <param name="expressions">The expressions used to filter items.</param>
    /// <param name="projection">The projection expression.</param>
    /// <param name="take">The maximum item count hint for the feed page.</param>
    /// <param name="orderExpression">The expression used to order items.</param>
    /// <param name="orderDescending">A value indicating whether to order descending.</param>
    /// <param name="partitionKeyValue">The optional Cosmos partition key value.</param>
    /// <param name="continuationToken">The provider-native Cosmos continuation token.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A Result containing one projected Cosmos feed page.</returns>
    /// <example>
    /// <code>
    /// var page = await provider.ReadItemsPageResultAsync(expressions, x => new { x.Id });
    /// </code>
    /// </example>
    Task<Result<CosmosSqlPage<TResult>>> ReadItemsPageResultAsync<TResult>(
        IEnumerable<Expression<Func<TItem, bool>>> expressions,
        Expression<Func<TItem, TResult>> projection,
        int? take = null,
        Expression<Func<TItem, object>> orderExpression = null,
        bool orderDescending = false,
        object partitionKeyValue = null,
        string continuationToken = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an item from the Cosmos DB container asynchronously.
    /// </summary>
    /// <param name="id">The identifier of the item to delete.</param>
    /// <param name="partitionKeyValue">The partition key value for the item. Optional.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation. Optional.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains a boolean indicating
    /// whether the item was successfully deleted.</returns>
    /// <example>
    /// var result = await cosmosSqlProvider.DeleteItemAsync("item-id");
    /// if (result)
    /// {
    /// // Item deleted successfully.
    /// }
    /// </example>
    Task<bool> DeleteItemAsync(
        string id,
        object partitionKeyValue = null,
        CancellationToken cancellationToken = default);
}
