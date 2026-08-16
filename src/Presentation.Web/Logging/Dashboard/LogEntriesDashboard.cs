// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Logging.Dashboard;

using System.Globalization;
using BridgingIT.DevKit.Application.Utilities;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides shared helpers for the log entries dashboard pages.
/// </summary>
/// <example>
/// <code>
/// var filter = LogEntriesDashboard.CreateFilter(httpContext);
/// </code>
/// </example>
public static class LogEntriesDashboard
{
    /// <summary>
    /// Defines the default page size value.
    /// </summary>
    public const int DefaultPageSize = 100;
    /// <summary>
    /// Defines the default level value.
    /// </summary>
    public const LogLevel DefaultLevel = LogLevel.Information;

    /// <summary>
    /// Creates filter.
    /// </summary>
    /// <param name="httpContext">The http context used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static LogEntriesDashboardFilter CreateFilter(HttpContext httpContext)
    {
        var query = httpContext.Request.Query;
        var filter = new LogEntriesDashboardFilter
        {
            SearchText = EmptyToNull(query["q"].ToString()),
            Level = query.ContainsKey("level") ? ParseLevel(query["level"].ToString()) : DefaultLevel,
            PageSize = ParsePageSize(query["pageSize"].ToString()),
            StartTime = ParseLocalDateStart(query["from"].ToString()) ?? DateTimeOffset.Now.Date,
            EndTime = ParseLocalDateEnd(query["to"].ToString()),
            TraceId = EmptyToNull(query["traceId"].ToString()),
            SpanId = EmptyToNull(query["spanId"].ToString()),
            CorrelationId = EmptyToNull(query["correlationId"].ToString()),
            LogKey = EmptyToNull(query["logKey"].ToString()),
            ModuleName = EmptyToNull(query["moduleName"].ToString()),
            ShortTypeName = EmptyToNull(query["shortTypeName"].ToString()),
            ContinuationToken = EmptyToNull(query["continuationToken"].ToString()),
            AfterId = ParseLong(query["afterId"].ToString())
        };

        return filter;
    }

    /// <summary>
    /// Creates request.
    /// </summary>
    /// <param name="filter">The filter used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static LogEntryQueryRequest CreateRequest(LogEntriesDashboardFilter filter)
    {
        return new LogEntryQueryRequest
        {
            StartTime = filter.StartTime,
            EndTime = filter.EndTime,
            Level = filter.Level,
            TraceId = filter.TraceId,
            SpanId = filter.SpanId,
            CorrelationId = filter.CorrelationId,
            LogKey = filter.LogKey,
            ModuleName = filter.ModuleName,
            ShortTypeName = filter.ShortTypeName,
            SearchText = filter.SearchText,
            PageSize = filter.PageSize,
            ContinuationToken = filter.ContinuationToken,
            AfterId = filter.AfterId
        };
    }

    /// <summary>
    /// Executes the build query operation.
    /// </summary>
    /// <param name="filter">The filter used by the operation.</param>
    /// <param name="continuationToken">The continuation token used by the operation.</param>
    /// <param name="afterId">The after id used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static string BuildQuery(LogEntriesDashboardFilter filter, string continuationToken = null, long? afterId = null)
    {
        var values = new Dictionary<string, string>
        {
            ["q"] = filter.SearchText,
            ["level"] = filter.Level?.ToString() ?? "All",
            ["pageSize"] = filter.PageSize.ToString(CultureInfo.InvariantCulture),
            ["from"] = FormatDate(filter.StartTime),
            ["to"] = FormatDate(filter.EndTime),
            ["traceId"] = filter.TraceId,
            ["spanId"] = filter.SpanId,
            ["correlationId"] = filter.CorrelationId,
            ["logKey"] = filter.LogKey,
            ["moduleName"] = filter.ModuleName,
            ["shortTypeName"] = filter.ShortTypeName,
            ["continuationToken"] = continuationToken,
            ["afterId"] = afterId?.ToString(CultureInfo.InvariantCulture)
        };

        return QueryHelpers.AddQueryString(string.Empty, values
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(pair => pair.Key, pair => pair.Value));
    }

    /// <summary>
    /// Builds a dashboard log entries link filtered to a correlation identifier.
    /// </summary>
    /// <param name="options">The dashboard endpoint options.</param>
    /// <param name="correlationId">The correlation identifier.</param>
    /// <returns>The filtered log entries URL, or <c>null</c> when no correlation identifier is supplied.</returns>
    /// <example>
    /// <code>
    /// var href = LogEntriesDashboard.BuildCorrelationHref(options, "correlation-1");
    /// </code>
    /// </example>
    public static string BuildCorrelationHref(DashboardEndpointsOptions options, string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return null;
        }

        var filter = new LogEntriesDashboardFilter
        {
            Level = null,
            PageSize = DefaultPageSize,
            StartTime = DateTimeOffset.Now.Date,
            CorrelationId = correlationId.Trim()
        };

        return $"{DashboardEndpoints.BuildLogEntriesPath(options)}{BuildQuery(filter)}";
    }

    /// <summary>
    /// Executes the format date operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static string FormatDate(DateTimeOffset? value)
    {
        return value?.LocalDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Executes the format timestamp operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static string FormatTimestamp(DateTimeOffset value)
    {
        return value.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Executes the format short timestamp operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static string FormatShortTimestamp(DateTimeOffset value)
    {
        return value.LocalDateTime.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Executes the short id operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <param name="maxLength">The max length used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static string ShortId(string value, int maxLength = 12)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "-"
            : value.Length <= maxLength ? value : value[..maxLength];
    }

    /// <summary>
    /// Executes the display value operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static string DisplayValue(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    /// <summary>
    /// Executes the display level operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static string DisplayLevel(string value)
    {
        return string.Equals(value, "Critical", StringComparison.OrdinalIgnoreCase) ? "Fatal" : DisplayValue(value);
    }

    /// <summary>
    /// Gets level badge class.
    /// </summary>
    /// <param name="level">The level used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static string GetLevelBadgeClass(string level)
    {
        return level switch
        {
            "Fatal" or "Critical" or "Error" => "bg-danger",
            "Warning" => "bg-warning text-dark",
            "Information" => "bg-info text-dark",
            "Debug" => "bg-secondary",
            "Verbose" or "Trace" => "bg-dark",
            _ => "bg-secondary"
        };
    }

    /// <summary>
    /// Gets row class.
    /// </summary>
    /// <param name="level">The level used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static string GetRowClass(string level)
    {
        return level switch
        {
            "Fatal" or "Critical" or "Error" => "table-danger",
            "Warning" => "table-warning",
            _ => string.Empty
        };
    }

    private static string EmptyToNull(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static int ParsePageSize(string value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Clamp(parsed, 25, 500)
            : DefaultPageSize;
    }

    private static long? ParseLong(string value)
    {
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static LogLevel? ParseLevel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultLevel;
        }

        if (string.Equals(value, "All", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.Equals(value, "Verbose", StringComparison.OrdinalIgnoreCase))
        {
            return LogLevel.Trace;
        }

        if (string.Equals(value, "Fatal", StringComparison.OrdinalIgnoreCase))
        {
            return LogLevel.Critical;
        }

        return Enum.TryParse<LogLevel>(value, true, out var parsed) ? parsed : null;
    }

    private static DateTimeOffset? ParseLocalDateStart(string value)
    {
        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
        {
            return null;
        }

        return new DateTimeOffset(parsed.Date, TimeZoneInfo.Local.GetUtcOffset(parsed.Date));
    }

    private static DateTimeOffset? ParseLocalDateEnd(string value)
    {
        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
        {
            return null;
        }

        var end = parsed.Date.AddDays(1).AddTicks(-1);
        return new DateTimeOffset(end, TimeZoneInfo.Local.GetUtcOffset(end));
    }
}

/// <summary>
/// Represents log entries dashboard filter.
/// </summary>
public sealed class LogEntriesDashboardFilter
{
    /// <summary>
    /// Gets or sets the search text.
    /// </summary>
    public string SearchText { get; init; }

    /// <summary>
    /// Gets or sets the level.
    /// </summary>
    public LogLevel? Level { get; init; }

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public int PageSize { get; init; } = LogEntriesDashboard.DefaultPageSize;

    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    public DateTimeOffset? StartTime { get; init; }

    /// <summary>
    /// Gets or sets the end time.
    /// </summary>
    public DateTimeOffset? EndTime { get; init; }

    /// <summary>
    /// Gets or sets the trace id.
    /// </summary>
    public string TraceId { get; init; }

    /// <summary>
    /// Gets or sets the span id.
    /// </summary>
    public string SpanId { get; init; }

    /// <summary>
    /// Gets or sets the correlation id.
    /// </summary>
    public string CorrelationId { get; init; }

    /// <summary>
    /// Gets or sets the log key.
    /// </summary>
    public string LogKey { get; init; }

    /// <summary>
    /// Gets or sets the module name.
    /// </summary>
    public string ModuleName { get; init; }

    /// <summary>
    /// Gets or sets the short type name.
    /// </summary>
    public string ShortTypeName { get; init; }

    /// <summary>
    /// Gets or sets the continuation token.
    /// </summary>
    public string ContinuationToken { get; init; }

    /// <summary>
    /// Gets or sets the callback invoked after the id operation.
    /// </summary>
    public long? AfterId { get; init; }
}
