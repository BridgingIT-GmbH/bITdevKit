// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Identity;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Application.DataPorter;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;

public class TodoItemImportCommandHandler(
    IDataImporter importer,
    IGenericRepository<TodoItem> repository,
    IMapper mapper,
    ICurrentUserAccessor currentUserAccessor)
    : RequestHandlerBase<TodoItemImportCommand, ImportResult<TodoItemModel>>
{
    protected override async Task<Result<ImportResult<TodoItemModel>>> HandleAsync(
        TodoItemImportCommand request,
        SendOptions options,
        CancellationToken cancellationToken)
    {
        var importResult = await importer.ImportAsync<TodoItemModel>(request.Stream, o => o
            .As(request.Format)
            .WithProfile("TodoItemImportProfile")
            .WithValidationBehavior(ImportValidationBehavior.CollectErrors), cancellationToken);

        return await importResult
            .Ensure(result => result.HasErrors == false || result.SuccessfulRows > 0,
                new ValidationError("Import failed: No valid rows could be imported"))
            .BindAsync(async (result, ct) =>
            {
                // Only persist successfully imported rows
                if (result.SuccessfulRows > 0)
                {
                    var entities = result.Data
                        .Select(mapper.Map<TodoItemModel, TodoItem>)
                        .ToList();

                    foreach (var entity in entities)
                    {
                        // Always scope imported data to the current user.
                        entity.UserId = currentUserAccessor.UserId;
                        await repository.UpsertAsync(entity, ct);
                    }
                }

                return Result<ImportResult<TodoItemModel>>.Success(result);
            }, cancellationToken);
    }
}
