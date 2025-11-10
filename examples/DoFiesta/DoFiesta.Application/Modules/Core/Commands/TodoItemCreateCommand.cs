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
[HandlerDatabaseTransaction(contextName: "Core")]
public class TodoItemCreateCommandHandler(
    IMapper mapper,
    IGenericRepository<TodoItem> repository,
    IEntityPermissionProvider permissionProvider,
    ISequenceNumberGenerator numberGenerator,
    ICurrentUserAccessor currentUserAccessor) : RequestHandlerBase<TodoItemCreateCommand, TodoItemModel>
{
    protected override async Task<Result<TodoItemModel>> HandleAsync(TodoItemCreateCommand request, SendOptions options, CancellationToken cancellationToken) =>
        await Result.Success()
            .Map(mapper.Map<TodoItemModel, TodoItem>(request.Model))
            .Tap(e => e.UserId = currentUserAccessor.UserId)
            .TapAsync(async (e, ct) =>
            {
                var seqResult = await numberGenerator.GetNextAsync("TodoItemSequence", "core", cancellationToken: ct);
                e.Number = seqResult.IsSuccess ? (int)seqResult.Value : 0;
            }, cancellationToken: cancellationToken)
            .UnlessAsync(async (e, ct) => await Rule // check rules
                .Add(RuleSet.IsNotEmpty(e.Title))
                .Add(RuleSet.NotEqual(e.Title, "todo"))
                .Add(new TitleShouldBeUniqueRule(e.Title, repository))
                .CheckAsync(cancellationToken), cancellationToken: cancellationToken)
            .Tap(e => e.DomainEvents.Register(new TodoItemCreatedDomainEvent(e)))
            .Tap(e => e.Title += " (ADD)")
            .BindAsync(async (e, ct) =>
                await repository.InsertResultAsync(e, cancellationToken), cancellationToken: cancellationToken)
            .Tap(e => e.Title += " (UPD1)")
            .BindAsync(async (e, ct) =>
                await repository.UpdateResultAsync(e, cancellationToken), cancellationToken: cancellationToken) // test for same contextId (transactions)
            .Tap(e => e.Title += " (UPD2)")
            .BindAsync(async (e, ct) =>
                await repository.UpdateResultAsync(e, cancellationToken), cancellationToken: cancellationToken) // test for same contextId (transactions)
            //.Tap(e => e.Title = null) // test for transaction fail/rollback
            //.BindAsync(async (e, ct) =>
            //    await repository.UpdateResultAsync(e, cancellationToken), cancellationToken: cancellationToken) // test for same contextId (transactions)
            .Tap(e => // will not be called if any above are failing (IsFailure result)
                new EntityPermissionProviderBuilder(permissionProvider) // set permissions
                    .ForUser(e.UserId)
                        .WithPermission<TodoItem>(e.Id, Permission.Read)
                        .WithPermission<TodoItem>(e.Id, Permission.Write)
                        .WithPermission<TodoItem>(e.Id, Permission.Delete)
                    .Build())
            .Tap(e => Console.WriteLine("AUDIT")) // do something
            .Map(mapper.Map<TodoItem, TodoItemModel>);
}