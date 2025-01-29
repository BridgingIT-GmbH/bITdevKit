// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;
using DevKit.Application.Commands;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;

public class TodoItemDeleteCommand(string entityId) : CommandRequestBase<AggregateDeletedCommandResult>,
    ICacheInvalidateCommand
{
    public string EntityId { get; } = entityId;

    CacheInvalidateCommandOptions ICacheInvalidateCommand.Options => new() { Key = "application_" };

    public override ValidationResult Validate()
    {
        return new Validator().Validate(this);
    }

    public class Validator : AbstractValidator<TodoItemDeleteCommand>
    {
        public Validator()
        {
            this.RuleFor(c => c.EntityId).NotNull().NotEmpty().Length(3, 128);
            // this.RuleFor(c => c.Id).Must(id => id != Guid.Empty).WithMessage("Invalid guid.");
        }
    }
}

public class TodoItemDeleteCommandHandler(
    ILoggerFactory loggerFactory,
    IGenericRepository<TodoItem> repository)
    : CommandHandlerBase<TodoItemDeleteCommand, AggregateDeletedCommandResult>(loggerFactory)
{
    public override async Task<CommandResponse<AggregateDeletedCommandResult>> Process(
        TodoItemDeleteCommand command,
        CancellationToken cancellationToken)
    {
        this.Logger.LogInformation($"+++ delete item: {command.EntityId}");

        await repository.DeleteAsync(TodoItemId.Create(command.EntityId), cancellationToken).AnyContext();

        return new CommandResponse<AggregateDeletedCommandResult>
        {
            Result = new AggregateDeletedCommandResult(command.EntityId.ToString())
        };
    }
}