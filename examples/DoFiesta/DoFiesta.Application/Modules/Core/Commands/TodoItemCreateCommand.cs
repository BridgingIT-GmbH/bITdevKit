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
    ISequenceNumberGenerator numberGenerator,
    ICurrentUserAccessor currentUserAccessor,
    IRepositoryTransaction<TodoItem> transaction) : RequestHandlerBase<TodoItemCreateCommand, TodoItemModel>
{
    protected override async Task<Result<TodoItemModel>> HandleAsync(TodoItemCreateCommand request, SendOptions options, CancellationToken cancellationToken) =>
        await Result<TodoItem>.Success(mapper.Map<TodoItemModel, TodoItem>(request.Model))
            // Start transaction scope using repository transaction
            .StartOperation(async ct => await transaction.BeginTransactionAsync(ct))
            // Set current user
            .Tap(e => e.UserId = currentUserAccessor.UserId)
            // Generate sequence number
            .TapAsync(async (e, ct) =>
            {
                var seqResult = await numberGenerator.GetNextAsync("TodoItemSequence", "core", cancellationToken: ct);
                e.Number = seqResult.IsSuccess ? (int)seqResult.Value : 0;
            }, cancellationToken: cancellationToken)
            // Check business rules
            .UnlessAsync(async (e, ct) => await Rule
                .Add(RuleSet.IsNotEmpty(e.Title))
                .Add(RuleSet.NotEqual(e.Title, "todo"))
                .Add(new TitleShouldBeUniqueRule(e.Title, repository))
                .CheckAsync(ct), cancellationToken: cancellationToken)
            // Register domain event
            .Tap(e => e.DomainEvents.Register(new TodoItemCreatedDomainEvent(e)))
            // Insert into database
            .BindAsync(async (e, ct) =>
                await repository.InsertResultAsync(e, ct), cancellationToken: cancellationToken)
            // Set permissions
            .Tap(e =>
                new EntityPermissionProviderBuilder(permissionProvider)
                    .ForUser(e.UserId)
                        .WithPermission<TodoItem>(e.Id, Permission.Read)
                        .WithPermission<TodoItem>(e.Id, Permission.Write)
                        .WithPermission<TodoItem>(e.Id, Permission.Delete)
                    .Build())
            // Audit logging
            .Tap(e => Console.WriteLine("AUDIT"))
            // End transaction (commit on success, rollback on failure)
            .EndOperationAsync(cancellationToken: cancellationToken)
            // Map entity back to model
            .Map(mapper.Map<TodoItem, TodoItemModel>);
}