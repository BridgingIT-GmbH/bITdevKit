// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

using System.Text.Json;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.Entities.Dashboard.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using RazorSlices;
using IResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>Request handlers of the Entities dashboard endpoints: builds the view models and delegates to the services.</summary>
internal static class EntitiesDashboardHandlers
{
    /// <summary>The maximum accepted JSON request body size in bytes (256 KiB).</summary>
    public const int MaxBodyBytes = 256 * 1024;

    /// <summary>Renders the page (entity picker, filters and the first batch of rows or the selected record).</summary>
    public static async Task<IResult> GetPageAsync(HttpContext http, string path, CancellationToken cancellationToken)
    {
        var catalog = http.RequestServices.GetRequiredService<EntitiesDashboardCatalog>();
        var options = EntitiesDashboardOptionsAccessor.Get(http.RequestServices);
        var query = EntitiesDashboardQuery.From(http.Request.Query);
        var canEdit = await CanEditAsync(http).ConfigureAwait(false);
        var entities = catalog.Entities.OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        var entity = query.Entity == null ? null : catalog.Find(query.Entity);
        var statusCode = StatusCodes.Status200OK;
        string error = null;
        if (query.Entity != null && entity == null)
        {
            statusCode = StatusCodes.Status404NotFound;
            error = "The entity is not available.";
        }

        EntitiesDashboardRowsViewModel rows = null;
        EntitiesDashboardDetailViewModel detail = null;
        var filter = new EntitiesDashboardFilterState(query, []);
        IReadOnlyList<EntityPropertyDescriptor> columns = [];
        IReadOnlyList<EntityPropertyDescriptor> filterable = [];
        if (entity != null)
        {
            var conditions = EntitiesDashboardFilterBuilder.ParseConditions(entity, query);
            filter = new EntitiesDashboardFilterState(query, conditions.IsSuccess ? conditions.Value : []);
            columns = EntitiesDashboardMasking.GetVisibleProperties(entity);
            filterable = columns.Where(c => c.IsFilterable).ToList();
            var service = http.RequestServices.GetRequiredService<EntitiesDashboardQueryService>();
            rows = BuildRows(
                path,
                entity,
                columns,
                await service.GetRowsAsync(http, entity, query, cancellationToken).ConfigureAwait(false),
                filter,
                canEdit,
                options.PageSizes);
            if (query.Id != null)
            {
                detail = BuildDetail(
                    path,
                    entity,
                    query.Source,
                    columns,
                    await service.GetDetailAsync(http, entity, query.Source, query.Id, cancellationToken).ConfigureAwait(false),
                    canEdit);
            }
        }

        var model = new EntitiesDashboardViewModel
        {
            Path = path,
            Entities = entities,
            Entity = entity,
            Source = entity == null ? null : EntitiesDashboardQueryService.ResolveSource(entity, query.Source)?.SourceName,
            Mode = detail != null ? "detail" : "list",
            CanEdit = canEdit,
            IsEditConfigured = options.IsEditConfigured,
            Filter = filter,
            FilterableProperties = filterable,
            Columns = columns,
            Rows = rows,
            Detail = detail,
            BatchSize = options.EffectiveBatchSize,
            MaxBulkSelection = options.MaxBulkSelection,
            MaxExportRows = options.MaxExportRows,
            Error = error
        };

        return Results.RazorSlice<Pages.Index, EntitiesDashboardViewModel>(model, statusCode);
    }

    /// <summary>Renders one batch of rows (also used for load-more).</summary>
    public static async Task<IResult> GetRowsAsync(HttpContext http, string path, CancellationToken cancellationToken)
    {
        var query = EntitiesDashboardQuery.From(http.Request.Query);
        var entity = Find(http, query.Entity);
        var canEdit = await CanEditAsync(http).ConfigureAwait(false);
        if (entity == null)
        {
            return Results.RazorSlice<Pages.Rows, EntitiesDashboardRowsViewModel>(
                new EntitiesDashboardRowsViewModel
                {
                    Path = path,
                    Error = "The entity is not available.",
                    StatusCode = StatusCodes.Status404NotFound,
                    Filter = new EntitiesDashboardFilterState(query, [])
                },
                StatusCodes.Status404NotFound);
        }

        var conditions = EntitiesDashboardFilterBuilder.ParseConditions(entity, query);
        var filter = new EntitiesDashboardFilterState(query, conditions.IsSuccess ? conditions.Value : []);
        var result = await http.RequestServices.GetRequiredService<EntitiesDashboardQueryService>()
            .GetRowsAsync(http, entity, query, cancellationToken).ConfigureAwait(false);
        var model = BuildRows(path, entity, EntitiesDashboardMasking.GetVisibleProperties(entity), result, filter, canEdit);

        return Results.RazorSlice<Pages.Rows, EntitiesDashboardRowsViewModel>(model, result.StatusCode);
    }

    /// <summary>Renders the detail of one record.</summary>
    public static async Task<IResult> GetDetailAsync(HttpContext http, string path, CancellationToken cancellationToken)
    {
        var query = EntitiesDashboardQuery.From(http.Request.Query);
        var entity = Find(http, query.Entity);
        if (entity == null)
        {
            return Results.RazorSlice<Pages.Detail, EntitiesDashboardDetailViewModel>(
                new EntitiesDashboardDetailViewModel
                {
                    Path = path,
                    Error = "The entity is not available.",
                    StatusCode = StatusCodes.Status404NotFound
                },
                StatusCodes.Status404NotFound);
        }

        var result = await http.RequestServices.GetRequiredService<EntitiesDashboardQueryService>()
            .GetDetailAsync(http, entity, query.Source, query.Id, cancellationToken).ConfigureAwait(false);
        var model = BuildDetail(path, entity, query.Source, EntitiesDashboardMasking.GetVisibleProperties(entity), result, await CanEditAsync(http).ConfigureAwait(false));

        return Results.RazorSlice<Pages.Detail, EntitiesDashboardDetailViewModel>(model, result.StatusCode);
    }

    /// <summary>Renders the editor for create (no <c>id</c>) or edit. The write guard runs first.</summary>
    public static async Task<IResult> GetEditorAsync(HttpContext http, string path, CancellationToken cancellationToken)
    {
        var denied = await EntitiesDashboardWriteGuard.CheckAsync(http).ConfigureAwait(false);
        if (denied != null)
        {
            return denied;
        }

        var query = EntitiesDashboardQuery.From(http.Request.Query);
        var entity = Find(http, query.Entity);
        if (entity == null)
        {
            return EditorError(path, null, null, "The entity is not available.", StatusCodes.Status404NotFound);
        }

        var source = EntitiesDashboardQueryService.ResolveSource(entity, query.Source);
        if (source == null)
        {
            return EditorError(path, entity, null, "The source is not available for this entity.", StatusCodes.Status400BadRequest);
        }

        var isCreate = query.Id == null;
        if ((isCreate && !entity.CanCreate) || (!isCreate && !entity.CanUpdate))
        {
            return EditorError(path, entity, source.SourceName, entity.CapabilityNote ?? "This entity cannot be edited.", StatusCodes.Status400BadRequest, entity.CapabilityNote);
        }

        IDictionary<string, object> row = null;
        string version = null;
        if (!isCreate)
        {
            var detail = await http.RequestServices.GetRequiredService<EntitiesDashboardQueryService>()
                .GetDetailAsync(http, entity, query.Source, query.Id, cancellationToken).ConfigureAwait(false);
            if (!detail.Success)
            {
                return EditorError(path, entity, source.SourceName, detail.Error, detail.StatusCode, correlationId: detail.CorrelationId);
            }

            if (detail.IsDeleted)
            {
                return EditorError(path, entity, source.SourceName, "A deleted record cannot be edited.", StatusCodes.Status409Conflict);
            }

            row = detail.Row;
            version = detail.ConcurrencyVersion;
        }

        var fields = entity.Properties
            .Where(p => !p.IsHidden && p.EditorKind != EntitiesDashboardEditorKind.Unsupported && p.Setter != null)
            .Select(p => new EntitiesDashboardEditorField(
                p,
                row != null && row.TryGetValue(p.Name, out var value) ? value : null,
                p.EditorKind is EntitiesDashboardEditorKind.Enum or EntitiesDashboardEditorKind.Enumeration
                    ? EntitiesDashboardValueConverter.GetOptions(p.UnderlyingType)
                    : [],
                p.IsWritable && !p.IsId))
            .ToList();

        return Results.RazorSlice<Pages.Editor, EntitiesDashboardEditorViewModel>(new EntitiesDashboardEditorViewModel
        {
            Path = path,
            Entity = entity,
            Source = source.SourceName,
            Mode = isCreate ? "create" : "edit",
            Id = query.Id,
            ConcurrencyVersion = version,
            Fields = fields,
            CanEdit = true
        });
    }

    /// <summary>Exports the filtered rows as JSON or CSV.</summary>
    public static async Task<IResult> ExportAsync(HttpContext http, CancellationToken cancellationToken)
    {
        var query = EntitiesDashboardQuery.From(http.Request.Query);
        var entity = Find(http, query.Entity);
        if (entity == null)
        {
            return Results.Text("The entity is not available.", "text/plain", System.Text.Encoding.UTF8, StatusCodes.Status404NotFound);
        }

        var format = http.Request.Query["format"].ToString();
        var result = await http.RequestServices.GetRequiredService<EntitiesDashboardExportService>()
            .ExportAsync(http, entity, query, string.IsNullOrWhiteSpace(format) ? "json" : format, cancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            return Results.Text(result.Error, "text/plain", System.Text.Encoding.UTF8, result.StatusCode);
        }

        http.Response.Headers["X-Entities-Export-Truncated"] = result.Truncated ? "true" : "false";

        return Results.File(result.Content, result.ContentType, result.FileName);
    }

    /// <summary>Creates a record.</summary>
    public static Task<IResult> CreateAsync(HttpContext http, CancellationToken cancellationToken) =>
        WriteAsync(http, (body, token) => http.RequestServices.GetRequiredService<EntitiesDashboardWriteService>().CreateAsync(http, body, token), cancellationToken);

    /// <summary>Updates a record; <c>preview=true</c> returns the changed properties without writing.</summary>
    public static Task<IResult> UpdateAsync(HttpContext http, bool preview, CancellationToken cancellationToken) =>
        WriteAsync(
            http,
            (body, token) => http.RequestServices.GetRequiredService<EntitiesDashboardWriteService>().UpdateAsync(http, body, preview, token),
            cancellationToken);

    /// <summary>Deletes a record.</summary>
    public static Task<IResult> DeleteAsync(HttpContext http, CancellationToken cancellationToken) =>
        WriteAsync(http, (body, token) => http.RequestServices.GetRequiredService<EntitiesDashboardWriteService>().DeleteAsync(http, body, token), cancellationToken);

    /// <summary>Deletes several records.</summary>
    public static Task<IResult> BulkDeleteAsync(HttpContext http, CancellationToken cancellationToken) =>
        WriteAsync(http, (body, token) => http.RequestServices.GetRequiredService<EntitiesDashboardWriteService>().BulkDeleteAsync(http, body, token), cancellationToken);

    private static async Task<IResult> WriteAsync(
        HttpContext http,
        Func<JsonElement, CancellationToken, Task<EntitiesDashboardOutcome>> action,
        CancellationToken cancellationToken)
    {
        // the server-side edit check always runs before the body is read
        var denied = await EntitiesDashboardWriteGuard.CheckAsync(http).ConfigureAwait(false);
        if (denied != null)
        {
            return denied;
        }

        var (body, failure) = await ReadBodyAsync(http, cancellationToken).ConfigureAwait(false);
        if (failure != null)
        {
            return failure.ToResult();
        }

        var outcome = await action(body, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult();
    }

    private static async Task<(JsonElement Body, EntitiesDashboardOutcome Failure)> ReadBodyAsync(HttpContext http, CancellationToken cancellationToken)
    {
        if (http.Request.ContentLength > MaxBodyBytes)
        {
            return (default, EntitiesDashboardOutcome.Failed("The request is too large.", StatusCodes.Status413PayloadTooLarge));
        }

        var feature = http.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (feature is { IsReadOnly: false })
        {
            feature.MaxRequestBodySize = MaxBodyBytes;
        }

        try
        {
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            int read;
            while ((read = await http.Request.Body.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
            {
                buffer.Write(chunk, 0, read);
                if (buffer.Length > MaxBodyBytes)
                {
                    return (default, EntitiesDashboardOutcome.Failed("The request is too large.", StatusCodes.Status413PayloadTooLarge));
                }
            }

            buffer.Position = 0;
            using var document = await JsonDocument.ParseAsync(buffer, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return (default, EntitiesDashboardOutcome.Failed("The request body must be a JSON object."));
            }

            return (document.RootElement.Clone(), null);
        }
        catch (JsonException)
        {
            return (default, EntitiesDashboardOutcome.Failed("The request body is not valid JSON."));
        }
        catch (BadHttpRequestException)
        {
            return (default, EntitiesDashboardOutcome.Failed("The request is too large.", StatusCodes.Status413PayloadTooLarge));
        }
    }

    private static EntitiesDashboardEntity Find(HttpContext http, string key) =>
        string.IsNullOrWhiteSpace(key) ? null : http.RequestServices.GetRequiredService<EntitiesDashboardCatalog>().Find(key);

    private static async Task<bool> CanEditAsync(HttpContext http) =>
        http.RequestServices.GetService<EntitiesDashboardAccessEvaluator>() is { } evaluator
        && await evaluator.CanEditAsync(http.User).ConfigureAwait(false);

    private static IResult EditorError(string path, EntitiesDashboardEntity entity, string source, string error, int statusCode, string capabilityNote = null, string correlationId = null) =>
        Results.RazorSlice<Pages.Editor, EntitiesDashboardEditorViewModel>(
            new EntitiesDashboardEditorViewModel
            {
                Path = path,
                Entity = entity,
                Source = source,
                CanEdit = true,
                CapabilityNote = capabilityNote,
                Error = error,
                CorrelationId = correlationId,
                StatusCode = statusCode
            },
            statusCode);

    private static EntitiesDashboardRowsViewModel BuildRows(
        string path,
        EntitiesDashboardEntity entity,
        IReadOnlyList<EntityPropertyDescriptor> columns,
        EntitiesDashboardRowsResult result,
        EntitiesDashboardFilterState filter,
        bool canEdit,
        IReadOnlyList<int> pageSizes = null) =>
        new()
        {
            Path = path,
            Entity = entity,
            Columns = columns,
            Rows = result.Rows,
            Batch = result.Batch,
            BatchSize = result.BatchSize,
            PageSizes = pageSizes ?? [],
            HasMore = result.HasMore,
            DeletedFlags = result.DeletedFlags,
            CanEdit = canEdit,
            Filter = filter,
            Error = result.Error,
            CorrelationId = result.CorrelationId,
            StatusCode = result.StatusCode
        };

    private static EntitiesDashboardDetailViewModel BuildDetail(
        string path,
        EntitiesDashboardEntity entity,
        string source,
        IReadOnlyList<EntityPropertyDescriptor> properties,
        EntitiesDashboardDetailResult result,
        bool canEdit) =>
        new()
        {
            Path = path,
            Entity = entity,
            Source = EntitiesDashboardQueryService.ResolveSource(entity, source)?.SourceName,
            Properties = properties,
            Row = result.Row,
            Id = result.Id,
            ConcurrencyVersion = result.ConcurrencyVersion,
            IsDeleted = result.IsDeleted,
            ChangeHistoryUrl = result.ChangeHistoryUrl,
            CanEdit = canEdit,
            Error = result.Error,
            CorrelationId = result.CorrelationId,
            StatusCode = result.StatusCode
        };
}
