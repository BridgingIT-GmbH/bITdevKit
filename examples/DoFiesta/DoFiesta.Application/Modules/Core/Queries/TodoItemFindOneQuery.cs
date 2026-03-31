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
[HandlerRetry(2, 100)]
[HandlerTimeout(500)]
public partial class TodoItemFindOneQuery
{
    public TodoItemFindOneQuery()
    {
    }

    public TodoItemFindOneQuery(string id)
    {
        this.Id = id;
    }

    [ValidateNotEmpty("Id is required.")]
    [ValidateValidGuid("Invalid guid.")]
    public string Id { get; private set; }

    [Handle]
    private async Task<Result<TodoItemModel>> HandleAsync(
        IMapper mapper,
        IGenericRepository<TodoItem> repository,
        ICurrentUserAccessor currentUserAccessor,
        IEntityPermissionEvaluator<TodoItem> permissionEvaluator,
        CancellationToken cancellationToken) =>
        await repository.FindOneResultAsync(TodoItemId.Create(this.Id), cancellationToken: cancellationToken)
            .EnsureAsync(async (e, ct) =>
                await permissionEvaluator.HasPermissionAsync(currentUserAccessor, e.Id, Permission.Read, cancellationToken: ct), new UnauthorizedError(), cancellationToken)
            .Tap(e => Console.WriteLine("AUDIT")) // do something
            .Map(mapper.Map<TodoItem, TodoItemModel>);
}
