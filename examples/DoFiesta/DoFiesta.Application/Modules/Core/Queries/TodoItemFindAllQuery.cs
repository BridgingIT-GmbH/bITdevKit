// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.DoFiesta.Domain;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;
using DevKit.Application.Queries;
using Microsoft.Extensions.Logging;

//
// QUERY ===============================
//

public class TodoItemFindAllQuery
    : QueryRequestBase<Result<IEnumerable<TodoItemModel>>>/*, ICacheQuery*/
{
    public FilterModel Filter { get; set; }

    //CacheQueryOptions ICacheQuery.Options => new() { Key = $"application_{nameof(TodoItemFindAllQuery)}", SlidingExpiration = new TimeSpan(0, 0, 30) };
}

//
// HANDLER ===============================
//

public class TodoItemFindAllQueryHandler(ILoggerFactory loggerFactory, IMapper mapper, IGenericRepository<TodoItem> repository, ICurrentUserAccessor currentUserAccessor)
    : QueryHandlerBase<TodoItemFindAllQuery, Result<IEnumerable<TodoItemModel>>>(loggerFactory)
{
    public override async Task<QueryResponse<Result<IEnumerable<TodoItemModel>>>> Process(TodoItemFindAllQuery query, CancellationToken cancellationToken)
    {
        var result = (await repository.FindAllResultAsync( // repo takes care of the filter
                query.Filter,
                [new ForUserSpecification(currentUserAccessor.UserId), new TodoItemIsNotDeletedSpecification()], cancellationToken: cancellationToken))
            //.Ensure(e => e.SafeAny(), new EntityNotFoundError())
            .Tap(e => Console.WriteLine("AUDIT")) // do something
            .Map(mapper.Map<TodoItem, TodoItemModel>);

        return QueryResult.For(result);

        //return QueryResult.For(
        //    result.Map(mapper.Map<TodoItem, TodoItemModel>));

        //return result.Match(
        //    onSuccess: _ => QueryResult.For(mapper.Map<TodoItem, TodoItemModel>(result.Value)),
        //    onFailure: _ => QueryResult.For<IEnumerable<TodoItemModel>>(result));
    }

    //
    //
    //
    //
    //
    //
    //
    //
    //
    //
    //
    //
    //
    //
    //
    //
    //
    //
    //
    //
    //
    //
    //
    //

    public async Task<QueryResponse<ResultPaged<TodoItemModel>>> ProcessEntities(TodoItemFindAllQuery query, CancellationToken cancellationToken)
    {
        var result = (await repository.FindAllResultPagedAsync( // repo takes care of the filter
                query.Filter,
                [new TodoItemIsNotDeletedSpecification()], cancellationToken: cancellationToken))
            .Ensure(e => e.SafeAny(), new EntityNotFoundError())
            .Tap(e => Console.WriteLine("AUDIT")) // do something
            .Map(mapper.Map<TodoItem, TodoItemModel>);

        return QueryResult.For(result);

        //return QueryResult.For(
        //    result.Map(mapper.Map<TodoItem, TodoItemModel>));

        //return result.HasError()
        //    ? QueryResult.ForPaged<TodoItemModel>(result)
        //    : QueryResult.For(mapper.Map<TodoItem, TodoItemModel>(result.Value));
    }
}
