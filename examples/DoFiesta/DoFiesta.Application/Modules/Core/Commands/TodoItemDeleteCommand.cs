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

public class TodoItemDeleteCommand(string id) : CommandRequestBase<Result>,
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
    : CommandHandlerBase<TodoItemDeleteCommand, Result>(loggerFactory)
{
    public override async Task<CommandResponse<Result>> Process(
        TodoItemDeleteCommand command,
        CancellationToken cancellationToken)
    {
        this.Logger.LogInformation($"+++ delete item: {command.Id}");

        var result = await repository.DeleteResultAsync(TodoItemId.Create(command.Id), cancellationToken)
            .Ensure(e => e == RepositoryActionResult.Deleted, new EntityNotFoundError())
            .Tap(e => Console.WriteLine("AUDIT")); // do something

        return CommandResult.For(result.Unwrap());
        // TODO: invalidate query cache
    }
}