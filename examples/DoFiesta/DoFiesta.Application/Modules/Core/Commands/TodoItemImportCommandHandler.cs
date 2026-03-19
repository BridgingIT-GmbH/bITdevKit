// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.DataPorter;
using BridgingIT.DevKit.Common;

public class TodoItemImportCommandHandler(
    IDataImporter importer)
    : RequestHandlerBase<TodoItemImportCommand, ImportResult<TodoItemModel>>
{
    protected override async Task<Result<ImportResult<TodoItemModel>>> HandleAsync(
        TodoItemImportCommand request,
        SendOptions options,
        CancellationToken cancellationToken)
    {
        var importResult = await importer.ImportAsync<TodoItemModel>(request.Stream, o => o
            .As(request.Format)
            .WithValidationBehavior(ImportValidationBehavior.CollectErrors), cancellationToken);

        return importResult
            .Ensure(result => result.HasErrors == false || result.SuccessfulRows > 0,
                new ValidationError("Import failed: No valid rows could be imported"));
    }
}
