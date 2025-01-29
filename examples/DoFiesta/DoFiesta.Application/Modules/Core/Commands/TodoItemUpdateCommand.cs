// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;
using DevKit.Application.Commands;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;

public class TodoItemUpdateCommand(TodoItem entity) : CommandRequestBase<AggregateUpdatedCommandResult>,
    ICacheInvalidateCommand
{
    public TodoItem Entity { get; } = entity;

    CacheInvalidateCommandOptions ICacheInvalidateCommand.Options => new() { Key = "application_" };

    public override ValidationResult Validate()
    {
        return new Validator().Validate(this);
    }

    public class Validator : AbstractValidator<TodoItemUpdateCommand>
    {
        public Validator()
        {
            this.RuleFor(c => c.Entity).NotNull();
            this.RuleFor(c => c.Entity.Id).Must(id => id != Guid.Empty).WithMessage("Invalid guid.");
            this.RuleFor(c => c.Entity.Title).NotNull();
        }
    }
}

public class TodoItemUpdateCommandHandler(
    ILoggerFactory loggerFactory,
    IGenericRepository<TodoItem> repository)
    : CommandHandlerBase<TodoItemUpdateCommand, AggregateUpdatedCommandResult>(loggerFactory)
{
    public override async Task<CommandResponse<AggregateUpdatedCommandResult>> Process(
        TodoItemUpdateCommand command,
        CancellationToken cancellationToken)
    {
        this.Logger.LogInformation($"+++ update item: {command.Entity.Title}");

        if (!await repository.ExistsAsync(command.Entity.Id, cancellationToken).AnyContext())
        {
            throw new EntityNotFoundException();
        }

        await repository.UpsertAsync(command.Entity, cancellationToken).AnyContext();

        // TODO: invalidate query cache

        return new CommandResponse<AggregateUpdatedCommandResult>
        {
            Result = new AggregateUpdatedCommandResult(command.Entity.Id.ToString())
        };
    }
}