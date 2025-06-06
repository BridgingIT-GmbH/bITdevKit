// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Utilities;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

public interface ILogQueryService
{
    Task<Stream> ExportLogsAsync(LogQueryRequest request, LogExportFormat format, CancellationToken cancellationToken = default);
    Task<LogStatisticsModel> GetLogStatisticsAsync(DateTimeOffset? startTime = null, DateTimeOffset? endTime = null, TimeSpan? groupByInterval = null, CancellationToken cancellationToken = default);
    Task PurgeLogsAsync(DateTimeOffset olderThan, bool archive = false, int batchSize = 1000, TimeSpan? delayInterval = null, CancellationToken cancellationToken = default);
    Task PurgeLogsAsync(TimeSpan age, bool archive = false, int batchSize = 1000, TimeSpan? delayInterval = null, CancellationToken cancellationToken = default);
    Task<LogQueryResponse> QueryLogsAsync(LogQueryRequest request, CancellationToken cancellationToken = default);
    IAsyncEnumerable<LogEntryModel> StreamLogsAsync(
        DateTimeOffset? startTime = null,
        LogLevel? level = null,
        string traceId = null,
        string correlationId = null,
        string logKey = null,
        string moduleName = null,
        string threadId = null,
        string shortTypeName = null,
        string searchText = null,
        TimeSpan? pollingInterval = null,
        CancellationToken cancellationToken = default);
    Task SubscribeToNotificationsAsync(Func<LogEntryModel, Task> callback, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the response for a log query, containing paged log entries and pagination metadata.
/// </summary>
public class LogQueryResponse
{
    /// <summary>
    /// Gets or sets the list of log entries returned by the query.
    /// </summary>
    public IReadOnlyList<LogEntryModel> Items { get; set; } = new List<LogEntryModel>();

    /// <summary>
    /// Gets or sets the continuation token for retrieving the next page of results.
    /// Null if no more pages are available.
    /// </summary>
    public string ContinuationToken { get; set; }

    /// <summary>
    /// Gets or sets the number of items in the current page.
    /// </summary>
    public int PageSize { get; set; }
}

/// <summary>
/// Represents a request to query log entries with various filtering, searching, paging, and continuation options.
/// </summary>
public class LogQueryRequest
{
    /// <summary>
    /// Gets or sets the start of the time range for filtering logs (inclusive).
    /// If null and <see cref="Age"/> is null, defaults to the start of the current day.
    /// Mutually exclusive with <see cref="Age"/>.
    /// </summary>
    public DateTimeOffset? StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end of the time range for filtering logs (inclusive).
    /// If null, logs are retrieved up to the present.
    /// </summary>
    public DateTimeOffset? EndTime { get; set; }

    /// <summary>
    /// Gets or sets the minimum log level to filter by (e.g., Information).
    /// Includes the specified level and all higher severity levels (e.g., Warning, Error, Fatal).
    /// If null, all levels are included.
    /// </summary>
    public LogLevel? Level { get; set; }

    /// <summary>
    /// Gets or sets the TraceId to filter by.
    /// If null or empty, no TraceId filter is applied.
    /// </summary>
    public string TraceId { get; set; }

    /// <summary>
    /// Gets or sets the CorrelationId to filter by.
    /// If null or empty, no CorrelationId filter is applied.
    /// </summary>
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the LogKey to filter by.
    /// If null or empty, no LogKey filter is applied.
    /// </summary>
    public string LogKey { get; set; }

    /// <summary>
    /// Gets or sets the ModuleName to filter by.
    /// If null or empty, no ModuleName filter is applied.
    /// </summary>
    public string ModuleName { get; set; }

    /// <summary>
    /// Gets or sets the ThreadId to filter by.
    /// If null or empty, no ThreadId filter is applied.
    /// </summary>
    public string ThreadId { get; set; }

    /// <summary>
    /// Gets or sets the ShortTypeName to filter by.
    /// If null or empty, no ShortTypeName filter is applied.
    /// </summary>
    public string ShortTypeName { get; set; }

    /// <summary>
    /// Gets or sets the text to search within Message, Exception, and LogEvent fields.
    /// If null or empty, no full-text search is applied.
    /// </summary>
    public string SearchText { get; set; }

    /// <summary>
    /// Gets or sets the number of records per page (default: 1000).
    /// Must be a positive integer.
    /// </summary>
    public int PageSize { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the continuation token for retrieving the next page of results.
    /// If null or empty, starts from the first page.
    /// </summary>
    public string ContinuationToken { get; set; }

    /// <summary>
    /// Gets or sets the age of logs to include, as a duration from the present.
    /// Converted to <see cref="StartTime"/> as (UtcNow - Age).
    /// Mutually exclusive with <see cref="StartTime"/>.
    /// </summary>
    public TimeSpan? Age { get; set; }

    /// <summary>
    /// Validates the request parameters.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    public void Validate()
    {
        if (this.StartTime.HasValue && this.Age.HasValue)
        {
            throw new ArgumentException("StartTime and Age cannot both be specified.");
        }

        if (this.Age < TimeSpan.Zero)
        {
            throw new ArgumentException("Age cannot be negative.");
        }

        if (this.StartTime.HasValue && this.EndTime.HasValue && this.StartTime > this.EndTime)
        {
            throw new ArgumentException("StartTime cannot be greater than EndTime.");
        }

        if (this.PageSize <= 0)
        {
            throw new ArgumentException("PageSize must be positive.");
        }

        //if (!string.IsNullOrEmpty(this.LogKey) && this.LogKey.Contains(";"))
        //{
        //    throw new ArgumentException("LogKey contains invalid characters.");
        //}

        //if (!string.IsNullOrEmpty(this.ModuleName) && this.ModuleName.Contains(";"))
        //{
        //    throw new ArgumentException("ModuleName contains invalid characters.");
        //}

        //if (!string.IsNullOrEmpty(this.ThreadId) && this.ThreadId.Contains(";"))
        //{
        //    throw new ArgumentException("ThreadId contains invalid characters.");
        //}

        //if (!string.IsNullOrEmpty(this.ShortTypeName) && this.ShortTypeName.Contains(";"))
        //{
        //    throw new ArgumentException("ShortTypeName contains invalid characters.");
        //}

        if (!string.IsNullOrEmpty(this.SearchText) && Regex.IsMatch(this.SearchText, @"[\p{Cc}\p{Cf}]"))
        {
            throw new ArgumentException("SearchText contains invalid control characters.");
        }
    }
}

/// <summary>
/// Data transfer object for representing a log entry in API responses.
/// </summary>
public class LogEntryModel
{
    /// <summary>
    /// Gets or sets the unique identifier for the log entry.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the log message.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets the message template used for the log.
    /// </summary>
    public string MessageTemplate { get; set; }

    /// <summary>
    /// Gets or sets the log level (e.g., Information, Error).
    /// </summary>
    public LogLevel? Level { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the log entry.
    /// </summary>
    public DateTimeOffset TimeStamp { get; set; }

    /// <summary>
    /// Gets or sets the exception details, if any.
    /// </summary>
    public string Exception { get; set; }

    /// <summary>
    /// Gets or sets the additional properties as a dictionary.
    /// </summary>
    public IDictionary<string, object> LogEvents { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets the trace identifier for distributed tracing.
    /// </summary>
    public string TraceId { get; set; }

    /// <summary>
    /// Gets or sets the span identifier for distributed tracing.
    /// </summary>
    public string SpanId { get; set; }

    /// <summary>
    /// Gets or sets the correlation identifier for tracking related operations.
    /// </summary>
    public string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the custom log key for categorizing logs.
    /// </summary>
    public string LogKey { get; set; }

    /// <summary>
    /// Gets or sets the module name associated with the log entry.
    /// </summary>
    public string ModuleName { get; set; }

    /// <summary>
    /// Gets or sets the thread ID associated with the log entry.
    /// </summary>
    public string ThreadId { get; set; }

    /// <summary>
    /// Gets or sets the short type name associated with the log entry.
    /// </summary>
    public string ShortTypeName { get; set; }
}

/// <summary>
/// Data transfer object for representing log statistics, including counts by level and time intervals.
/// </summary>
public class LogStatisticsModel
{
    /// <summary>
    /// Gets or sets the counts of log entries by level (e.g., Information, Error).
    /// </summary>
    public Dictionary<LogLevel, int> LevelCounts { get; set; } = [];

    /// <summary>
    /// Gets or sets the counts of log entries by time interval and level.
    /// Keys are the start times of intervals, values are dictionaries of level counts.
    /// </summary>
    public Dictionary<DateTimeOffset, Dictionary<LogLevel, int>> TimeIntervalCounts { get; set; } = [];
}

/// <summary>
/// Enum representing the supported formats for exporting log entries.
/// </summary>
public enum LogExportFormat
{
    /// <summary>
    /// Comma-separated values format.
    /// </summary>
    Csv,

    /// <summary>
    /// JSON format.
    /// </summary>
    Json,

    /// <summary>
    /// Plain text format.
    /// </summary>
    Txt
}