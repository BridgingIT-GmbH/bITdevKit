// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license
namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Identity;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Modules.Core;
using FluentValidation;

[Command]
[HandlerRetry(2, 100)]
[HandlerTimeout(500)]
public partial class TodoItemUpdateCommand
{
    [ValidateNotNull]
    public TodoItemModel Model { get; set; }

    [Validate]
    private static void Validate(InlineValidator<TodoItemUpdateCommand> validator)
    {
        validator.RuleFor(c => c.Model.Id).MustBeValidGuid().WithMessage("Invalid guid.");
        validator.RuleFor(c => c.Model.Title).NotNull().NotEmpty();
    }

    [Handle]
    private async Task<Result<TodoItemModel>> HandleAsync(
        IMapper mapper,
        IGenericRepository<TodoItem> repository,
        ICurrentUserAccessor currentUserAccessor,
        IEntityPermissionEvaluator<TodoItem> permissionEvaluator,
        CancellationToken cancellationToken) =>
        await Result.Success()
            .Map(mapper.Map<TodoItemModel, TodoItem>(this.Model))
            .EnsureAsync(async (e, ct) => // check permissions
                await permissionEvaluator.HasPermissionAsync(currentUserAccessor, e.Id, Permission.Write, cancellationToken: ct), new UnauthorizedError(), cancellationToken)
            .UnlessAsync(async (e, ct) => await Rule // check rules
                .Add(RuleSet.IsNotEmpty(e.Title))
                .Add(RuleSet.NotEqual(e.Title, "todo"))
                //.Add(new TitleShouldBeUniqueRule(e.Title, this.repository))
                .CheckAsync(cancellationToken), cancellationToken: cancellationToken)
            .Tap(e => e.DomainEvents.Register(new TodoItemUpdatedDomainEvent(e)))
            .BindAsync(async (e, ct) =>
                await repository.UpdateResultAsync(e, ct), cancellationToken)
            .Tap(e => Console.WriteLine("AUDIT")) // do something
            .Map(mapper.Map<TodoItem, TodoItemModel>);
}
