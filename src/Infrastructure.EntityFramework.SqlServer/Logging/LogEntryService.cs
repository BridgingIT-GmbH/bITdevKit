// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Utilities;
using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Implements querying and management of log entries using a generic EF Core DbContext.
/// Supports paged queries, streaming, background purging, statistics, and exporting.
/// </summary>
/// <typeparam name="TContext">The DbContext type, which must implement <see cref="ILoggingContext"/>.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="LogEntryService{TContext}"/> class.
/// </remarks>
public class LogEntryService<TContext>(
    ILogger<LogEntryService<TContext>> logger,
    TContext dbContext,
    LogEntryMaintenanceQueue purgeQueue) : ILogEntryService
    where TContext : DbContext, ILoggingContext
{
    private readonly TContext dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    private readonly ILogger<LogEntryService<TContext>> logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly LogEntryMaintenanceQueue purgeQueue = purgeQueue ?? throw new ArgumentNullException(nameof(purgeQueue));
    private static readonly string[] LogLevels = ["Verbose", "Debug", "Information", "Warning", "Error", "Fatal"];

    /// <inheritdoc/>
    public async Task<LogEntryQueryResponse> QueryAsync(LogEntryQueryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        request.Validate();

        this.logger.LogTrace("{LogKey}: starting query with filters: StartTime={StartTime}, EndTime={EndTime}, Age={Age}, Level={Level}, TraceId={TraceId}, CorrelationId={CorrelationId}, LogKey={LogKey}, SearchText={SearchText}, PageSize={PageSize}, ContinuationToken={ContinuationToken}", "LOG", request.StartTime, request.EndTime, request.Age, request.Level, request.TraceId, request.CorrelationId, request.LogKey, request.SearchText, request.PageSize, request.ContinuationToken);

        var pageSize = Math.Max(1, Math.Min(request.PageSize, 10000)); // Cap at 10,000 for safety
        long? lastId = null;
        if (!string.IsNullOrEmpty(request.ContinuationToken) && long.TryParse(request.ContinuationToken, out var parsedId))
        {
            lastId = parsedId;
        }

        var query = this.dbContext.LogEntries.AsNoTracking()
            .Where(e => e.IsArchived == null || e.IsArchived == false); // Only active logs (NULL or false)

        // Handle StartTime or Age
        var effectiveStartTime = request.StartTime;
        if (request.Age.HasValue)
        {
            effectiveStartTime = request.Age.Value == TimeSpan.Zero ? DateTimeOffset.UtcNow.Date : DateTimeOffset.UtcNow.Date - request.Age.Value;
        }
        else if (!request.StartTime.HasValue && !request.Age.HasValue)
        {
            effectiveStartTime = DateTimeOffset.UtcNow.Date; // Start of current day
        }

        if (effectiveStartTime.HasValue)
        {
            query = query.Where(e => e.TimeStamp >= effectiveStartTime.Value);
        }

        if (request.EndTime.HasValue)
        {
            query = query.Where(e => e.TimeStamp <= request.EndTime.Value);
        }

        if (request.Level.HasValue)
        {
            var levelIndex = (int)request.Level.Value;
            var allowedLevels = LogLevels.Skip(levelIndex).ToArray();

            query = query.Where(e => allowedLevels.Contains(e.Level));
        }

        if (!string.IsNullOrEmpty(request.TraceId))
        {
            query = query.Where(e => e.TraceId == request.TraceId);
        }

        if (!string.IsNullOrEmpty(request.CorrelationId))
        {
            query = query.Where(e => e.CorrelationId == request.CorrelationId);
        }

        if (!string.IsNullOrEmpty(request.LogKey))
        {
            query = query.Where(e => e.LogKey == request.LogKey);
        }

        if (!string.IsNullOrEmpty(request.ModuleName))
        {
            query = query.Where(e => e.ModuleName == request.ModuleName);
        }

        //if (!string.IsNullOrEmpty(request.ThreadId))
        //{
        //    query = query.Where(e => e.ThreadId == request.ThreadId);
        //}

        if (!string.IsNullOrEmpty(request.ShortTypeName))
        {
            query = query.Where(e => e.ShortTypeName == request.ShortTypeName);
        }

        if (!string.IsNullOrEmpty(request.SearchText))
        {
            string searchPattern;
            switch (this.dbContext.Database.ProviderName)
            {
                case "Microsoft.EntityFrameworkCore.SqlServer":
                    searchPattern = $"%{request.SearchText}%";
                    query = query.Where(e =>
                        EF.Functions.Like(e.Message ?? "", searchPattern) ||
                        e.CorrelationId == searchPattern ||
                        EF.Functions.Like(e.Exception ?? "", searchPattern) ||
                        EF.Functions.Like(e.LogEventsJson ?? "", searchPattern));
                    break;
                //case "Npgsql.EntityFrameworkCore.PostgreSQL":
                //    query = query.Where(e =>
                //        EF.Functions.ToTsVector("english", e.Message ?? "").Matches(EF.Functions.ToTsQuery("english", request.SearchText)) ||
                //        EF.Functions.ToTsVector("english", e.Exception ?? "").Matches(EF.Functions.ToTsQuery("english", request.SearchText)) ||
                //        EF.Functions.ToTsVector("english", e.PropertiesJson ?? "").Matches(EF.Functions.ToTsQuery("english", request.SearchText)));
                //    break;
                //case "Microsoft.EntityFrameworkCore.Sqlite":
                default:
                    searchPattern = $"%{request.SearchText}%";
                    query = query.Where(e =>
                        EF.Functions.Like(e.Message ?? "", searchPattern) ||
                        e.CorrelationId == searchPattern ||
                        EF.Functions.Like(e.Exception ?? "", searchPattern) ||
                        EF.Functions.Like(e.LogEventsJson ?? "", searchPattern));
                    break;
            }
        }

        if (lastId.HasValue)
        {
            query = query.Where(e => e.Id < lastId.Value); // Reverse: fetch entries with Id < lastId
        }

        query = query.OrderByDescending(e => e.Id); // Reverse: newest first

        var items = await query
            .Take(pageSize + 1).ToListAsync(cancellationToken);

        var hasMore = items.Count > pageSize;
        if (hasMore)
        {
            items = [.. items.Take(pageSize)];
        }

        var dtos = items.ConvertAll(e => new LogEntryModel
        {
            Id = e.Id,
            Message = e.Message,
            MessageTemplate = e.MessageTemplate,
            Level = e.Level,
            TimeStamp = e.TimeStamp,
            Exception = e.Exception ?? "",
            TraceId = e.TraceId ?? "",
            SpanId = e.SpanId ?? "",
            CorrelationId = e.CorrelationId ?? "",
            LogKey = e.LogKey ?? "",
            ModuleName = e.ModuleName,
            ThreadId = e.ThreadId,
            ShortTypeName = e.ShortTypeName,
            LogEvents = e.LogEvents,
        });

        var continuationToken = hasMore && dtos.Count != 0 ? dtos[^1].Id.ToString() : null; // Reverse: use last (smallest) Id

        this.logger.LogTrace("{LogKey}: query completed with {ItemCount} items", "LOG", dtos.Count);

        return new LogEntryQueryResponse
        {
            Items = dtos,
            ContinuationToken = continuationToken,
            PageSize = dtos.Count
        };
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<LogEntryModel> StreamAsync(
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
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        this.logger.LogTrace("{LogKey}: starting log stream with filters: StartTime={StartTime}, Level={Level}, TraceId={TraceId}, CorrelationId={CorrelationId}, LogKey={LogKey}, SearchText={SearchText}, PollingInterval={PollingInterval}", "LOG", startTime, level, traceId, correlationId, logKey, searchText, pollingInterval);

        long? lastId = null;
        var interval = pollingInterval ?? TimeSpan.FromSeconds(1);

        while (!cancellationToken.IsCancellationRequested)
        {
            var query = this.dbContext.LogEntries.AsNoTracking()
                .Where(e => e.IsArchived == null || e.IsArchived == false); // Only active logs (NULL or false)

            // Handle StartTime or default to current day
            var effectiveStartTime = startTime;
            if (!startTime.HasValue)
            {
                effectiveStartTime = DateTimeOffset.UtcNow.Date; // Start of current day
            }

            if (effectiveStartTime.HasValue)
            {
                query = query.Where(e => e.TimeStamp >= effectiveStartTime.Value);
            }

            if (level.HasValue)
            {
                var levelIndex = (int)level.Value;
                var allowedLevels = LogLevels.Skip(levelIndex).ToArray();
                query = query.Where(e => allowedLevels.Contains(e.Level));
            }

            if (!string.IsNullOrEmpty(traceId))
            {
                query = query.Where(e => e.TraceId == traceId);
            }

            if (!string.IsNullOrEmpty(correlationId))
            {
                query = query.Where(e => e.CorrelationId == correlationId);
            }

            if (!string.IsNullOrEmpty(logKey))
            {
                query = query.Where(e => e.LogKey == logKey);
            }

            if (!string.IsNullOrEmpty(moduleName))
            {
                query = query.Where(e => e.ModuleName == moduleName);
            }

            if (!string.IsNullOrEmpty(threadId))
            {
                query = query.Where(e => e.ThreadId == threadId);
            }

            if (!string.IsNullOrEmpty(shortTypeName))
            {
                query = query.Where(e => e.ShortTypeName == shortTypeName);
            }

            if (!string.IsNullOrEmpty(searchText))
            {
                string searchPattern;
                switch (this.dbContext.Database.ProviderName)
                {
                    case "Microsoft.EntityFrameworkCore.SqlServer":
                        searchPattern = $"%{searchText}%";
                        query = query.Where(e =>
                            EF.Functions.Like(e.Message ?? "", searchPattern) ||
                            e.CorrelationId == searchPattern ||
                            EF.Functions.Like(e.Exception ?? "", searchPattern) ||
                            EF.Functions.Like(e.LogEventsJson ?? "", searchPattern));
                        break;
                    //case "Npgsql.EntityFrameworkCore.PostgreSQL":
                    //    query = query.Where(e =>
                    //        EF.Functions.ToTsVector("english", e.Message ?? "").Matches(EF.Functions.ToTsQuery("english", searchText)) ||
                    //        EF.Functions.ToTsVector("english", e.Exception ?? "").Matches(EF.Functions.ToTsQuery("english", searchText)) ||
                    //        EF.Functions.ToTsVector("english", e.PropertiesJson ?? "").Matches(EF.Functions.ToTsQuery("english", searchText)));
                    //    break;
                    //case "Microsoft.EntityFrameworkCore.Sqlite":
                    default:
                        searchPattern = $"%{searchText}%";
                        query = query.Where(e =>
                            EF.Functions.Like(e.Message ?? "", searchPattern) ||
                            e.CorrelationId == searchPattern ||
                            EF.Functions.Like(e.Exception ?? "", searchPattern) ||
                            EF.Functions.Like(e.LogEventsJson ?? "", searchPattern));
                        break;
                }
            }

            if (lastId.HasValue)
            {
                query = query.Where(e => e.Id < lastId.Value); // Reverse: fetch entries with Id < lastId
            }

            query = query.OrderByDescending(e => e.Id); // Reverse: newest first
            var items = await query.Take(100).ToListAsync(cancellationToken);

            if (items.Count != 0)
            {
                lastId = items.Min(e => e.Id); // Reverse: update lastId to smallest Id in this batch

                foreach (var item in items)
                {
                    yield return new LogEntryModel
                    {
                        Id = item.Id,
                        Message = item.Message,
                        MessageTemplate = item.MessageTemplate,
                        Level = item.Level,
                        TimeStamp = item.TimeStamp,
                        Exception = item.Exception ?? "",
                        TraceId = item.TraceId ?? "",
                        SpanId = item.SpanId ?? "",
                        CorrelationId = item.CorrelationId ?? "",
                        LogKey = item.LogKey ?? "",
                        ModuleName = item.ModuleName,
                        ThreadId = item.ThreadId,
                        ShortTypeName = item.ShortTypeName,
                        LogEvents = item.LogEvents
                    };
                }

                this.logger.LogTrace("{LogKey}: streamed {ItemCount} log entries", "LOG", items.Count);
            }

            await Task.Delay(interval, cancellationToken);
        }

        this.logger.LogTrace("{LogKey}: log stream completed", "LOG");
    }

    /// <inheritdoc/>
    public Task CleanupAsync(
        DateTimeOffset olderThan,
        bool archive = false,
        int batchSize = 1000,
        TimeSpan? delayInterval = null,
        CancellationToken cancellationToken = default)
    {
        this.logger.LogTrace("{LogKey}: queuing maintenance for logs older than {OlderThan} with archive={Archive}, batchSize={BatchSize}, delayInterval={DelayInterval}", "LOG", olderThan, archive, batchSize, delayInterval);

        batchSize = Math.Max(1, batchSize);
        var effectiveDelay = delayInterval ?? TimeSpan.FromMilliseconds(100);

        this.purgeQueue.Enqueue(olderThan, archive, batchSize, effectiveDelay);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task CleanupAsync(
        TimeSpan age,
        bool archive = false,
        int batchSize = 1000,
        TimeSpan? delayInterval = null,
        CancellationToken cancellationToken = default)
    {
        if (age < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(age), "Age cannot be negative.");
        }

        var olderThan = DateTimeOffset.UtcNow.EndOfDay() - age;
        this.logger.LogTrace("{LogKey}: queuing maintenance for logs older than age {Age} (olderThan={OlderThan}) with archive={Archive}, batchSize={BatchSize}, delayInterval={DelayInterval}", "LOG", age, olderThan, archive, batchSize, delayInterval);

        return this.CleanupAsync(olderThan, archive, batchSize, delayInterval, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<LogEntryStatisticsModel> GetStatisticsAsync(
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null,
        TimeSpan? groupByInterval = null,
        CancellationToken cancellationToken = default)
    {
        this.logger.LogTrace("{LogKey}: retrieving statistics with StartTime={StartTime}, EndTime={EndTime}, GroupByInterval={GroupByInterval}", "LOG", startTime, endTime, groupByInterval);

        var query = this.dbContext.LogEntries.AsNoTracking()
            .Where(e => e.IsArchived == null || e.IsArchived == false); // Only active logs (NULL or false)

        // Handle StartTime or default to current day
        var effectiveStartTime = startTime;
        if (!startTime.HasValue)
        {
            effectiveStartTime = DateTimeOffset.UtcNow.Date; // Start of current day
        }

        if (effectiveStartTime.HasValue)
        {
            query = query.Where(e => e.TimeStamp >= effectiveStartTime.Value);
        }

        if (endTime.HasValue)
        {
            query = query.Where(e => e.TimeStamp <= endTime.Value);
        }

        var levelCounts = await query
            .GroupBy(e => e.Level)
            .Select(g => new { Level = g.Key, Count = g.Count() })
            .ToDictionaryAsync(
                e => e.Level != null ? Enum.Parse<LogLevel>(e.Level) : LogLevel.Trace,
                e => e.Count,
                cancellationToken);

        var result = new LogEntryStatisticsModel
        {
            LevelCounts = levelCounts
        };

        if (groupByInterval.HasValue)
        {
            var interval = groupByInterval.Value;
            var timeIntervalCounts = await query
                .GroupBy(e => new
                {
                    TimeBucket = EF.Functions.DateDiffSecond(
                        effectiveStartTime ?? e.TimeStamp,
                        e.TimeStamp) / (int)interval.TotalSeconds * (int)interval.TotalSeconds,
                    e.Level
                })
                .Select(g => new
                {
                    TimeBucketSeconds = g.Key.TimeBucket,
                    g.Key.Level,
                    Count = g.Count()
                }).ToListAsync(cancellationToken);

            var groupedByTime = timeIntervalCounts
                .GroupBy(x => x.TimeBucketSeconds)
                .ToDictionary(
                    g => (effectiveStartTime ?? DateTimeOffset.UtcNow).AddSeconds(g.Key),
                    g => g.ToDictionary(
                        e => e.Level != null ? Enum.Parse<LogLevel>(e.Level) : LogLevel.Trace,
                        e => e.Count));

            result.TimeIntervalCounts = groupedByTime;
        }

        this.logger.LogTrace("{LogKey}: statistics retrieved with {LevelCount} level counts", "LOG", levelCounts.Count);

        return result;
    }

    /// <inheritdoc/>
    public async Task<Stream> ExportAsync(LogEntryQueryRequest request, LogEntryExportFormat format, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        request.Validate();

        this.logger.LogTrace("{LogKey}: starting export with filters: StartTime={StartTime}, EndTime={EndTime}, Age={Age}, Level={Level}, TraceId={TraceId}, CorrelationId={CorrelationId}, LogKey={LogKey}, SearchText={SearchText}, PageSize={PageSize}, Format={Format}", "LOG", request.StartTime, request.EndTime, request.Age, request.Level, request.TraceId, request.CorrelationId, request.LogKey, request.SearchText, request.PageSize, format);

        var stream = new MemoryStream();
        await using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);

        var pageSize = Math.Max(1, Math.Min(request.PageSize, 10000)); // Cap at 10,000
        string continuationToken = null;
        bool hasMore;

        do
        {
            var currentRequest = request.Clone(); // Clone to avoid modifying original request
            currentRequest.PageSize = pageSize;
            currentRequest.ContinuationToken = continuationToken;

            var response = await this.QueryAsync(currentRequest, cancellationToken);
            var logs = response.Items;

            if (logs.Any())
            {
                switch (format)
                {
                    case LogEntryExportFormat.Csv:
                        var buffer = new StringBuilder(512);
                        const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";

                        if (stream.Position == 0) // Write header only once
                        {
                            await writer.WriteLineAsync("Id,TimeStamp,Level,Message,Exception,TraceId,SpanId,CorrelationId,LogKey,Module,ThreadId,Type");
                        }
                        foreach (var log in logs)
                        {
                            buffer.Clear(); // Reuse the same StringBuilder

                            buffer.Append(log.Id);
                            buffer.Append(',');
                            buffer.Append(log.TimeStamp.ToString(TimestampFormat));
                            buffer.Append(',');
                            buffer.Append(log.Level?.ToString() ?? "");
                            buffer.Append(',');
                            AppendCsvField(buffer, log.Message);
                            buffer.Append(',');
                            AppendCsvField(buffer, log.Exception);
                            buffer.Append(',');
                            buffer.Append(log.TraceId ?? "");
                            buffer.Append(',');
                            buffer.Append(log.SpanId ?? "");
                            buffer.Append(',');
                            buffer.Append(log.CorrelationId ?? "");
                            buffer.Append(',');
                            buffer.Append(log.LogKey ?? "");
                            buffer.Append(',');
                            buffer.Append(log.ModuleName ?? "");
                            buffer.Append(',');
                            buffer.Append(log.ThreadId ?? "");
                            buffer.Append(',');
                            buffer.Append(log.ShortTypeName ?? "");

                            await writer.WriteLineAsync(buffer.ToString());
                        }
                        break;
                    case LogEntryExportFormat.Json:
                        if (stream.Position == 0)
                        {
                            await writer.WriteAsync("[");
                        }
                        else
                        {
                            await writer.WriteAsync(",");
                        }

                        logs?.ForEach(e => e.LogEvents.Clear(), cancellationToken: cancellationToken);

                        await JsonSerializer.SerializeAsync(writer.BaseStream, logs, DefaultSystemTextJsonSerializerOptions.Create(), cancellationToken);
                        break;
                    case LogEntryExportFormat.Txt:
                        const string indentSpaces = "                                   "; // 35 spaces
                        var newlineIndent = $"\n{indentSpaces}";

                        foreach (var log in logs)
                        {
                            // Write main log line
                            await writer.WriteAsync('[');
                            await writer.WriteAsync(log.TimeStamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                            await writer.WriteAsync("] ");
                            await writer.WriteAsync(log.Level);
                            await writer.WriteAsync(": ");
                            await writer.WriteLineAsync(log.Message);

                            // Track if we need newline + indent
                            var needsNewlineIndent = true;

                            // Write additional fields directly without string concatenation
                            if (!string.IsNullOrEmpty(log.Exception))
                            {
                                if (needsNewlineIndent)
                                {
                                    await writer.WriteAsync(newlineIndent);
                                    needsNewlineIndent = false;
                                }
                                await writer.WriteAsync(" exception: ");
                                await writer.WriteAsync(log.Exception);
                            }

                            if (!string.IsNullOrEmpty(log.CorrelationId))
                            {
                                if (needsNewlineIndent)
                                {
                                    await writer.WriteAsync(newlineIndent);
                                    needsNewlineIndent = false;
                                }
                                await writer.WriteAsync(" correlationId: ");
                                await writer.WriteAsync(log.CorrelationId);
                            }

                            if (!string.IsNullOrEmpty(log.LogKey))
                            {
                                if (needsNewlineIndent)
                                {
                                    await writer.WriteAsync(newlineIndent);
                                    needsNewlineIndent = false;
                                }
                                await writer.WriteAsync(" logKey: ");
                                await writer.WriteAsync(log.LogKey);
                            }

                            if (!string.IsNullOrEmpty(log.ModuleName))
                            {
                                if (needsNewlineIndent)
                                {
                                    await writer.WriteAsync(newlineIndent);
                                    needsNewlineIndent = false;
                                }
                                await writer.WriteAsync(" module: ");
                                await writer.WriteAsync(log.ModuleName);
                            }

                            if (!string.IsNullOrEmpty(log.ShortTypeName))
                            {
                                if (needsNewlineIndent)
                                {
                                    await writer.WriteAsync(indentSpaces);
                                    needsNewlineIndent = false;
                                }
                                await writer.WriteAsync(" type: ");
                                await writer.WriteAsync(log.ShortTypeName);
                            }

                            await writer.WriteAsync('\n');
                        }
                        break;
                    default:
                        throw new ArgumentException("Unsupported export format", nameof(format));
                }
            }

            continuationToken = response.ContinuationToken;
            hasMore = continuationToken != null;

            await writer.FlushAsync(cancellationToken);
        } while (hasMore);

        if (format == LogEntryExportFormat.Json && stream.Position > 0)
        {
            await writer.WriteAsync("]");
            await writer.FlushAsync(cancellationToken);
        }

        stream.Position = 0;

        this.logger.LogTrace("{LogKey}: export completed in {Format} format", "LOG", format);

        return stream;

        static void AppendCsvField(StringBuilder sb, string field)
        {
            if (string.IsNullOrEmpty(field))
            {
                return;
            }

            if (field.IndexOfAny(['"', ',', '\n', '\r']) >= 0)
            {
                sb.Append('"');
                if (field.Contains('"'))
                {
                    sb.Append(field.Replace("\"", "\"\""));
                }
                else
                {
                    sb.Append(field);
                }
                sb.Append('"');
            }
            else
            {
                sb.Append(field);
            }
        }
    }

    /// <inheritdoc/>
    public async Task SubscribeAsync(Func<LogEntryModel, Task> callback, LogLevel logLevel = LogLevel.Error, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);

        this.logger.LogTrace("{LogKey}: subscribing to {LogLevel} log notifications", "LOG", logLevel);

        await foreach (var log in this.StreamAsync(level: logLevel, cancellationToken: cancellationToken))
        {
            await callback(log);
            this.logger.LogTrace("{LogKey}: notified log notification: Id={Id}, Level={Level}", "LOG", log.Id, log.Level);
        }

        this.logger.LogTrace("{LogKey}: notification subscription ended", "LOG");
    }
}