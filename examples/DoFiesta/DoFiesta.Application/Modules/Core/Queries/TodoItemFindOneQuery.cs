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

public class TodoItemFindOneQuery : QueryRequestBase<Result<TodoItem>>, ICacheQuery
{
    public TodoItemFindOneQuery(string entityId)
    {
        this.EntityId = entityId;
    }

    public string EntityId { get; }

    CacheQueryOptions ICacheQuery.Options =>
        new()
        {
            Key = $"application_{nameof(TodoItemFindOneQuery)}_{this.EntityId}".TrimEnd('_'),
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
            this.RuleFor(c => c.EntityId).MustBeValidGuid().WithMessage("Invalid guid.");
        }
    }
}

public class TodoItemFindOneQueryHandler(
    ILoggerFactory loggerFactory,
    IGenericRepository<TodoItem> repository)
    : QueryHandlerBase<TodoItemFindOneQuery, Result<TodoItem>>(loggerFactory)
{
    public override async Task<QueryResponse<Result<TodoItem>>> Process(
        TodoItemFindOneQuery query,
        CancellationToken cancellationToken)
    {
        var result = await repository.FindOneResultAsync(
            TodoItemId.Create(query.EntityId), cancellationToken: cancellationToken);

        return QueryResponse.For(result);
    }
}