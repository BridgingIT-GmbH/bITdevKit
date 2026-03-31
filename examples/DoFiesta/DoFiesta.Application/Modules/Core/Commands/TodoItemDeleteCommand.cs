// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license
namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Identity;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;

[Command]
[HandlerRetry(2, 100)]
[HandlerTimeout(500)]
public partial class TodoItemDeleteCommand
{
    public TodoItemDeleteCommand()
    {
    }

    public TodoItemDeleteCommand(string id)
    {
        this.Id = id;
    }

    [ValidateNotEmpty("Id is required.")]
    [ValidateValidGuid("Invalid guid.")]
    public string Id { get; private set; }

    [Handle]
    private async Task<Result<Unit>> HandleAsync(
        IGenericRepository<TodoItem> repository,
        ICurrentUserAccessor currentUserAccessor,
        IEntityPermissionEvaluator<TodoItem> permissionEvaluator,
        CancellationToken cancellationToken) =>
        await Result<Unit>.Success()
            .EnsureAsync(async (e, ct) => // check permissions
                await permissionEvaluator.HasPermissionAsync(currentUserAccessor, this.Id, Permission.Delete, cancellationToken: ct), new UnauthorizedError(), cancellationToken)
            .BindAsync(async (e, ct) =>
                await repository.DeleteResultAsync(TodoItemId.Create(this.Id), cancellationToken)
            .Ensure(e => e == RepositoryActionResult.Deleted, new EntityNotFoundError())
            .Tap(e => Console.WriteLine("AUDIT")) // do something
            .Map(_ => Unit.Value), cancellationToken: cancellationToken);
}
