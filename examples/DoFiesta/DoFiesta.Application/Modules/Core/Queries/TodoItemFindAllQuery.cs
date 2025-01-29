// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;
using DevKit.Application.Queries;
using Microsoft.Extensions.Logging;

public class TodoItemFindAllQuery(FilterModel filter = null)
    : QueryRequestBase<Result<IEnumerable<TodoItem>>>, ICacheQuery
{
    public FilterModel Filter { get; } = filter;

    CacheQueryOptions ICacheQuery.Options =>
        new() { Key = $"application_{nameof(TodoItemFindAllQuery)}", SlidingExpiration = new TimeSpan(0, 0, 30) };
}

public class TodoItemFindAllQueryHandler(ILoggerFactory loggerFactory, IGenericRepository<TodoItem> repository)
    : QueryHandlerBase<TodoItemFindAllQuery, Result<IEnumerable<TodoItem>>>(loggerFactory)
{
    public override async Task<QueryResponse<Result<IEnumerable<TodoItem>>>> Process(
        TodoItemFindAllQuery query,
        CancellationToken cancellationToken)
    {
        var result = await repository.FindAllResultAsync( // repo takes care of the filter
                query.Filter,
                [new TodoItemIsNotDeletedSpecification()],
                cancellationToken: cancellationToken)
            .Ensure(e => e != null, new EntityNotFoundError())
            .TapAsync(async (e, ct) => await Task.Delay(1, ct), cancellationToken);

        return QueryResponse.For(result);
    }

    public async Task<QueryResponse<ResultPaged<TodoItem>>> ProcessEntities(
        TodoItemFindAllQuery query,
        CancellationToken cancellationToken)
    {
        var result = await (await repository.FindAllResultPagedAsync( // repo takes care of the filter
                query.Filter,
                [new TodoItemIsNotDeletedSpecification()],
                cancellationToken: cancellationToken))
            .Ensure(e => e != null, new EntityNotFoundError())
            .TapAsync(async (e, ct) => await Task.Delay(1, ct), cancellationToken);

        return QueryResponse.For(result);
    }

    // public async Task<ResultPaged<TodoItem>> ProcessEntities2(
    //         TodoItemFindAllQuery query,
    //         CancellationToken cancellationToken)
    // {
    //     var items = await (await repository.FindAllResultPagedAsync( // repo takes care of the filter
    //             query.Filter,
    //             [new TodoItemIsNotDeletedSpecification()],
    //             cancellationToken: cancellationToken))
    //         .Ensure(e => e != null, new EntityNotFoundError())
    //         .TapAsync(async (e, ct) => await Task.Delay(1, ct), cancellationToken));

    //     return items;
    // }

    // public async Task<QueryResponse<Result<IEnumerable<TodoItem>>>> ProcessResult(
    //     TodoItemFindAllQuery query,
    //     CancellationToken cancellationToken)
    // {
    //     var items = await (await repository.FindAllResultAsync( // repo takes care of the filter
    //             query.Filter,
    //             [new TodoItemIsNotDeletedSpecification()],
    //             cancellationToken: cancellationToken))
    //         .Ensure(e => e != null, new EntityNotFoundError())
    //         .TapAsync(async (e, ct) => await Task.Delay(1, ct), cancellationToken)
    //         .Map(e => e.Select(c => TodoItem.Create(c)));

    //     return QueryResponse.For(items);
    // }
}
