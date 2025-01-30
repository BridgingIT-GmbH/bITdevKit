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

public class TodoItemDeleteCommand(string id) : CommandRequestBase<AggregateDeletedCommandResult>,
    ICacheInvalidateCommand
{
    public string Id { get; } = id;

    CacheInvalidateCommandOptions ICacheInvalidateCommand.Options => new() { Key = "application_" };

    public override ValidationResult Validate()
    {
        return new Validator().Validate(this);
    }

    public class Validator : AbstractValidator<TodoItemDeleteCommand>
    {
        public Validator()
        {
            this.RuleFor(c => c.Id).MustBeValidGuid().WithMessage("Invalid guid.");
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
        this.Logger.LogInformation($"+++ delete item: {command.Id}");

        await repository.DeleteAsync(TodoItemId.Create(command.Id), cancellationToken).AnyContext();

        return new CommandResponse<AggregateDeletedCommandResult>
        {
            Result = new AggregateDeletedCommandResult(command.Id.ToString())
        };
    }
}