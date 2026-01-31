// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Common.DataPorter;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.DoFiesta.Domain;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;

public class TodoItemExportQueryHandler(
    IGenericRepository<TodoItem> repository,
    IDataExporter exporter,
    IMapper mapper) : RequestHandlerBase<TodoItemExportQuery, Stream>
{
    protected override async Task<Result<Stream>> HandleAsync(
        TodoItemExportQuery request,
        SendOptions options,
        CancellationToken cancellationToken)
    {
        return await Result<IEnumerable<TodoItem>>.Success(
                await repository.FindAllAsync(
                    new TodoItemIsNotDeletedSpecification(),
                    cancellationToken: cancellationToken))
            .Tap(e => Console.WriteLine($"EXPORT: Exporting {e.Count()} TodoItems to {request.Format}"))
            .Map(e => e.Select(mapper.Map<TodoItem, TodoItemModel>))
            .BindAsync(async (models, ct) =>
            {
                var memoryStream = new MemoryStream();
                var exportResult = await exporter.ExportAsync(models, memoryStream, new ExportOptions
                {
                    Format = request.Format
                }, ct);

                return exportResult.IsSuccess
                    ? Result<Stream>.Success(memoryStream)
                    : Result<Stream>.Failure().WithError(exportResult.Errors.FirstOrDefault());
            }, cancellationToken)
            .Tap(stream => stream.Position = 0);
    }
}
