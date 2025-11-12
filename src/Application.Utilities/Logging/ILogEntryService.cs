// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Utilities;

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

public interface ILogEntryService
{
    /// <summary>
    /// Exports log entries matching the specified request in the given format.
    /// </summary>
    /// <param name="request">The log entry query request.</param>
    /// <param name="format">The export format.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A stream containing the exported log entries.</returns>
    Task<Stream> ExportAsync(LogEntryQueryRequest request, LogEntryExportFormat format, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves log entry statistics for the specified time range and grouping interval.
    /// </summary>
    /// <param name="startTime">The start time for statistics (optional).</param>
    /// <param name="endTime">The end time for statistics (optional).</param>
    /// <param name="groupByInterval">The interval to group statistics by (optional).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A model containing log entry statistics.</returns>
    Task<LogEntryStatisticsModel> GetStatisticsAsync(DateTimeOffset? startTime = null, DateTimeOffset? endTime = null, TimeSpan? groupByInterval = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up log entries older than the specified date, with optional archiving and batching.
    /// </summary>
    /// <param name="olderThan">Delete entries older than this date.</param>
    /// <param name="archive">Whether to archive entries before deletion.</param>
    /// <param name="batchSize">The number of entries to process per batch.</param>
    /// <param name="delayInterval">Delay between batches (optional).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task CleanupAsync(DateTimeOffset olderThan, bool archive = false, int batchSize = 1000, TimeSpan? delayInterval = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up log entries older than the specified age, with optional archiving and batching.
    /// </summary>
    /// <param name="age">Delete entries older than this age.</param>
    /// <param name="archive">Whether to archive entries before deletion.</param>
    /// <param name="batchSize">The number of entries to process per batch.</param>
    /// <param name="delayInterval">Delay between batches (optional).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task CleanupAsync(TimeSpan age, bool archive = false, int batchSize = 1000, TimeSpan? delayInterval = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries log entries based on the specified request.
    /// </summary>
    /// <param name="request">The log entry query request.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A response containing the queried log entries.</returns>
    Task<LogEntryQueryResponse> QueryAsync(LogEntryQueryRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams log entries matching the specified filters as an asynchronous sequence.
    /// </summary>
    /// <param name="startTime">The start time for log entries (optional).</param>
    /// <param name="level">The log level to filter by (optional).</param>
    /// <param name="traceId">The trace identifier to filter by (optional).</param>
    /// <param name="correlationId">The correlation identifier to filter by (optional).</param>
    /// <param name="logKey">The log key to filter by (optional).</param>
    /// <param name="moduleName">The module name to filter by (optional).</param>
    /// <param name="threadId">The thread identifier to filter by (optional).</param>
    /// <param name="shortTypeName">The short type name to filter by (optional).</param>
    /// <param name="searchText">The search text to filter by (optional).</param>
    /// <param name="pollingInterval">The polling interval for streaming (optional).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An asynchronous sequence of log entry models.</returns>
    IAsyncEnumerable<LogEntryModel> StreamAsync(
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

    /// <summary>
    /// Subscribes to log entries with the specified log level and invokes the callback for each entry.
    /// </summary>
    /// <param name="callback">The callback to invoke for each log entry.</param>
    /// <param name="logLevel">The minimum log level to subscribe to.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SubscribeAsync(Func<LogEntryModel, Task> callback, LogLevel logLevel = LogLevel.Error, CancellationToken cancellationToken = default);
}
