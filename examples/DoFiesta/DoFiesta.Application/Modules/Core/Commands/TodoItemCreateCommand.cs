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

public class TodoItemCreateCommand(TodoItemModel model) : CommandRequestBase<Result<TodoItemModel>>,
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
    IMapper mapper,
    IGenericRepository<TodoItem> repository,
    ICurrentUserAccessor currentUserAccessor) : CommandHandlerBase<TodoItemCreateCommand, Result<TodoItemModel>>(loggerFactory)
{
    public override async Task<CommandResponse<Result<TodoItemModel>>> Process(
        TodoItemCreateCommand command,
        CancellationToken cancellationToken)
    {
        this.Logger.LogInformation($"+++ create item: {command.Model.Title}");

        var entity = mapper.Map<TodoItemModel, TodoItem>(command.Model);
        entity.UserId = currentUserAccessor.UserId;

        var result = await repository.InsertResultAsync(entity, cancellationToken)
            .Tap(e => Console.WriteLine("AUDIT")) // do something
            .Map(mapper.Map<TodoItem, TodoItemModel>);

        return CommandResult.For(result);
    }
}