// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license
namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.DoFiesta.Domain;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;

[Command]
[HandlerRetry(2, 300)]
[HandlerTimeout(5000)]
public partial class TodoItemCompleteAllCommand
{
    public TodoItemCompleteAllCommand()
    {
    }

    public TodoItemCompleteAllCommand(DateTime? from)
    {
        this.From = from;
    }

    public DateTime? From { get; private set; }

    [Handle]
    private async Task<Result<Unit>> HandleAsync(
        IGenericRepository<TodoItem> repository,
        ICurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var filter = FilterModelBuilder.For<TodoItem>()
            .AddFilter(e => e.Status, FilterOperator.Equal, TodoStatus.InProgress)
            //.AddCustomFilter(FilterCustomType.FullTextSearch)
            // .AddParameter("searchTerm", "***")
            // .AddParameter("fields", ["Title"]).Done()
            //.AddCustomFilter(FilterCustomType.NamedSpecification)
            // .AddParameter("specificationName", "TodoItemIsNotDeleted").Done()
            .AddInclude(e => e.Steps)
            .AddOrdering(e => e.DueDate, OrderDirection.Descending)
            //.SetPaging(0, 10)
            .Build();

        return await repository.FindAllResultAsync(
                filter,
                [new ForUserSpecification(currentUserAccessor.UserId), new TodoItemIsNotDeletedSpecification()],
                cancellationToken: cancellationToken)
            .Tap(e => Console.WriteLine("COMPLETEALL: #" + e.Count())) // do something
            .Unless(e => e.Any(s => s.Status != TodoStatus.InProgress), new Error("invalid todoitem status")) // extra check
            .Tap(e => e.ForEach(f => f.SetCompleted())) // logic
                                                        //.TapItemsAsync(repository.UpdateAsync, cancellationToken: cancellationToken)
            .TraverseAsync(async (e, ct) => await repository.UpdateResultAsync(e, ct), cancellationToken: cancellationToken) // BindItemsAsync
            .Map(_ => Unit.Value);
    }
}
