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

public class TodoItemUpdateCommand : RequestBase<TodoItemModel>
{
    public TodoItemModel Model { get; set; }

    public class Validator : AbstractValidator<TodoItemUpdateCommand>
    {
        public Validator()
        {
            this.RuleFor(c => c.Model).NotNull();
            this.RuleFor(c => c.Model.Id).MustBeValidGuid().WithMessage("Invalid guid.");
            this.RuleFor(c => c.Model.Title).NotNull().NotEmpty();
        }
    }
}

[HandlerRetry(2, 100)]
[HandlerTimeout(500)]
public class TodoItemUpdateCommandHandler(
    IMapper mapper,
    IGenericRepository<TodoItem> repository,
    ICurrentUserAccessor currentUserAccessor,
    IEntityPermissionEvaluator<TodoItem> permissionEvaluator) : RequestHandlerBase<TodoItemUpdateCommand, TodoItemModel>
{
    protected override async Task<Result<TodoItemModel>> HandleAsync(TodoItemUpdateCommand request, SendOptions options, CancellationToken cancellationToken) =>
        await Result.Success()
            .Map(mapper.Map<TodoItemModel, TodoItem>(request.Model))
            .EnsureAsync(async (e, ct) => // check permissions
                await permissionEvaluator.HasPermissionAsync(currentUserAccessor, e.Id, Permission.Write, cancellationToken: ct), new UnauthorizedError(), cancellationToken)
            .UnlessAsync(async (e, ct) => await Rule // check rules
                .Add(RuleSet.IsNotEmpty(e.Title))
                .Add(RuleSet.NotEqual(e.Title, "todo"))
                //.Add(new TitleShouldBeUniqueRule(e.Title, this.repository))
                .CheckAsync(cancellationToken), cancellationToken: cancellationToken)
            .BindAsync(async (e, ct) =>
                await repository.UpdateResultAsync(e, ct), cancellationToken)
            .Tap(e => Console.WriteLine("AUDIT")) // do something
            .Map(mapper.Map<TodoItem, TodoItemModel>);
}