// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core.DataPorter;

using BridgingIT.DevKit.Application.DataPorter;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;

/// <summary>
/// Persists imported todo item rows during DataPorter import processing.
/// </summary>
public sealed class TodoItemImportPersistenceInterceptor(
    IGenericRepository<TodoItem> repository,
    IMapper mapper,
    ICurrentUserAccessor currentUserAccessor) : IImportRowInterceptor<TodoItemModel>
{
    /// <inheritdoc/>
    public async Task<RowInterceptionDecision> BeforeImportAsync(
        ImportRowContext<TodoItemModel> context,
        CancellationToken cancellationToken = default)
    {
        var entity = mapper.Map<TodoItemModel, TodoItem>(context.Item);
        entity.UserId = currentUserAccessor.UserId;

        await repository.UpsertAsync(entity, cancellationToken);

        return RowInterceptionDecision.Continue();
    }
}
