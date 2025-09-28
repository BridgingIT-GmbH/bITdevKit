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

public class TodoItemDeleteCommand(string id) : RequestBase<Unit>
{
    public string Id { get; } = id;

    public class Validator : AbstractValidator<TodoItemDeleteCommand>
    {
        public Validator()
        {
            this.RuleFor(c => c.Id).MustBeValidGuid().WithMessage("Invalid guid.");
        }
    }
}

[HandlerRetry(2, 100)]
[HandlerTimeout(500)]
public class TodoItemDeleteCommandHandler(
    IGenericRepository<TodoItem> repository,
    ICurrentUserAccessor currentUserAccessor,
    IEntityPermissionEvaluator<TodoItem> permissionEvaluator) : RequestHandlerBase<TodoItemDeleteCommand, Unit>
{
    private readonly IGenericRepository<TodoItem> repository = repository;

    protected override async Task<Result<Unit>> HandleAsync(
        TodoItemDeleteCommand request,
        SendOptions options,
        CancellationToken cancellationToken)
    {
        return await Result<Unit>.Success()
            .EnsureAsync(async (e, ct) =>
                await permissionEvaluator.HasPermissionAsync(currentUserAccessor, request.Id, Permission.Delete, cancellationToken: ct), new UnauthorizedError(), cancellationToken)
            .BindAsync(async (e, ct) =>
                await this.repository.DeleteResultAsync(TodoItemId.Create(request.Id), cancellationToken)
            .Ensure(e => e == RepositoryActionResult.Deleted, new EntityNotFoundError())
            .Tap(e => Console.WriteLine("AUDIT")) // do something
            .Map(_ => Unit.Value), cancellationToken: cancellationToken);
    }
}