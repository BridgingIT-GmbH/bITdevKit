// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage.Documents.Dashboard;

using System.Net;
using System.Globalization;
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
    private const string DocumentsPath = "/storage/documents";
    private const string DocumentsContentPath = "/storage/documents/content";
    private const string DocumentsDownloadPath = "/storage/documents/download";
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
                    var existingResult = await accessor.GetJsonAsync(key, cancellationToken);
                    if (existingResult.IsSuccess)
                    {
                        return Result.Failure(new ConflictError($"Document '{key.PartitionKey}/{key.RowKey}' already exists."));
                    }

                    if (!existingResult.Errors.Any(e => e is DocumentStoreNotFoundError or NotFoundError))
                    {
                        return Result.Failure(existingResult.Messages, existingResult.Errors);
                    }
                }

                var metadata = ParseProperties(GetFormText(form, "properties"));
                if (metadata.IsFailure)
                {
                    return Result.Failure(metadata.Messages, metadata.Errors);
                }

                var expiresAt = GetFormValue(form, "expiresAt");
                var writeOptions = new DocumentWriteOptions
                {
                    CreateOnly = string.Equals(GetFormValue(form, "mode"), "new", StringComparison.OrdinalIgnoreCase),
                    IfMatchETag = GetFormValue(form, "etag"),
                    Properties = metadata.Value,
                    Expiration = string.IsNullOrWhiteSpace(expiresAt)
                        ? ExpirationChange.Clear
                        : DateTimeOffset.TryParse(expiresAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
                            ? ExpirationChange.At(parsed)
                            : null
                };
                if (writeOptions.Expiration is null)
                {
                    return Result.Failure(new ValidationError("Expiration must be an ISO-8601 timestamp."));
                }

                return await accessor.UpsertJsonAsync(key, content, writeOptions, cancellationToken);
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
                    : await accessor.DeleteAsync(key, new DocumentDeleteOptions { IfMatchETag = GetFormValue(form, "etag") }, cancellationToken);
            }))
            .WithName("_bdk.Dashboard.Storage.Documents.Delete")
            .WithSummary("Delete document storage document")
            .DisableAntiforgery()
            .ExcludeFromDescription();

    }

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
        var result = await accessor.GetJsonAsync(key, cancellationToken);

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

    private static Result<PropertyBag> ParseProperties(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Result<PropertyBag>.Success(new());
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Result<PropertyBag>.Failure(new ValidationError("Properties must be a JSON object."));
            }

            var result = new PropertyBag();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                result.Set(property.Name, property.Value.ValueKind switch
                {
                    JsonValueKind.Null => null,
                    JsonValueKind.String when property.Value.TryGetDateTimeOffset(out var date) => date,
                    JsonValueKind.String when property.Value.TryGetGuid(out var guid) => guid,
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Number when property.Value.TryGetInt64(out var integer) => integer,
                    JsonValueKind.Number => property.Value.GetDecimal(),
                    _ => throw new JsonException($"Property '{property.Name}' must contain a scalar value.")
                });
            }

            return Result<PropertyBag>.Success(result);
        }
        catch (Exception ex) when (ex is JsonException or FormatException)
        {
            return Result<PropertyBag>.Failure(new ValidationError($"Properties are invalid: {ex.Message}"));
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

        return builder.ToString().EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? builder.ToString()
            : $"{builder}.json";
    }
}
