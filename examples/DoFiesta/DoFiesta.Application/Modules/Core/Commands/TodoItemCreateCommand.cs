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
    private readonly IMapper mapper = mapper;
    private readonly IGenericRepository<TodoItem> repository = repository;
    private readonly IEntityPermissionProvider permissionProvider = permissionProvider;
    private readonly ICurrentUserAccessor currentUserAccessor = currentUserAccessor;

    protected override async Task<Result<TodoItemModel>> HandleAsync(
        TodoItemCreateCommand request,
        SendOptions options,
        CancellationToken cancellationToken)
    {
        // Map the model to the entity
        var entity = this.mapper.Map<TodoItemModel, TodoItem>(request.Model);
        entity.UserId = this.currentUserAccessor.UserId;

        // Use rules to validate the entity
        var ruleResult = await Rule
            .Add(RuleSet.IsNotEmpty(entity.Title))
            .Add(RuleSet.NotEqual(entity.Title, "todo"))
            .Add(new TitleShouldBeUniqueRule(entity.Title, this.repository))
            .CheckAsync(cancellationToken);

        Console.WriteLine("RESULT: " + ruleResult.ToString());
        if (ruleResult.IsFailure)
        {
            return Result<TodoItemModel>.Failure()
                .WithErrors(ruleResult.Errors)
                .WithMessages(ruleResult.Messages);
        }

        // Insert the entity into the repository
        var result = await this.repository.InsertResultAsync(entity, cancellationToken)
            .Tap(e => Console.WriteLine("AUDIT")) // do something
            .Map(e => this.mapper.Map<TodoItem, TodoItemModel>(e));

        // Set permissions for the user and the new entity
        if (result.IsSuccess)
        {
            new EntityPermissionProviderBuilder(this.permissionProvider)
                .ForUser(entity.UserId)
                    .WithPermission<TodoItem>(entity.Id, Permission.Read)
                    .WithPermission<TodoItem>(entity.Id, Permission.Write)
                    .WithPermission<TodoItem>(entity.Id, Permission.Delete)
                .Build();
        }

        return result;
    }
}