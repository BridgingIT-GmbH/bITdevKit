// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Utilities;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

public interface ILogEntryQueryService
{
    Task<Stream> ExportLogEntriesAsync(LogEntryQueryRequest request, LogEntryExportFormat format, CancellationToken cancellationToken = default);

    Task<LogEntryStatisticsModel> GetLogEntriesStatisticsAsync(DateTimeOffset? startTime = null, DateTimeOffset? endTime = null, TimeSpan? groupByInterval = null, CancellationToken cancellationToken = default);

    Task PurgeLogEntriesAsync(DateTimeOffset olderThan, bool archive = false, int batchSize = 1000, TimeSpan? delayInterval = null, CancellationToken cancellationToken = default);

    Task PurgeLogEntriesAsync(TimeSpan age, bool archive = false, int batchSize = 1000, TimeSpan? delayInterval = null, CancellationToken cancellationToken = default);

    Task<LogEntryQueryResponse> QueryLogEntriesAsync(LogEntryQueryRequest request, CancellationToken cancellationToken = default);
    IAsyncEnumerable<LogEntryModel> StreamLogEntriesAsync(
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
