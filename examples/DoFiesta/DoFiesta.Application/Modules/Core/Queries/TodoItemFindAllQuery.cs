// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Identity;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;

[Query]
[HandlerRetry(2, 300)]
[HandlerTimeout(5000)]
public partial class TodoItemFindAllQuery
{
    public FilterModel Filter { get; set; }

    [Handle]
    private async Task<Result<IEnumerable<TodoItemModel>>> HandleAsync(
        IMapper mapper,
        IGenericRepository<TodoItem> repository,
        ICurrentUserAccessor currentUserAccessor,
        IEntityPermissionEvaluator<TodoItem> permissionEvaluator,
        CancellationToken cancellationToken) =>
        await repository.FindAllResultAsync( // repo takes care of the filter
                this.Filter,
                /*[new ForUserSpecification(currentUserAccessor.UserId), new TodoItemIsNotDeletedSpecification()],*/ cancellationToken: cancellationToken)
            .FilterItemsAsync(async (e, ct) =>
                await permissionEvaluator.HasPermissionAsync(currentUserAccessor, e.Id, Permission.Read, cancellationToken: ct), null, cancellationToken)
            .Tap(e => Console.WriteLine("USER " + currentUserAccessor.Email)) // do something
            .Tap(e => Console.WriteLine("AUDIT #" + e.Count())) // do something
            .Map(mapper.Map<TodoItem, TodoItemModel>);
}
