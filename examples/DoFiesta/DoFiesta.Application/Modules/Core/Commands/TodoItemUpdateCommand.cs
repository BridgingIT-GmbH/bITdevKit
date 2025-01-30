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

public class TodoItemUpdateCommand(TodoItemModel model) : CommandRequestBase<AggregateUpdatedCommandResult>,
    ICacheInvalidateCommand
{
    public TodoItemModel Model { get; } = model;

    CacheInvalidateCommandOptions ICacheInvalidateCommand.Options => new() { Key = "application_" };

    public override ValidationResult Validate()
    {
        return new Validator().Validate(this);
    }

    public class Validator : AbstractValidator<TodoItemUpdateCommand>
    {
        public Validator()
        {
            this.RuleFor(c => c.Model).NotNull();
            this.RuleFor(c => c.Model.Id).MustBeValidGuid().WithMessage("Invalid guid.");
            this.RuleFor(c => c.Model.Title).NotNull().NotEmpty();
        }
    }
}

public class TodoItemUpdateCommandHandler(
    ILoggerFactory loggerFactory,
    IGenericRepository<TodoItem> repository,
    IMapper mapper)
    : CommandHandlerBase<TodoItemUpdateCommand, AggregateUpdatedCommandResult>(loggerFactory)
{
    public override async Task<CommandResponse<AggregateUpdatedCommandResult>> Process(
        TodoItemUpdateCommand command,
        CancellationToken cancellationToken)
    {
        this.Logger.LogInformation($"+++ update item: {command.Model.Title}");

        if (!await repository.ExistsAsync(command.Model.Id, cancellationToken).AnyContext())
        {
            throw new EntityNotFoundException();
        }

        var entity = mapper.Map<TodoItemModel, TodoItem>(command.Model);
        await repository.UpsertAsync(entity, cancellationToken).AnyContext();

        // TODO: invalidate query cache

        return new CommandResponse<AggregateUpdatedCommandResult>
        {
            Result = new AggregateUpdatedCommandResult(command.Model.Id.ToString())
        };
    }
}