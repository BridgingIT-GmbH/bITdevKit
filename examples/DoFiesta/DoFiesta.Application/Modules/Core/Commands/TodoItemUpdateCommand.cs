// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license
namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Identity;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Modules.Core;
using FluentValidation;

[Command]
[HandlerRetry(2, 300)]
[HandlerTimeout(5000)]
public partial class TodoItemUpdateCommand
{
    [ValidateNotNull]
    public TodoItemModel Model { get; set; }

    [Validate]
    private static void Validate(InlineValidator<TodoItemUpdateCommand> validator)
    {
        validator.RuleFor(c => c.Model.Id).MustBeValidGuid().WithMessage("Invalid guid.");
        validator.RuleFor(c => c.Model.Title).NotNull().NotEmpty();
        validator.RuleFor(c => c.Model.ConcurrencyVersion)
            .Must(value => string.IsNullOrWhiteSpace(value) || Guid.TryParse(value, out _))
            .WithMessage("Invalid concurrency version.");
    }

    [Handle]
    private async Task<Result<TodoItemModel>> HandleAsync(
        IMapper mapper,
        IGenericRepository<TodoItem> repository,
        ICurrentUserAccessor currentUserAccessor,
        IEntityPermissionEvaluator<TodoItem> permissionEvaluator,
        CancellationToken cancellationToken)
    {
        var id = TodoItemId.Create(this.Model.Id);
        var existingResult = await repository.FindOneResultAsync(id, cancellationToken: cancellationToken);
        if (existingResult.IsFailure)
        {
            return Result<TodoItemModel>.Failure()
                .WithErrors(existingResult.Errors)
                .WithMessages(existingResult.Messages);
        }

        var existing = existingResult.Value;
        if (!await permissionEvaluator.HasPermissionAsync(currentUserAccessor, existing.Id, Permission.Write, cancellationToken: cancellationToken))
        {
            return Result<TodoItemModel>.Failure(new UnauthorizedError());
        }

        var ruleResult = await Rule
            .Add(RuleSet.IsNotEmpty(this.Model.Title))
            .Add(RuleSet.NotEqual(this.Model.Title, "todo"))
            //.Add(new TitleShouldBeUniqueRule(this.Model.Title, this.repository))
            .CheckAsync(cancellationToken);
        if (ruleResult.IsFailure)
        {
            return Result<TodoItemModel>.Failure()
                .WithErrors(ruleResult.Errors)
                .WithMessages(ruleResult.Messages);
        }

        if (!string.IsNullOrWhiteSpace(this.Model.ConcurrencyVersion))
        {
            if (!Guid.TryParse(this.Model.ConcurrencyVersion, out var expectedVersion))
            {
                return Result<TodoItemModel>.Failure(new ValidationError("Invalid concurrency version."));
            }

            if (existing.ConcurrencyVersion != expectedVersion)
            {
                return Result<TodoItemModel>.Failure(new ConcurrencyError
                {
                    EntityType = nameof(TodoItem),
                    EntityId = existing.Id?.ToString()
                });
            }
        }

        var changed = false;
        var changeResult = existing.Change()
            .Set(e => e.Title, this.Model.Title)
            .Set(e => e.Description, this.Model.Description)
            .Set(e => e.Status, ResolveStatus(this.Model.Status))
            .Set(e => e.Priority, ResolvePriority(this.Model.Priority))
            .Set(e => e.DueDate, this.Model.DueDate)
            .Set(e => e.OrderIndex, this.Model.OrderIndex)
            .Set(e => e.Assignee, ResolveAssignee(this.Model.Assignee))
            .Register((e, _) => new TodoItemUpdatedDomainEvent(e))
            .OnChanged(_ => changed = true)
            .Apply();
        if (changeResult.IsFailure)
        {
            return Result<TodoItemModel>.Failure()
                .WithErrors(changeResult.Errors)
                .WithMessages(changeResult.Messages);
        }

        if (!changed)
        {
            return Result<TodoItemModel>.Success(mapper.Map<TodoItem, TodoItemModel>(existing));
        }

        return await repository.UpdateResultAsync(existing, cancellationToken)
            .Map(mapper.Map<TodoItem, TodoItemModel>);
    }

    private static TodoStatus ResolveStatus(int status)
        => Enumeration.GetAll<TodoStatus>().FirstOrDefault(e => e.Id == status) ?? TodoStatus.New;

    private static TodoPriority ResolvePriority(int priority)
        => Enumeration.GetAll<TodoPriority>().FirstOrDefault(e => e.Id == priority) ?? TodoPriority.Low;

    private static EmailAddress ResolveAssignee(string assignee)
        => string.IsNullOrWhiteSpace(assignee) ? null : EmailAddress.Create(assignee);
}
