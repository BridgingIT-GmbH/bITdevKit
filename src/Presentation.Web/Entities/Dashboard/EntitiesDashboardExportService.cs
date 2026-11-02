// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Entities.Dashboard;

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>The result of an export request.</summary>
/// <param name="StatusCode">The status code: 200, 400 or 500.</param>
/// <param name="Error">The user-facing error, or null.</param>
/// <param name="Content">The file content.</param>
/// <param name="ContentType">The content type.</param>
/// <param name="FileName">The file name (<c>{EntityName}-{yyyyMMddHHmmss}.{json|csv}</c>).</param>
/// <param name="Truncated">True when more rows matched than <see cref="EntitiesDashboardOptions.MaxExportRows"/>.</param>
/// <param name="RowCount">The number of exported rows.</param>
internal sealed record EntitiesDashboardExportResult(
    int StatusCode,
    string Error,
    byte[] Content,
    string ContentType,
    string FileName,
    bool Truncated,
    int RowCount)
{
    /// <summary>Gets a value indicating whether the export succeeded.</summary>
    public bool Success => this.StatusCode == StatusCodes.Status200OK;
}

/// <summary>Exports the filtered and sorted rows of an entity as JSON or CSV. Masked values and hidden properties are never exported.</summary>
/// <param name="loggerFactory">The optional logger factory.</param>
internal sealed class EntitiesDashboardExportService(ILoggerFactory loggerFactory = null)
{
    private static readonly char[] FormulaPrefixes = ['=', '+', '-', '@', '\t', '\r'];

    private readonly ILogger logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<EntitiesDashboardExportService>();

    /// <summary>Exports the rows matching the query.</summary>
    /// <param name="http">The HTTP context.</param>
    /// <param name="entity">The entity.</param>
    /// <param name="query">The parsed query (filters, sort and visible columns; the batch is ignored).</param>
    /// <param name="format">The format, <c>json</c> or <c>csv</c>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The export or an error.</returns>
    public async Task<EntitiesDashboardExportResult> ExportAsync(
        HttpContext http,
        EntitiesDashboardEntity entity,
        EntitiesDashboardQuery query,
        string format,
        CancellationToken cancellationToken)
    {
        var isCsv = string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase);
        if (!isCsv && !string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            return Error(StatusCodes.Status400BadRequest, "The export format must be json or csv.");
        }

        var columns = this.ResolveColumns(entity, query.Columns, out var columnError);
        if (columnError != null)
        {
            return Error(StatusCodes.Status400BadRequest, columnError);
        }

        var source = EntitiesDashboardQueryService.ResolveSource(entity, query.Source);
        if (source == null)
        {
            return Error(StatusCodes.Status400BadRequest, "The source is not available for this entity.");
        }

        var max = Math.Max(1, EntitiesDashboardOptionsAccessor.Get(http.RequestServices).MaxExportRows);
        var filter = EntitiesDashboardFilterBuilder.Build(entity, query with { Batch = 1 }, max + 1);
        if (filter.IsFailure)
        {
            return Error(StatusCodes.Status400BadRequest, filter.Messages.FirstOrDefault() ?? "The filter is invalid.");
        }

        try
        {
            var result = await source.FindAllAsync(http.RequestServices, filter.Value, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                var correlationId = EntitiesDashboardOutcome.GetCorrelationId(http);
                EntitiesDashboardLog.FailedResult(this.logger, entity.Name, "export", correlationId, result.Errors.FirstOrDefault()?.GetType().Name, result.Errors.FirstOrDefault()?.Message);

                return Error(StatusCodes.Status500InternalServerError, $"The export failed. Correlation id: {correlationId}");
            }

            var all = (result.Value ?? []).ToList();
            var truncated = all.Count > max;
            var rows = all.Take(max).Select(r => EntitiesDashboardMasking.Project(entity, r)).ToList();

            var content = isCsv ? BuildCsv(columns, rows) : BuildJson(columns, rows);
            var extension = isCsv ? "csv" : "json";
            var fileName = $"{entity.Name}-{DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)}.{extension}";

            EntitiesDashboardLog.Exported(this.logger, entity.Name, extension, rows.Count, truncated, EntitiesDashboardWriteService.GetUserName(http));

            return new EntitiesDashboardExportResult(
                StatusCodes.Status200OK,
                null,
                content,
                isCsv ? "text/csv; charset=utf-8" : "application/json; charset=utf-8",
                fileName,
                truncated,
                rows.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            var correlationId = EntitiesDashboardOutcome.GetCorrelationId(http);
            EntitiesDashboardLog.Failed(this.logger, ex, entity.Name, "export", correlationId);

            return Error(StatusCodes.Status500InternalServerError, $"The export failed. Correlation id: {correlationId}");
        }
    }

    /// <summary>Escapes a CSV cell: formula prefix escaping (SEC-008) and RFC 4180 quoting.</summary>
    /// <param name="text">The cell text.</param>
    /// <returns>The escaped cell.</returns>
    /// <param name="isNumber">True when the cell holds a number, so a leading sign is not a formula.</param>
    internal static string EscapeCsv(string text, bool isNumber = false)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        if (!isNumber && Array.IndexOf(FormulaPrefixes, text[0]) >= 0)
        {
            text = "'" + text;
        }

        return text.IndexOfAny(['"', ',', '\r', '\n']) >= 0
            ? "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : text;
    }

    private static byte[] BuildCsv(IReadOnlyList<EntityPropertyDescriptor> columns, IReadOnlyList<IDictionary<string, object>> rows)
    {
        var builder = new StringBuilder();
        builder.Append(string.Join(',', columns.Select(c => EscapeCsv(c.Name)))).Append("\r\n");
        foreach (var row in rows)
        {
            builder.Append(string.Join(',', columns.Select(c => EscapeCsv(CellText(row, c), row.TryGetValue(c.Name, out var cell) && IsNumber(cell))))).Append("\r\n");
        }

        // BOM so spreadsheet applications detect UTF-8
        return new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
    }

    private static byte[] BuildJson(IReadOnlyList<EntityPropertyDescriptor> columns, IReadOnlyList<IDictionary<string, object>> rows)
    {
        var array = new JsonArray();
        foreach (var row in rows)
        {
            var item = new JsonObject();
            foreach (var column in columns)
            {
                row.TryGetValue(column.Name, out var value);
                item[column.Name] = value switch
                {
                    null => null,
                    string text when column.EditorKind == EntitiesDashboardEditorKind.Json && !column.IsMasked => ParseNode(text),
                    string text => JsonValue.Create(text),
                    bool flag => JsonValue.Create(flag),
                    sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal =>
                        JsonSerializer.SerializeToNode(value, value.GetType()),
                    _ => JsonValue.Create(Convert.ToString(value, CultureInfo.InvariantCulture))
                };
            }

            array.Add(item);
        }

        return Encoding.UTF8.GetBytes(array.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static JsonNode ParseNode(string json)
    {
        try
        {
            return JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return JsonValue.Create(json);
        }
    }

    private static bool IsNumber(object value) =>
        value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;

    private static string CellText(IDictionary<string, object> row, EntityPropertyDescriptor column)
    {
        if (!row.TryGetValue(column.Name, out var value) || value == null)
        {
            return string.Empty;
        }

        return value switch
        {
            bool flag => flag ? "true" : "false",
            string text when column.EditorKind == EntitiesDashboardEditorKind.Json && !column.IsMasked => CompactJson(text),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
        };
    }

    private static string CompactJson(string json)
    {
        try
        {
            return JsonNode.Parse(json)?.ToJsonString() ?? json;
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private static EntitiesDashboardExportResult Error(int statusCode, string message) =>
        new(statusCode, message, null, null, null, false, 0);

    private IReadOnlyList<EntityPropertyDescriptor> ResolveColumns(EntitiesDashboardEntity entity, IReadOnlyList<string> requested, out string error)
    {
        error = null;
        var visible = EntitiesDashboardMasking.GetVisibleProperties(entity);
        if (requested == null || requested.Count == 0)
        {
            return visible.Where(p => p.EditorKind != EntitiesDashboardEditorKind.Unsupported).ToList();
        }

        var selected = new List<EntityPropertyDescriptor>();
        var id = entity.IdProperty;
        if (id != null)
        {
            selected.Add(id);
        }

        foreach (var path in requested)
        {
            var property = visible.FirstOrDefault(p => string.Equals(p.Name, path, StringComparison.OrdinalIgnoreCase));
            if (property == null || property.EditorKind == EntitiesDashboardEditorKind.Unsupported)
            {
                error = "One or more export columns are not available for this entity.";

                return null;
            }

            if (!selected.Contains(property))
            {
                selected.Add(property);
            }
        }

        return selected;
    }
}
