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

public class TodoItemCreateCommand(TodoItemModel model) : CommandRequestBase<AggregateCreatedCommandResult>,
    ICacheInvalidateCommand
{
    public TodoItemModel Model { get; } = model;

    CacheInvalidateCommandOptions ICacheInvalidateCommand.Options => new() { Key = "application_" };

    public override ValidationResult Validate()
    {
        return new Validator().Validate(this);
    }

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

public class TodoItemCreateCommandHandler(
    ILoggerFactory loggerFactory,
    IGenericRepository<TodoItem> repository,
    IMapper mapper)
    : CommandHandlerBase<TodoItemCreateCommand, AggregateCreatedCommandResult>(loggerFactory)
{
    public override async Task<CommandResponse<AggregateCreatedCommandResult>> Process(
        TodoItemCreateCommand command,
        CancellationToken cancellationToken)
    {
        this.Logger.LogInformation($"+++ create item: {command.Model.Title}");

        var entity = mapper.Map<TodoItemModel, TodoItem>(command.Model);
        await repository.InsertAsync(entity, cancellationToken).AnyContext();

        return new CommandResponse<AggregateCreatedCommandResult>
        {
            Result = new AggregateCreatedCommandResult(command.Model.Id.ToString())
        };
    }
}