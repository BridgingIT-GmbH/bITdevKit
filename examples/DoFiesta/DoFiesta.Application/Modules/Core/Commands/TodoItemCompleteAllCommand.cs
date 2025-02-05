// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.DoFiesta.Domain;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;
using DevKit.Application.Commands;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;

public class TodoItemCompleteAllCommand(DateTime? from = null) : CommandRequestBase<Result>
{
    public DateTime? From { get; } = from;

    public override ValidationResult Validate()
    {
        return new Validator().Validate(this);
    }

    public class Validator : AbstractValidator<TodoItemCompleteAllCommand>
    {
        public Validator()
        {
            //this.RuleFor(c => c.From).NotNull().NotEmpty();
        }
    }
}

public class TodoItemCompleteAllCommandHandler(
    ILoggerFactory loggerFactory,
    IGenericRepository<TodoItem> repository,
    ICurrentUserAccessor currentUserAccessor)
    : CommandHandlerBase<TodoItemCompleteAllCommand, Result>(loggerFactory)
{
    public override async Task<CommandResponse<Result>> Process(
        TodoItemCompleteAllCommand command,
        CancellationToken cancellationToken)
    {
        var filter = FilterModelBuilder.For<TodoItem>()
            .AddFilter(e => e.Status, FilterOperator.Equal, TodoStatus.InProgress)
            //.AddCustomFilter(FilterCustomType.FullTextSearch)
            //    .AddParameter("searchTerm", "***")
            //    .AddParameter("fields", ["Title"]).Done()
            //.AddCustomFilter(FilterCustomType.NamedSpecification)
            //    .AddParameter("specificationName", "TodoItemIsNotDeleted").Done()
            .AddInclude(e => e.Steps)
            .AddOrdering(e => e.DueDate, OrderDirection.Descending)
            //.SetPaging(0, 10)
            .Build();

        var result = await repository.FindAllResultAsync(
            filter,
            [new ForUserSpecification(currentUserAccessor.UserId), new TodoItemIsNotDeletedSpecification()], cancellationToken: cancellationToken)
                // work on the repository result
                .Tap(e => Console.WriteLine("COMPLETEALL: #" + e.Count())) // do something
                .Unless(e => e.Any(s => s.Status != TodoStatus.InProgress), new Error("invalid todoitem status")) // extra check
                .Tap(e => e.ForEach(f => f.SetCompleted())) // logic
                .TraverseAsync(async (e, ct) =>
                    await repository.UpdateAsync(e, ct), cancellationToken: cancellationToken);

        return CommandResult.For(result.Unwrap());
        // TODO: invalidate query cache
    }
}