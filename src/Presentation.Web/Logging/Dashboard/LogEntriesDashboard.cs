// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Logging.Dashboard;

using System.Globalization;
using BridgingIT.DevKit.Application.Utilities;
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
    public const int DefaultPageSize = 100;
    public const LogLevel DefaultLevel = LogLevel.Information;

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

    public static string FormatDate(DateTimeOffset? value)
    {
        return value?.LocalDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public static string FormatTimestamp(DateTimeOffset value)
    {
        return value.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }

    public static string FormatShortTimestamp(DateTimeOffset value)
    {
        return value.LocalDateTime.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }

    public static string ShortId(string value, int maxLength = 12)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "-"
            : value.Length <= maxLength ? value : value[..maxLength];
    }

    public static string DisplayValue(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    public static string DisplayLevel(string value)
    {
        return string.Equals(value, "Critical", StringComparison.OrdinalIgnoreCase) ? "Fatal" : DisplayValue(value);
    }

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

public sealed class LogEntriesDashboardFilter
{
    public string SearchText { get; init; }

    public LogLevel? Level { get; init; }

    public int PageSize { get; init; } = LogEntriesDashboard.DefaultPageSize;

    public DateTimeOffset? StartTime { get; init; }

    public DateTimeOffset? EndTime { get; init; }

    public string TraceId { get; init; }

    public string SpanId { get; init; }

    public string CorrelationId { get; init; }

    public string LogKey { get; init; }

    public string ModuleName { get; init; }

    public string ShortTypeName { get; init; }

    public string ContinuationToken { get; init; }

    public long? AfterId { get; init; }
}
