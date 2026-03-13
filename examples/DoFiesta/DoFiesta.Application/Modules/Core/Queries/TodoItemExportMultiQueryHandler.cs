// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Application.DataPorter;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.DoFiesta.Domain;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;

public class TodoItemExportMultiQueryHandler(
    IGenericRepository<TodoItem> repository,
    IDataExporter exporter,
    IMapper mapper) : RequestHandlerBase<TodoItemExportMultiQuery, Stream>
{
    protected override async Task<Result<Stream>> HandleAsync(
        TodoItemExportMultiQuery request,
        SendOptions options,
        CancellationToken cancellationToken)
    {
        return await Result<IEnumerable<TodoItem>>.Success(
                await repository.FindAllAsync(
                    new TodoItemIsNotDeletedSpecification(),
                    cancellationToken: cancellationToken))
            .Tap(e => Console.WriteLine($"EXPORT MULTI: Exporting {e.Count()} TodoItems grouped by status"))
            .Map(e => e.Select(entity => mapper.Map<TodoItem, TodoItemModel>(entity)).ToList())
            .Map(models => new[]
            {
                ExportDataSet.Create(models.Where(t => t.Status == 1).ToList(), "New"),
                ExportDataSet.Create(models.Where(t => t.Status == 2).ToList(), "In Progress"),
                ExportDataSet.Create(models.Where(t => t.Status == 3).ToList(), "Completed")
            })
            .Tap(dataSets => Console.WriteLine($"EXPORT MULTI: Created {dataSets.Length} sheets"))
            .BindAsync(async (dataSets, ct) =>
            {
                var memoryStream = new MemoryStream();
                var exportResult = await exporter.ExportMultipleAsync(dataSets, memoryStream, new ExportOptions
                {
                    Format = Format.Excel
                }, ct);

                return exportResult.IsSuccess
                    ? Result<Stream>.Success(memoryStream)
                    : Result<Stream>.Failure().WithError(exportResult.Errors.FirstOrDefault());
            }, cancellationToken)
            .Tap(stream => stream.Position = 0);
    }
}
