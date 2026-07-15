// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Server.Modules.Core.DataPorter;

using BridgingIT.DevKit.Application.DataPorter;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

/// <summary>
/// Persists a completed TodoItem import batch through the provider-selected bulk insert path.
/// </summary>
public sealed class TodoItemBulkImportPersistenceInterceptor(
    IMapper mapper,
    IEntityBulkInserter<TodoItem> bulkInserter,
    ICurrentUserAccessor currentUserAccessor = null)
    : IImportRowInterceptor<TodoItemModel>
{
    /// <inheritdoc/>
    public Task<RowInterceptionDecision> BeforeImportAsync(
        ImportRowContext<TodoItemModel> context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(RowInterceptionDecision.Continue());
    }

    /// <inheritdoc/>
    public async Task<Result> AfterImportCompletedAsync(
        ImportCompletionContext<TodoItemModel> context,
        CancellationToken cancellationToken = default)
    {
        if (context.Result.HasErrors || context.Result.SuccessfulRows == 0)
        {
            return Result.Success();
        }

        var entities = context.Result.Data
            .Select(model =>
            {
                if (currentUserAccessor != null)
                {
                    model.UserId = currentUserAccessor.UserId;
                }
                model.ConcurrencyVersion = Guid.TryParse(model.ConcurrencyVersion, out _)
                    ? model.ConcurrencyVersion
                    : null;

                var entity = mapper.Map<TodoItemModel, TodoItem>(model);
                if (currentUserAccessor != null)
                {
                    entity.UserId = currentUserAccessor.UserId;
                }
                entity.Steps.Clear();

                return entity;
            })
            .ToList();

        var bulkInsertResult = await bulkInserter.InsertAsync(entities, cancellationToken);

        return bulkInsertResult.IsSuccess
            ? Result.Success()
            : Result.Failure()
                .WithMessages(bulkInsertResult.Messages)
                .WithErrors(bulkInsertResult.Errors);
    }
}
