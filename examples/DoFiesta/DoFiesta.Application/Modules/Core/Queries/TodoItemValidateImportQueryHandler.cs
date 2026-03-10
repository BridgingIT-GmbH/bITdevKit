// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Application.DataPorter;

public class TodoItemValidateImportQueryHandler(IDataImporter importer)
    : RequestHandlerBase<TodoItemValidateImportQuery, ValidationResult>
{
    protected override async Task<Result<ValidationResult>> HandleAsync(
        TodoItemValidateImportQuery request,
        SendOptions options,
        CancellationToken cancellationToken)
    {
        var validationResult = await importer.ValidateAsync<TodoItemModel>(request.Stream, new ImportOptions
        {
            Format = request.Format
        },
        cancellationToken);

        return validationResult
            .Tap(result => Console.WriteLine($"VALIDATE: Checked {result.TotalRows} rows, {result.ValidRows} valid, {result.InvalidRows} invalid"))
            .Tap(result =>
            {
                if (!result.IsValid)
                {
                    Console.WriteLine($"VALIDATE: Found {result.Errors.Count} validation errors");
                    foreach (var error in result.Errors.Take(5))
                    {
                        Console.WriteLine($"  Row {error.RowNumber}, Column {error.Column}: {error.Message}");
                    }
                }
            });
    }
}
