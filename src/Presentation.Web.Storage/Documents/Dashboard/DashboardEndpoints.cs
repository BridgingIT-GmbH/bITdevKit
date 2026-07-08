// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage.Documents.Dashboard;

using System.Net;
using System.Text;
using System.Text.Json;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using HttpResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>
/// Maps the Document Storage dashboard pages and dashboard-local document actions.
/// </summary>
/// <example>
/// <code>
/// services.AddDashboard(options => options.WithPluginAssemblyContaining&lt;DashboardEndpoints&gt;());
/// </code>
/// </example>
public sealed class DashboardEndpoints(DashboardEndpointsOptions options) : EndpointsBase, IDashboardEndpoints
{
    internal const string DocumentsPath = "/storage/documents";
    internal const string DocumentsContentPath = "/storage/documents/content";
    internal const string DocumentsDownloadPath = "/storage/documents/download";

    private const string ActionsPath = "/storage/documents/actions";

    /// <inheritdoc />
    public override void Map(IEndpointRouteBuilder app)
    {
        options ??= new DashboardEndpointsOptions();

        if (!options.Enabled || !IsDocumentStorageEnabled(app.ServiceProvider))
        {
            return;
        }

        var group = this.MapGroup(app, options)
            .WithTags("_bdk.Dashboard");

        group.MapDashboardPage<Pages.Index>(
            DocumentsPath,
            "_bdk.Dashboard.Storage.Documents",
            "Dashboard Documents",
            "Shows registered document storage clients with key listing, viewing, editing, and delete actions.");

        group.MapDashboardPage<Pages.Content>(
            DocumentsContentPath,
            "_bdk.Dashboard.Storage.DocumentsContent",
            "Dashboard Documents Content",
            "Shows the refreshable document storage dashboard content fragment.");

        group.MapGet(DocumentsDownloadPath, async (HttpContext context, CancellationToken cancellationToken) =>
            await DownloadDocumentAsync(context, cancellationToken))
            .WithName("_bdk.Dashboard.Storage.Documents.Download")
            .WithSummary("Download document storage document")
            .ExcludeFromDescription();

        group.MapPost($"{ActionsPath}/save", async (HttpContext context, CancellationToken cancellationToken) =>
            await ExecuteFormActionAsync(context, async (accessor, form) =>
            {
                var key = CreateDocumentKey(form);
                var keyValidation = ValidateDocumentKey(key);
                if (keyValidation.IsFailure)
                {
                    return keyValidation;
                }

                var content = GetFormText(form, "content");
                var validation = ValidateJsonContent(content);
                if (validation.IsFailure)
                {
                    return validation;
                }

                if (string.Equals(GetFormValue(form, "mode"), "new", StringComparison.OrdinalIgnoreCase))
                {
                    var existingResult = await accessor.GetJsonResultAsync(key, cancellationToken);
                    if (existingResult.IsSuccess)
                    {
                        return Result.Failure(new ConflictError($"Document '{key.PartitionKey}/{key.RowKey}' already exists."));
                    }

                    if (!existingResult.Errors.Any(e => e is DocumentStoreNotFoundError or NotFoundError))
                    {
                        return Result.Failure(existingResult.Messages, existingResult.Errors);
                    }
                }

                return await accessor.UpsertJsonResultAsync(key, content, cancellationToken);
            }))
            .WithName("_bdk.Dashboard.Storage.Documents.Save")
            .WithSummary("Save document storage document")
            .DisableAntiforgery()
            .ExcludeFromDescription();

        group.MapPost($"{ActionsPath}/delete", async (HttpContext context, CancellationToken cancellationToken) =>
            await ExecuteFormActionAsync(context, async (accessor, form) =>
            {
                var key = CreateDocumentKey(form);
                var keyValidation = ValidateDocumentKey(key);
                return keyValidation.IsFailure
                    ? keyValidation
                    : await accessor.DeleteResultAsync(key, cancellationToken);
            }))
            .WithName("_bdk.Dashboard.Storage.Documents.Delete")
            .WithSummary("Delete document storage document")
            .DisableAntiforgery()
            .ExcludeFromDescription();

        group.MapPost($"{ActionsPath}/delete-batch", async (HttpContext context, CancellationToken cancellationToken) =>
            await ExecuteFormActionAsync(context, async (accessor, form) =>
                await DeleteDocumentsAsync(accessor, CreateDocumentKeys(form), cancellationToken)))
            .WithName("_bdk.Dashboard.Storage.Documents.DeleteBatch")
            .WithSummary("Delete document storage documents")
            .DisableAntiforgery()
            .ExcludeFromDescription();
    }

    internal static string BuildDocumentsPath(DashboardEndpointsOptions opts) =>
        DashboardPath.Combine(opts?.GroupPath, DocumentsPath);

    internal static string BuildDocumentsContentPath(DashboardEndpointsOptions opts) =>
        DashboardPath.Combine(opts?.GroupPath, DocumentsContentPath);

    internal static string BuildDocumentsDownloadPath(DashboardEndpointsOptions opts) =>
        DashboardPath.Combine(opts?.GroupPath, DocumentsDownloadPath);

    internal static string BuildDocumentsActionBase(DashboardEndpointsOptions opts) =>
        DashboardPath.Combine(opts?.GroupPath, ActionsPath);

    private static bool IsDocumentStorageEnabled(IServiceProvider services) =>
        services.GetService<DocumentStorageFeature>()?.IsEnabled == true &&
        services.GetServices<DocumentStoreClientDescriptor>().Any();

    private static async Task<HttpResult> ExecuteFormActionAsync(
        HttpContext context,
        Func<IDocumentStoreClientAccessor, IFormCollection, Task<Result>> action)
    {
        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        var clientId = GetFormValue(form, "clientId");
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return Results.Problem("Document client is required.", statusCode: (int)HttpStatusCode.BadRequest);
        }

        var factory = context.RequestServices.GetService<IDocumentStoreClientFactory>();
        if (factory is null)
        {
            return Results.Problem("AddDocumentStorage() is not registered.", statusCode: (int)HttpStatusCode.ServiceUnavailable);
        }

        var accessor = factory.Create(clientId);
        if (accessor is null)
        {
            return Results.NotFound($"No document storage client registered with id '{clientId}'.");
        }

        try
        {
            var result = await action(accessor, form);
            return result.IsSuccess
                ? Results.Ok(new { message = result.Messages?.LastOrDefault() ?? "Document action completed." })
                : MapFailure(result);
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    private static async Task<HttpResult> DownloadDocumentAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var clientId = GetQueryValue(context.Request.Query, "clientId");
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return Results.Problem("Document client is required.", statusCode: (int)HttpStatusCode.BadRequest);
        }

        var factory = context.RequestServices.GetService<IDocumentStoreClientFactory>();
        if (factory is null)
        {
            return Results.Problem("AddDocumentStorage() is not registered.", statusCode: (int)HttpStatusCode.ServiceUnavailable);
        }

        var accessor = factory.Create(clientId);
        if (accessor is null)
        {
            return Results.NotFound($"No document storage client registered with id '{clientId}'.");
        }

        var key = new DocumentKey(
            GetQueryValue(context.Request.Query, "partitionKey"),
            GetQueryValue(context.Request.Query, "rowKey"));
        var result = await accessor.GetJsonResultAsync(key, cancellationToken);

        return result.IsSuccess
            ? Results.File(
                Encoding.UTF8.GetBytes(result.Value ?? string.Empty),
                "application/json; charset=utf-8",
                CreateDownloadFileName(key))
            : MapFailure(result);
    }

    private static HttpResult MapFailure(BridgingIT.DevKit.Common.IResult result)
    {
        var error = result.Errors?.FirstOrDefault();
        var message = result.Messages?.LastOrDefault()
            ?? error?.Message
            ?? "The document storage request failed.";

        return error switch
        {
            ValidationError => Results.Problem(message, statusCode: (int)HttpStatusCode.BadRequest),
            DocumentStoreNotFoundError => Results.NotFound(message),
            NotFoundError => Results.NotFound(message),
            ConflictError => Results.Problem(message, statusCode: (int)HttpStatusCode.Conflict),
            AccessDeniedError => Results.Problem(message, statusCode: (int)HttpStatusCode.Forbidden),
            UnauthorizedError => Results.Problem(message, statusCode: (int)HttpStatusCode.Unauthorized),
            OperationCancelledError => Results.Problem(message, statusCode: (int)HttpStatusCode.RequestTimeout),
            _ => Results.Problem(message, statusCode: (int)HttpStatusCode.InternalServerError)
        };
    }

    private static DocumentKey CreateDocumentKey(IFormCollection form) =>
        new(GetFormValue(form, "partitionKey"), GetFormValue(form, "rowKey"));

    private static IReadOnlyList<DocumentKey> CreateDocumentKeys(IFormCollection form)
    {
        var partitionKeys = form["partitionKey"];
        var rowKeys = form["rowKey"];
        var count = Math.Min(partitionKeys.Count, rowKeys.Count);
        var keys = new List<DocumentKey>(count);

        for (var i = 0; i < count; i++)
        {
            keys.Add(new DocumentKey(partitionKeys[i], rowKeys[i]));
        }

        return keys;
    }

    private static async Task<Result> DeleteDocumentsAsync(
        IDocumentStoreClientAccessor accessor,
        IReadOnlyList<DocumentKey> keys,
        CancellationToken cancellationToken)
    {
        if (keys.Count == 0)
        {
            return Result.Failure(new ValidationError("Select at least one document to delete."));
        }

        foreach (var key in keys)
        {
            var result = await accessor.DeleteResultAsync(key, cancellationToken);
            if (result.IsFailure)
            {
                return result;
            }
        }

        return Result.Success()
            .WithMessage($"{keys.Count} document(s) deleted.");
    }

    private static Result ValidateJsonContent(string content)
    {
        try
        {
            using var _ = JsonDocument.Parse(content);
            return Result.Success();
        }
        catch (JsonException ex)
        {
            return Result.Failure(new ValidationError($"Document payload must be valid JSON: {ex.Message}"));
        }
    }

    private static Result ValidateDocumentKey(DocumentKey key) =>
        string.IsNullOrWhiteSpace(key.PartitionKey) || string.IsNullOrWhiteSpace(key.RowKey)
            ? Result.Failure(new ValidationError("Partition key and row key are required."))
            : Result.Success();

    private static string GetFormValue(IFormCollection form, string key) =>
        form.TryGetValue(key, out var value) ? value.ToString() : string.Empty;

    private static string GetFormText(IFormCollection form, string key) =>
        form.TryGetValue(key, out var value) ? value.ToString() : string.Empty;

    private static string GetQueryValue(IQueryCollection query, string key) =>
        query.TryGetValue(key, out var value) ? value.ToString() : string.Empty;

    private static string CreateDownloadFileName(DocumentKey key)
    {
        var value = $"{key.PartitionKey}-{key.RowKey}";
        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            builder.Append(invalidChars.Contains(character) ? '_' : character);
        }

        return $"{builder}.json";
    }
}
