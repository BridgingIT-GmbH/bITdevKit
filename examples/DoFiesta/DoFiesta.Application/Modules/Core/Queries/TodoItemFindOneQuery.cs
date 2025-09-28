// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license
namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Identity;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;
using FluentValidation;

public class TodoItemFindOneQuery(string id) : RequestBase<TodoItemModel>
{
    public string Id { get; } = id;

    public class Validator : AbstractValidator<TodoItemFindOneQuery>
    {
        public Validator()
        {
            this.RuleFor(c => c.Id).MustBeValidGuid().WithMessage("Invalid guid.");
        }
    }
}

[HandlerRetry(2, 100)]
[HandlerTimeout(500)]
public class TodoItemFindOneQueryHandler(
    IMapper mapper,
    IGenericRepository<TodoItem> repository,
    ICurrentUserAccessor currentUserAccessor,
    IEntityPermissionEvaluator<TodoItem> permissionEvaluator) : RequestHandlerBase<TodoItemFindOneQuery, TodoItemModel>
{
    protected override async Task<Result<TodoItemModel>> HandleAsync(TodoItemFindOneQuery request, SendOptions options, CancellationToken cancellationToken) =>
        await repository.FindOneResultAsync(TodoItemId.Create(request.Id), cancellationToken: cancellationToken)
            .EnsureAsync(async (e, ct) =>
                await permissionEvaluator.HasPermissionAsync(currentUserAccessor, e.Id, Permission.Read, cancellationToken: ct), new UnauthorizedError(), cancellationToken)
            .Tap(e => Console.WriteLine("AUDIT")) // do something
            .Map(mapper.Map<TodoItem, TodoItemModel>);
}