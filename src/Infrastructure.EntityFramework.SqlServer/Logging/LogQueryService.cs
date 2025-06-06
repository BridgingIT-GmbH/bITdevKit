// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using BridgingIT.DevKit.Application.Utilities;
using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Implements querying and management of log entries using a generic EF Core DbContext.
/// Supports paged queries, streaming, background purging, statistics, and exporting.
/// </summary>
/// <typeparam name="TContext">The DbContext type, which must implement <see cref="ILoggingContext"/>.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="LogQueryService{TContext}"/> class.
/// </remarks>
/// <param name="logger">The logger for recording service operations, resolved from DI.</param>
/// <param name="dbContext">The generic database context implementing <see cref="ILoggingContext"/>.</param>
/// <param name="purgeService">The optional service for queuing background purge operations. If null, purging is not supported.</param>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="dbContext"/> or <paramref name="logger"/> is null.</exception>
public class LogQueryService<TContext>(
    ILogger<LogQueryService<TContext>> logger,
    TContext dbContext,
    BackgroundPurgeService<TContext> purgeService = null) : ILogQueryService
    where TContext : DbContext, ILoggingContext
{
    private readonly TContext dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    private readonly ILogger<LogQueryService<TContext>> logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private static readonly string[] LogLevels = ["Verbose", "Debug", "Information", "Warning", "Error", "Fatal"];

    /// <inheritdoc/>
    public async Task<LogQueryResponse> QueryLogsAsync(LogQueryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        request.Validate();

        this.logger.LogDebug("{LogKey}: Starting query with filters: StartTime={StartTime}, EndTime={EndTime}, Age={Age}, Level={Level}, TraceId={TraceId}, CorrelationId={CorrelationId}, LogKey={LogKey}, SearchText={SearchText}, PageSize={PageSize}, ContinuationToken={ContinuationToken}", "Log", request.StartTime, request.EndTime, request.Age, request.Level, request.TraceId, request.CorrelationId, request.LogKey, request.SearchText, request.PageSize, request.ContinuationToken);

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

        if (!string.IsNullOrEmpty(request.ThreadId))
        {
            query = query.Where(e => e.ThreadId == request.ThreadId);
        }

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
                    searchPattern = $"\"{request.SearchText}\"";
                    query = query.Where(e =>
                        EF.Functions.Contains(e.Message, searchPattern) ||
                        EF.Functions.Contains(e.Exception, searchPattern) ||
                        EF.Functions.Contains(e.LogEventsJson, searchPattern));
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
                        EF.Functions.Like(e.Exception ?? "", searchPattern) ||
                        EF.Functions.Like(e.LogEventsJson ?? "", searchPattern));
                    break;
            }
        }

        if (lastId.HasValue)
        {
            query = query.Where(e => e.Id > lastId.Value);
        }

        query = query.OrderBy(e => e.Id);

        var items = await query
            .Take(pageSize + 1).ToListAsync(cancellationToken);

        var hasMore = items.Count > pageSize;
        if (hasMore)
        {
            items = [.. items.Take(pageSize)];
        }

        var dtos = items.ConvertAll(e => new LogEntryDto
        {
            Id = e.Id,
            Message = e.Message,
            MessageTemplate = e.MessageTemplate,
            Level = e.Level != null ? Enum.Parse<LogLevel>(e.Level) : null,
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

        var continuationToken = hasMore && dtos.Count != 0 ? dtos[^1].Id.ToString() : null;

        this.logger.LogDebug("{LogKey}: Query completed with {ItemCount} items", "Log", dtos.Count);

        return new LogQueryResponse
        {
            Items = dtos,
            ContinuationToken = continuationToken,
            PageSize = dtos.Count
        };
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<LogEntryDto> StreamLogsAsync(
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
        this.logger.LogDebug("{LogKey}: Starting log stream with filters: StartTime={StartTime}, Level={Level}, TraceId={TraceId}, CorrelationId={CorrelationId}, LogKey={LogKey}, SearchText={SearchText}, PollingInterval={PollingInterval}", "Log", startTime, level, traceId, correlationId, logKey, searchText, pollingInterval);

        long lastId = 0;
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
                        searchPattern = $"\"{searchText}\"";
                        query = query.Where(e =>
                            EF.Functions.Contains(e.Message, searchPattern) ||
                            EF.Functions.Contains(e.Exception, searchPattern) ||
                            EF.Functions.Contains(e.LogEventsJson, searchPattern));
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
                            EF.Functions.Like(e.Exception ?? "", searchPattern) ||
                            EF.Functions.Like(e.LogEventsJson ?? "", searchPattern));
                        break;
                }
            }

            query = query.OrderBy(e => e.Id);

            var items = await query                .Take(100).ToListAsync(cancellationToken);

            if (items.Count != 0)
            {
                lastId = items.Max(e => e.Id);

                foreach (var item in items)
                {
                    yield return new LogEntryDto
                    {
                        Id = item.Id,
                        Message = item.Message,
                        MessageTemplate = item.MessageTemplate,
                        Level = item.Level != null ? Enum.Parse<LogLevel>(item.Level) : null,
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

                this.logger.LogDebug("{LogKey}: Streamed {ItemCount} log entries", "Log", items.Count);
            }

            await Task.Delay(interval, cancellationToken);
        }

        this.logger.LogDebug("{LogKey}: Log stream completed", "Log");
    }

    /// <inheritdoc/>
    public Task PurgeLogsAsync(
        DateTimeOffset olderThan,
        bool archive = false,
        int batchSize = 1000,
        TimeSpan? delayInterval = null,
        CancellationToken cancellationToken = default)
    {
        if (purgeService == null)
        {
            throw new InvalidOperationException("Cannot purge logs: BackgroundPurgeService is not registered.");
        }

        this.logger.LogDebug("{LogKey}: Queuing purge for logs older than {OlderThan} with archive={Archive}, batchSize={BatchSize}, delayInterval={DelayInterval}", "Log", olderThan, archive, batchSize, delayInterval);

        batchSize = Math.Max(1, batchSize);
        var effectiveDelay = delayInterval ?? TimeSpan.FromMilliseconds(100);

        purgeService.EnqueuePurge(olderThan, archive, batchSize, effectiveDelay);

        this.logger.LogDebug("{LogKey}: Purge queued successfully", "Log");

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task PurgeLogsAsync(
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
        this.logger.LogDebug("{LogKey}: Queuing purge for logs older than age {Age} (olderThan={OlderThan}) with archive={Archive}, batchSize={BatchSize}, delayInterval={DelayInterval}", "Log", age, olderThan, archive, batchSize, delayInterval);

        return this.PurgeLogsAsync(olderThan, archive, batchSize, delayInterval, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<LogStatisticsDto> GetLogStatisticsAsync(
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null,
        TimeSpan? groupByInterval = null,
        CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("{LogKey}: Retrieving statistics with StartTime={StartTime}, EndTime={EndTime}, GroupByInterval={GroupByInterval}", "Log", startTime, endTime, groupByInterval);

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
                x => x.Level != null ? Enum.Parse<LogLevel>(x.Level) : LogLevel.Trace,
                x => x.Count,
                cancellationToken);

        var result = new LogStatisticsDto
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
                })
                .ToListAsync(cancellationToken);

            var groupedByTime = timeIntervalCounts
                .GroupBy(x => x.TimeBucketSeconds)
                .ToDictionary(
                    g => (effectiveStartTime ?? DateTimeOffset.UtcNow).AddSeconds(g.Key),
                    g => g.ToDictionary(
                        x => x.Level != null ? Enum.Parse<LogLevel>(x.Level) : LogLevel.Trace,
                        x => x.Count));

            result.TimeIntervalCounts = groupedByTime;
        }

        this.logger.LogDebug("{LogKey}: Statistics retrieved with {LevelCount} level counts", "Log", levelCounts.Count);

        return result;
    }

    /// <inheritdoc/>
    public async Task<Stream> ExportLogsAsync(LogQueryRequest request, LogExportFormat format, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        request.Validate();

        this.logger.LogDebug("{LogKey}: Starting export with filters: StartTime={StartTime}, EndTime={EndTime}, Age={Age}, Level={Level}, TraceId={TraceId}, CorrelationId={CorrelationId}, LogKey={LogKey}, SearchText={SearchText}, PageSize={PageSize}, Format={Format}", "Log", request.StartTime, request.EndTime, request.Age, request.Level, request.TraceId, request.CorrelationId, request.LogKey, request.SearchText, request.PageSize, format);

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

            var response = await this.QueryLogsAsync(currentRequest, cancellationToken);
            var logs = response.Items;

            if (logs.Any())
            {
                switch (format)
                {
                    case LogExportFormat.Csv:
                        if (stream.Position == 0) // Write header only once
                        {
                            await writer.WriteLineAsync("Id,TimeStamp,Level,Message,MessageTemplate,Exception,TraceId,SpanId,CorrelationId,LogKey,ModuleName,ThreadId,ShortTypeName");
                        }
                        foreach (var log in logs)
                        {
                            //var logEventsJson = JsonSerializer.Serialize(log.LogEvents, DefaultSystemTextJsonSerializerOptions.Create());
                            var escapedFields = new[]
                            {
                                log.Id.ToString(),
                                log.TimeStamp.ToString("o") ?? "",
                                log.Level?.ToString() ?? "",
                                $"\"{log.Message?.Replace("\"", "\"\"") ?? ""}\"",
                                $"\"{log.MessageTemplate?.Replace("\"", "\"\"") ?? ""}\"",
                                $"\"{log.Exception?.Replace("\"", "\"\"") ?? ""}\"",
                                //$"\"{logEventsJson.Replace("\"", "\"\"")}\"",
                                log.TraceId ?? "",
                                log.SpanId ?? "",
                                log.CorrelationId ?? "",
                                log.LogKey ?? "",
                                log.ModuleName ?? "",
                                log.ThreadId ?? "",
                                log.ShortTypeName ?? ""
                            };
                            await writer.WriteLineAsync(string.Join(",", escapedFields));
                        }
                        break;
                    case LogExportFormat.Json:
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
                    case LogExportFormat.Txt:
                        foreach (var log in logs)
                        {
                            await writer.WriteLineAsync($"[{log.TimeStamp:o}] {log.Level}: {log.Message}");
                            if (!string.IsNullOrEmpty(log.Exception))
                            {
                                await writer.WriteLineAsync($"Exception: {log.Exception}");
                            }

                            if (!string.IsNullOrEmpty(log.CorrelationId))
                            {
                                await writer.WriteLineAsync($"CorrelationId: {log.CorrelationId}");
                            }

                            if (!string.IsNullOrEmpty(log.LogKey))
                            {
                                await writer.WriteLineAsync($"LogKey: {log.LogKey}");
                            }

                            if (!string.IsNullOrEmpty(log.ModuleName))
                            {
                                await writer.WriteLineAsync($"ModuleName: {log.ModuleName}");
                            }

                            if (!string.IsNullOrEmpty(log.ThreadId))
                            {
                                await writer.WriteLineAsync($"ThreadId: {log.ThreadId}");
                            }

                            if (!string.IsNullOrEmpty(log.ShortTypeName))
                            {
                                await writer.WriteLineAsync($"ShortTypeName: {log.ShortTypeName}");
                            }

                            //if (log.Properties.Any())
                            //{
                            //    var logEventsJson = JsonSerializer.Serialize(log.Properties, DefaultSystemTextJsonSerializerOptions.Create());
                            //    await writer.WriteLineAsync($"Properties: {logEventsJson}");
                            //}
                            await writer.WriteLineAsync("---");
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

        if (format == LogExportFormat.Json && stream.Position > 0)
        {
            await writer.WriteAsync("]");
            await writer.FlushAsync(cancellationToken);
        }

        stream.Position = 0;

        this.logger.LogDebug("{LogKey}: Export completed in {Format} format", "Log", format);

        return stream;
    }

    /// <inheritdoc/>
    public async Task SubscribeToNotificationsAsync(Func<LogEntryDto, Task> callback, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);

        this.logger.LogDebug("{LogKey}: Subscribing to high-severity log notifications", "Log");

        await foreach (var log in this.StreamLogsAsync(
            level: LogLevel.Error,
            cancellationToken: cancellationToken))
        {
            await callback(log);
            this.logger.LogDebug("{LogKey}: Notified high-severity log: Id={Id}, Level={Level}", "Log", log.Id, log.Level);
        }

        this.logger.LogDebug("{LogKey}: Notification subscription ended", "Log");
    }
}