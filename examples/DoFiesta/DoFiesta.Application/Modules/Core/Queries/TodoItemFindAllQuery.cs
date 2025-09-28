// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Identity;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.DoFiesta.Domain;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;

public class TodoItemFindAllQuery
    : RequestBase<IEnumerable<TodoItemModel>>
{
    public FilterModel Filter { get; set; }
}

[HandlerRetry(2, 100)]
[HandlerTimeout(500)]
public class TodoItemFindAllQueryHandler(
    IMapper mapper,
    IGenericRepository<TodoItem> repository,
    ICurrentUserAccessor currentUserAccessor,
    IEntityPermissionEvaluator<TodoItem> permissionEvaluator) : RequestHandlerBase<TodoItemFindAllQuery, IEnumerable<TodoItemModel>>()
{
    protected override async Task<Result<IEnumerable<TodoItemModel>>> HandleAsync(
        TodoItemFindAllQuery request,
        SendOptions options,
        CancellationToken cancellationToken)
    {
        return await repository.FindAllResultAsync( // repo takes care of the filter
                request.Filter,
                [new ForUserSpecification(currentUserAccessor.UserId), new TodoItemIsNotDeletedSpecification()], cancellationToken: cancellationToken)
            .FilterItemsAsync(async (e, ct) =>
                await permissionEvaluator.HasPermissionAsync(currentUserAccessor, e.Id, Permission.Read, cancellationToken: ct), null, cancellationToken)
            .Tap(e => Console.WriteLine("AUDIT")) // do something
            .Map(mapper.Map<TodoItem, TodoItemModel>);
    }
}