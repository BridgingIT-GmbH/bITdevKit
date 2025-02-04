// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;
using DevKit.Application.Queries;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;

public class TodoItemFindOneQuery(string id) : QueryRequestBase<Result<TodoItemModel>>, ICacheQuery
{
    public string Id { get; } = id;

    CacheQueryOptions ICacheQuery.Options =>
        new()
        {
            Key = $"application_{nameof(TodoItemFindOneQuery)}_{this.Id}".TrimEnd('_'),
            SlidingExpiration = new TimeSpan(0, 0, 30)
        };

    public override ValidationResult Validate()
    {
        return new Validator().Validate(this);
    }

    public class Validator : AbstractValidator<TodoItemFindOneQuery>
    {
        public Validator()
        {
            this.RuleFor(c => c.Id).MustBeValidGuid().WithMessage("Invalid guid.");
        }
    }
}

public class TodoItemFindOneQueryHandler(
    ILoggerFactory loggerFactory,
    IMapper mapper,
    IGenericRepository<TodoItem> repository,
    ICurrentUserAccessor currentUserAccessor)
    : QueryHandlerBase<TodoItemFindOneQuery, Result<TodoItemModel>>(loggerFactory)
{
    public override async Task<QueryResponse<Result<TodoItemModel>>> Process(
        TodoItemFindOneQuery query,
        CancellationToken cancellationToken)
    {
        var result = await repository.FindOneResultAsync(TodoItemId.Create(query.Id), cancellationToken: cancellationToken)
            .Ensure(e => e != null, new EntityNotFoundError())
            .Ensure(e => e.UserId == currentUserAccessor.UserId, new UnauthorizedError())
            .Tap(e => Console.WriteLine("AUDIT")) // do something
            .Map(mapper.Map<TodoItem, TodoItemModel>);

        return QueryResult.For(result);

        //return result.HasError()
        //    ? QueryResult.For<TodoItemModel>(result)
        //    : QueryResult.For(mapper.Map<TodoItem, TodoItemModel>(result.Value));
    }
}