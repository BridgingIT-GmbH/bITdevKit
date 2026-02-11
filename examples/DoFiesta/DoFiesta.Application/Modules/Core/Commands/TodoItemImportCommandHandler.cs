// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Application.Identity;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Common.DataPorter;
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
        var importResult = await importer.ImportAsync<TodoItemModel>(request.Stream, new ImportOptions
        {
            Format = request.Format,
            ProfileName = "TodoItemImportProfile",
            ValidationBehavior = ImportValidationBehavior.CollectErrors // Continue importing despite errors
        },
        cancellationToken);

        return await importResult
            .Tap(result => Console.WriteLine($"IMPORT: Processed {result.TotalRows} rows, {result.SuccessfulRows} successful, {result.FailedRows} failed"))
            .Ensure(result => result.HasErrors == false || result.SuccessfulRows > 0,
                new ValidationError("Import failed: No valid rows could be imported"))
            .Tap(result =>
            {
                if (result.HasErrors)
                {
                    Console.WriteLine($"IMPORT WARNING: {result.Errors.Count} errors encountered during import");

                    // Group errors by type to understand the pattern
                    var errorsByColumn = result.Errors.GroupBy(e => e.Column).ToDictionary(g => g.Key, g => g.Count());
                    Console.WriteLine($"IMPORT ERROR SUMMARY:");
                    foreach (var (column, count) in errorsByColumn)
                    {
                        Console.WriteLine($"  Column '{column}': {count} errors");
                    }

                    // Show first few errors with raw values
                    Console.WriteLine($"IMPORT ERROR DETAILS (first 10):");
                    foreach (var error in result.Errors.Take(10))
                    {
                        Console.WriteLine($"  Row {error.RowNumber}, Column '{error.Column}': {error.Message}");
                        if (!string.IsNullOrEmpty(error.RawValue))
                        {
                            Console.WriteLine($"    Raw value: '{error.RawValue}'");
                        }
                    }
                }
            })
            .BindAsync(async (result, ct) =>
            {
                // Only persist successfully imported rows
                if (result.SuccessfulRows > 0)
                {
                    var entities = result.Data
                        .Select(mapper.Map<TodoItemModel, TodoItem>)
                        .ToList();

                    Console.WriteLine($"IMPORT: Persisting {entities.Count} entities to repository");

                    foreach (var entity in entities)
                    {
                        // Set the current user ID for each imported entity
                        entity.UserId = currentUserAccessor.UserId;
                        await repository.UpsertAsync(entity, ct);
                    }

                    Console.WriteLine($"IMPORT: Successfully persisted {entities.Count} TodoItems");
                }

                return Result<ImportResult<TodoItemModel>>.Success(result);
            }, cancellationToken);
    }
}
