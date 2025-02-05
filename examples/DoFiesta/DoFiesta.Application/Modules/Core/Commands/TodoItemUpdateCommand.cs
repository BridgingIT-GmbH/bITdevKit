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

//
// COMMAND ===============================
//

public class TodoItemUpdateCommand(TodoItemModel model) : CommandRequestBase<Result<TodoItemModel>>,
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

//
// HANDLER ===============================
//

public class TodoItemUpdateCommandHandler(
    ILoggerFactory loggerFactory,
    IMapper mapper,
    IGenericRepository<TodoItem> repository) : CommandHandlerBase<TodoItemUpdateCommand, Result<TodoItemModel>>(loggerFactory)
{
    public override async Task<CommandResponse<Result<TodoItemModel>>> Process(
        TodoItemUpdateCommand command,
        CancellationToken cancellationToken)
    {
        this.Logger.LogInformation($"+++ update item: {command.Model.Title}");

        var entity = mapper.Map<TodoItemModel, TodoItem>(command.Model);
        var result = await repository.UpdateResultAsync(entity, cancellationToken)
            .Tap(e => Console.WriteLine("AUDIT")) // do something
            .Map(mapper.Map<TodoItem, TodoItemModel>);

        return CommandResult.For(result);
        // TODO: invalidate query cache
    }
}