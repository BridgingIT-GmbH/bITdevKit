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

public class TodoItemCreateCommand(TodoItem entity) : CommandRequestBase<AggregateCreatedCommandResult>,
    ICacheInvalidateCommand
{
    public TodoItem Entity { get; } = entity;

    CacheInvalidateCommandOptions ICacheInvalidateCommand.Options => new() { Key = "application_" };

    public override ValidationResult Validate()
    {
        return new Validator().Validate(this);
    }

    public class Validator : AbstractValidator<TodoItemCreateCommand>
    {
        public Validator()
        {
            this.RuleFor(c => c.Entity).NotNull();
            this.RuleFor(c => c.Entity.Id).Must(id => id == Guid.Empty).WithMessage("Invalid guid.");
            this.RuleFor(c => c.Entity.Title).NotNull().NotEmpty();
        }
    }
}

public class TodoItemCreateCommandHandler(
    ILoggerFactory loggerFactory,
    IGenericRepository<TodoItem> repository)
    : CommandHandlerBase<TodoItemCreateCommand, AggregateCreatedCommandResult>(loggerFactory)
{
    public override async Task<CommandResponse<AggregateCreatedCommandResult>> Process(
        TodoItemCreateCommand command,
        CancellationToken cancellationToken)
    {
        this.Logger.LogInformation($"+++ create item: {command.Entity.Title}");

        await repository.InsertAsync(command.Entity, cancellationToken).AnyContext();

        return new CommandResponse<AggregateCreatedCommandResult>
        {
            Result = new AggregateCreatedCommandResult(command.Entity.Id.ToString())
        };
    }
}