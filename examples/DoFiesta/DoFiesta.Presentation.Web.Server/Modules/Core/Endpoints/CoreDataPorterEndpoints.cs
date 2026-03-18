// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Presentation.Web.Server.Modules.Core;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Application.DataPorter;
using BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;
using BridgingIT.DevKit.Examples.DoFiesta.Domain.Model;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Mvc;

public class CoreDataPorterEndpoints : EndpointsBase
{
    //ToDo: Create Map OK for DataPorter endpoints
    public override void Map(IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("api/core/todoitems/dataporter")
            .RequireAuthorization()
            .WithTags("CoreModule.TodoItems.DataPorter");

        // Export TodoItems to various formats
        group.MapGet("/export/{format}",
            async ([FromServices] IRequester requester,
                   [FromRoute] string format,
                   [FromQuery] string fileName = null,
                   CancellationToken ct = default) =>
            {
                if (!Format.TryParse(format, out var dataPorterFormat))
                {
                    return Results.Problem(
                        title: "Invalid format",
                        detail: $"Format '{format}' is not supported. Valid formats: excel, csv, json, xml, pdf",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                var result = await requester.SendAsync(
                    new TodoItemExportQuery(dataPorterFormat),
                    cancellationToken: ct);

                if (result.IsFailure)
                {
                    return Results.Problem(
                        title: "Export failed",
                        detail: result.Errors.FirstOrDefault()?.Message,
                        statusCode: StatusCodes.Status400BadRequest);
                }

                var stream = result.Value;
                var (contentType, extension) = dataPorterFormat.Key switch
                {
                    "excel" => ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx"),
                    "csv" => ("text/csv", "csv"),
                    "json" => ("application/json", "json"),
                    "xml" => ("application/xml", "xml"),
                    "pdf" => ("application/pdf", "pdf"),
                    _ => ("application/octet-stream", "dat")
                };

                var downloadFileName = fileName.IsNullOrEmpty()
                    ? $"todoitems-{DateTime.UtcNow:yyyyMMdd-HHmmss}.{extension}"
                    : $"{fileName}.{extension}";

                return Results.File(stream, contentType, downloadFileName);
            })
            .WithName("CoreModule.TodoItems.DataPorter.Export")
            .WithDescription("Exports TodoItems in the specified format (excel, csv, json, xml, pdf).")
            .RequireEntityPermission<TodoItem>(Permission.Read)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // Export TodoItems in multi-sheet Excel
        group.MapGet("/export/multi",
            async ([FromServices] IRequester requester,
                   [FromQuery] string fileName = null,
                   CancellationToken ct = default) =>
            {
                var result = await requester.SendAsync(
                    new TodoItemExportMultiQuery(),
                    cancellationToken: ct);

                if (result.IsFailure)
                {
                    return Results.Problem(
                        title: "Export failed",
                        detail: result.Errors.FirstOrDefault()?.Message,
                        statusCode: StatusCodes.Status400BadRequest);
                }

                var stream = result.Value;
                var downloadFileName = fileName.IsNullOrEmpty()
                    ? $"todoitems-by-status-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx"
                    : $"{fileName}.xlsx";

                return Results.File(
                    stream,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    downloadFileName);
            })
            .WithName("CoreModule.TodoItems.DataPorter.ExportMulti")
            .WithDescription("Exports TodoItems grouped by status in separate Excel sheets.")
            .RequireEntityPermission<TodoItem>(Permission.Read)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError);

        // Import TodoItems from file
        group.MapPost("/import/{format}",
            async ([FromServices] IRequester requester,
                   [FromRoute] string format,
                   IFormFile file,
                   CancellationToken ct = default) =>
            {
                if (file == null || file.Length == 0)
                {
                    return Results.Problem(
                        title: "No file provided",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                if (!Format.TryParse(format, out var dataPorterFormat))
                {
                    return Results.Problem(
                        title: "Invalid format",
                        detail: $"Format '{format}' is not supported. Valid formats: excel, csv, json, xml",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                await using var stream = file.OpenReadStream();
                var result = await requester.SendAsync(
                    new TodoItemImportCommand(stream, dataPorterFormat),
                    cancellationToken: ct);

                if (result.IsFailure)
                {
                    return Results.Problem(
                        title: "Import failed",
                        detail: result.Errors.FirstOrDefault()?.Message,
                        statusCode: StatusCodes.Status400BadRequest);
                }

                var importResult = result.Value;

                if (importResult.HasErrors)
                {
                    return Results.Json(new
                    {
                        success = false,
                        totalRows = importResult.TotalRows,
                        successfulRows = importResult.SuccessfulRows,
                        failedRows = importResult.FailedRows,
                        errorCount = importResult.Errors.Count,
                        errors = importResult.Errors.Select(e => new
                        {
                            row = e.RowNumber,
                            column = e.Column,
                            message = e.Message
                        })
                    }, statusCode: StatusCodes.Status422UnprocessableEntity);
                }

                return Results.Json(new
                {
                    success = true,
                    totalRows = importResult.TotalRows,
                    successfulRows = importResult.SuccessfulRows,
                    failedRows = importResult.FailedRows,
                    data = importResult.Data
                });
            })
            .WithName("CoreModule.TodoItems.DataPorter.Import")
            .WithDescription("Imports TodoItems from the specified format (excel, csv, json, xml).")
            .RequireEntityPermission<TodoItem>(Permission.Write)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status422UnprocessableEntity)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError)
            .DisableAntiforgery();

        // Validate import file without actually importing
        group.MapPost("/validate/{format}",
            async ([FromServices] IRequester requester,
                   [FromRoute] string format,
                   IFormFile file,
                   CancellationToken ct = default) =>
            {
                if (file == null || file.Length == 0)
                {
                    return Results.Problem(
                        title: "No file provided",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                if (!Format.TryParse(format, out var dataPorterFormat))
                {
                    return Results.Problem(
                        title: "Invalid format",
                        detail: $"Format '{format}' is not supported. Valid formats: excel, csv, json, xml",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                await using var stream = file.OpenReadStream();
                var result = await requester.SendAsync(
                    new TodoItemValidateImportQuery(stream, dataPorterFormat),
                    cancellationToken: ct);

                if (result.IsFailure)
                {
                    return Results.Problem(
                        title: "Validation failed",
                        detail: result.Errors.FirstOrDefault()?.Message,
                        statusCode: StatusCodes.Status400BadRequest);
                }

                var validationResult = result.Value;

                return Results.Json(new
                {
                    isValid = validationResult.IsValid,
                    totalRows = validationResult.TotalRows,
                    validRows = validationResult.ValidRows,
                    invalidRows = validationResult.InvalidRows,
                    errorCount = validationResult.Errors.Count,
                    errors = validationResult.Errors.Select(e => new
                    {
                        row = e.RowNumber,
                        column = e.Column,
                        message = e.Message
                    })
                });
            })
            .WithName("CoreModule.TodoItems.DataPorter.Validate")
            .WithDescription("Validates an import file without actually importing the data.")
            .RequireEntityPermission<TodoItem>(Permission.Read)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesResultProblem(StatusCodes.Status500InternalServerError)
            .DisableAntiforgery();
    }
}
