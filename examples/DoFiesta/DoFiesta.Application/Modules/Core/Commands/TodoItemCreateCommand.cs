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

public class TodoItemCreateCommand : RequestBase<TodoItemModel>
{
    public TodoItemModel Model { get; set; }

    public class Validator : AbstractValidator<TodoItemCreateCommand>
    {
        public Validator()
        {
            this.RuleFor(c => c.Model).NotNull();
            this.RuleFor(c => c.Model.Id).MustBeDefaultOrEmptyGuid();
            this.RuleFor(c => c.Model.Title).NotNull().NotEmpty();
        }
    }
}

[HandlerRetry(2, 100)]
[HandlerTimeout(500)]
public class TodoItemCreateCommandHandler(
    IMapper mapper,
    IGenericRepository<TodoItem> repository,
    IEntityPermissionProvider permissionProvider,
    ICurrentUserAccessor currentUserAccessor) : RequestHandlerBase<TodoItemCreateCommand, TodoItemModel>
{
    protected override async Task<Result<TodoItemModel>> HandleAsync(TodoItemCreateCommand request, SendOptions options, CancellationToken cancellationToken) =>
        await Result.Success()
            .Map(mapper.Map<TodoItemModel, TodoItem>(request.Model))
            .Tap(e => e.UserId = currentUserAccessor.UserId)
            .UnlessAsync(async (e, ct) => await Rule // check rules
                .Add(RuleSet.IsNotEmpty(e.Title))
                .Add(RuleSet.NotEqual(e.Title, "todo"))
                .Add(new TitleShouldBeUniqueRule(e.Title, repository))
                .CheckAsync(cancellationToken), cancellationToken: cancellationToken)
            .Tap(e => e.DomainEvents.Register(new TodoItemCreatedDomainEvent(e)))
            .BindAsync(async (e, ct) =>
                await repository.InsertResultAsync(e, cancellationToken), cancellationToken: cancellationToken)
            .Tap(e =>
                new EntityPermissionProviderBuilder(permissionProvider) // set permissions
                    .ForUser(e.UserId)
                        .WithPermission<TodoItem>(e.Id, Permission.Read)
                        .WithPermission<TodoItem>(e.Id, Permission.Write)
                        .WithPermission<TodoItem>(e.Id, Permission.Delete)
                    .Build())
            .Tap(e => Console.WriteLine("AUDIT")) // do something
            .Map(mapper.Map<TodoItem, TodoItemModel>);
}