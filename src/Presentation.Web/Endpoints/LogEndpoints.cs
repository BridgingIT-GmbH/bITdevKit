// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System;
using System.Net;
using System.Text.Json;
using System.Threading;
using BridgingIT.DevKit.Application.Utilities;
using BridgingIT.DevKit.Presentation.Web.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using IResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>
/// Configures HTTP endpoints for querying, streaming, purging, exporting, and retrieving statistics for log entries.
/// </summary>
public class LogEndpoints(LogEndpointsOptions options = null, ILogger<LogEndpoints> logger = null) : EndpointsBase
{
    private readonly LogEndpointsOptions options = options ?? new LogEndpointsOptions();
    private readonly ILogger<LogEndpoints> logger = logger;

    /// <inheritdoc/>
    public override void Map(IEndpointRouteBuilder app)
    {
        if (!this.options.Enabled)
        {
            return;
        }

        var group = this.MapGroup(app, this.options);

        group.MapGet("", this.GetLogs)
            .Produces<LogQueryResponse>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("GetLogs")
            .WithDescription("Retrieves a paged list of log entries with optional filters. Dates must be in ISO 8601 format (e.g., 2025-04-15T00:00:00Z).");

        group.MapGet("stream", this.StreamLogs)
            .Produces<IEnumerable<LogEntryDto>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("StreamLogs")
            .WithDescription("Streams log entries in real-time based on optional filters. Dates must be in ISO 8601 format (e.g., 2025-04-15T00:00:00Z).");

        group.MapDelete("", this.PurgeLogs)
            .Produces<string>((int)HttpStatusCode.Accepted)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("PurgeLogs")
            .WithDescription("Queues a purge operation for log entries older than a specified date or age, with options to archive, set batch size, and delay interval. Date must be in ISO 8601 format (e.g., 2025-04-01T00:00:00Z).");

        group.MapGet("stats", this.GetLogStatistics)
            .Produces<LogStatisticsDto>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("GetLogStatistics")
            .WithDescription("Retrieves aggregated statistics for log entries, grouped by time intervals. Dates must be in ISO 8601 format (e.g., 2025-04-15T00:00:00Z).");

        group.MapGet("export", this.ExportLogs)
            //.Produces("text/csv")
            //.Produces("application/json")
            //.Produces("text/plain")
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("ExportLogs")
            .WithDescription("Exports log entries as a downloadable file in the specified format (csv, json, txt). Dates must be in ISO 8601 format (e.g., 2025-04-15T00:00:00Z).");
    }

    private async Task<IResult> GetLogs(
        [FromServices] ILogQueryService queryService,
        [FromQuery] string startTime,
        [FromQuery] string endTime,
        [FromQuery] double? ageDays,
        [FromQuery] LogLevel? level,
        [FromQuery] string traceId,
        [FromQuery] string correlationId,
        [FromQuery] string logKey,
        [FromQuery] string moduleName,
        [FromQuery] string threadId,
        [FromQuery] string shortTypeName,
        [FromQuery] string searchText,
        [FromQuery] int? pageSize,
        [FromQuery] string continuationToken,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(queryService);

        this.logger?.LogDebug("{LogKey}: Fetching paged logs with filters: startTime={StartTime}, endTime={EndTime}, ageDays={AgeDays}, level={Level}, traceId={TraceId}, correlationId={CorrelationId}, logKey={LogKey}, moduleName={ModuleName}, threadId={ThreadId}, shortTypeName={ShortTypeName}, searchText={SearchText}, pageSize={PageSize}, continuationToken={ContinuationToken}", "Log", startTime, endTime, ageDays, level, traceId, correlationId, logKey, moduleName, threadId, shortTypeName, searchText, pageSize, continuationToken);

        try
        {
            if (!string.IsNullOrEmpty(startTime) && ageDays.HasValue)
            {
                return Results.Problem(new ProblemDetails
                {
                    Status = (int)HttpStatusCode.BadRequest,
                    Title = "Invalid Query Parameters",
                    Detail = "Specify either startTime or ageDays, not both."
                });
            }

            DateTimeOffset? parsedStartTime = null;
            if (!string.IsNullOrEmpty(startTime) && !DateTimeOffset.TryParse(startTime, out var startTimeValue))
            {
                return Results.Problem(new ProblemDetails
                {
                    Status = (int)HttpStatusCode.BadRequest,
                    Title = "Invalid Date Format",
                    Detail = "Invalid startTime format. Use ISO 8601 (e.g., 2025-04-15T00:00:00Z)."
                });
            }

            DateTimeOffset? parsedEndTime = null;
            if (!string.IsNullOrEmpty(endTime) && !DateTimeOffset.TryParse(endTime, out var endTimeValue))
            {
                return Results.Problem(new ProblemDetails
                {
                    Status = (int)HttpStatusCode.BadRequest,
                    Title = "Invalid Date Format",
                    Detail = "Invalid endTime format. Use ISO 8601 (e.g., 2025-04-15T00:00:00Z)."
                });
            }

            TimeSpan? age = null;
            if (ageDays.HasValue)
            {
                if (ageDays.Value < 0)
                {
                    return Results.Problem(new ProblemDetails
                    {
                        Status = (int)HttpStatusCode.BadRequest,
                        Title = "Invalid Age",
                        Detail = "AgeDays cannot be negative."
                    });
                }
                age = ageDays.Value == 0 ? TimeSpan.Zero : TimeSpan.FromDays(ageDays.Value);
            }

            var request = new LogQueryRequest
            {
                StartTime = parsedStartTime,
                EndTime = parsedEndTime,
                Age = age,
                Level = level,
                TraceId = traceId,
                CorrelationId = correlationId,
                LogKey = logKey,
                ModuleName = moduleName,
                ThreadId = threadId,
                ShortTypeName = shortTypeName,
                SearchText = searchText,
                PageSize = pageSize ?? 1000,
                ContinuationToken = continuationToken
            };

            var response = await queryService.QueryLogsAsync(request, cancellationToken);
            this.logger?.LogDebug("{LogKey}: Retrieved {ItemCount} log entries", "Log", response.Items.Count);
            return Results.Ok(response);
        }
        catch (ArgumentException ex)
        {
            this.logger?.LogError(ex, "{LogKey}: Invalid query request", "Log");
            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.BadRequest,
                Title = "Invalid Query Request",
                Detail = $"Invalid request: {ex.Message}"
            });
        }
        catch (Exception ex)
        {
            this.logger?.LogError(ex, "{LogKey}: Error fetching logs", "Log");
            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.InternalServerError,
                Title = "Query Failed",
                Detail = "An error occurred while fetching logs."
            });
        }
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    private async Task<IResult> StreamLogs(
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        [FromServices] ILogQueryService queryService,
        [FromQuery] string startTime,
        [FromQuery] double? ageDays,
        [FromQuery] LogLevel? level,
        [FromQuery] string traceId,
        [FromQuery] string correlationId,
        [FromQuery] string logKey,
        [FromQuery] string moduleName,
        [FromQuery] string threadId,
        [FromQuery] string shortTypeName,
        [FromQuery] string searchText,
        [FromQuery] double? pollingIntervalSeconds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(queryService);

        this.logger?.LogDebug("{LogKey}: Starting log stream with filters: startTime={StartTime}, ageDays={AgeDays}, level={Level}, traceId={TraceId}, correlationId={CorrelationId}, logKey={LogKey}, moduleName={ModuleName}, threadId={ThreadId}, shortTypeName={ShortTypeName}, searchText={SearchText}, pollingIntervalSeconds={PollingIntervalSeconds}", "Log", startTime, ageDays, level, traceId, correlationId, logKey, moduleName, threadId, shortTypeName, searchText, pollingIntervalSeconds);

        try
        {
            if (!string.IsNullOrEmpty(startTime) && ageDays.HasValue)
            {
                return Results.Problem(new ProblemDetails
                {
                    Status = (int)HttpStatusCode.BadRequest,
                    Title = "Invalid Query Parameters",
                    Detail = "Specify either startTime or ageDays, not both."
                });
            }

            DateTimeOffset? parsedStartTime = null;
            if (!string.IsNullOrEmpty(startTime) && !DateTimeOffset.TryParse(startTime, out var startTimeValue))
            {
                return Results.Problem(new ProblemDetails
                {
                    Status = (int)HttpStatusCode.BadRequest,
                    Title = "Invalid Date Format",
                    Detail = "Invalid startTime format. Use ISO 8601 (e.g., 2025-04-15T00:00:00Z)."
                });
            }

            TimeSpan? age = null;
            if (ageDays.HasValue)
            {
                if (ageDays.Value < 0)
                {
                    return Results.Problem(new ProblemDetails
                    {
                        Status = (int)HttpStatusCode.BadRequest,
                        Title = "Invalid Age",
                        Detail = "AgeDays cannot be negative."
                    });
                }
                age = ageDays.Value == 0 ? TimeSpan.Zero : TimeSpan.FromDays(ageDays.Value);
                parsedStartTime = DateTimeOffset.UtcNow - age.Value;
            }

            var pollingInterval = pollingIntervalSeconds.HasValue
                ? TimeSpan.FromSeconds(pollingIntervalSeconds.Value)
                : TimeSpan.FromSeconds(1);

            return Results.Stream(async stream =>
            {
                await foreach (var log in queryService.StreamLogsAsync(
                    parsedStartTime,
                    level,
                    traceId,
                    correlationId,
                    logKey,
                    moduleName,
                    threadId,
                    shortTypeName,
                    searchText,
                    pollingInterval,
                    cancellationToken))
                {
                    this.logger?.LogDebug("{LogKey}: Streaming log entry: Id={Id}, Level={Level}, LogKey={LogKey}", "Log", log.Id, log.Level, log.LogKey);
                    await JsonSerializer.SerializeAsync(stream, log, cancellationToken: cancellationToken);
                    await stream.WriteAsync(Encoding.UTF8.GetBytes("\n"), cancellationToken);
                }
                this.logger?.LogDebug("{LogKey}: Log stream completed", "Log");
            }, contentType: "application/x-ndjson");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this.logger?.LogError(ex, "{LogKey}: Error streaming logs", "Log");
            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.InternalServerError,
                Title = "Stream Failed",
                Detail = "An error occurred while streaming logs."
            });
        }
    }

    private async Task<IResult> PurgeLogs(
        [FromServices] ILogQueryService queryService,
        [FromQuery] string olderThan,
        [FromQuery] double? ageDays,
        [FromQuery] bool archive = false,
        [FromQuery] int? batchSize = 1000,
        [FromQuery] double? delayIntervalMilliseconds = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queryService);

        this.logger?.LogDebug("{LogKey}: Queuing purge with olderThan={OlderThan}, ageDays={AgeDays}, archive={Archive}, batchSize={BatchSize}, delayIntervalMilliseconds={DelayIntervalMilliseconds}", "Log", olderThan, ageDays, archive, batchSize, delayIntervalMilliseconds);

        try
        {
            if (!string.IsNullOrEmpty(olderThan) && ageDays.HasValue)
            {
                return Results.Problem(new ProblemDetails
                {
                    Status = (int)HttpStatusCode.BadRequest,
                    Title = "Invalid Query Parameters",
                    Detail = "Specify either olderThan or ageDays, not both."
                });
            }

            if (batchSize <= 0)
            {
                return Results.Problem(new ProblemDetails
                {
                    Status = (int)HttpStatusCode.BadRequest,
                    Title = "Invalid Batch Size",
                    Detail = "BatchSize must be positive."
                });
            }

            var delayInterval = delayIntervalMilliseconds.HasValue
                ? TimeSpan.FromMilliseconds(delayIntervalMilliseconds.Value)
                : TimeSpan.FromMilliseconds(100);

            if (!string.IsNullOrEmpty(olderThan))
            {
                if (!DateTimeOffset.TryParse(olderThan, out var olderThanValue))
                {
                    return Results.Problem(new ProblemDetails
                    {
                        Status = (int)HttpStatusCode.BadRequest,
                        Title = "Invalid Date Format",
                        Detail = "Invalid olderThan format. Use ISO 8601 (e.g., 2025-04-01T00:00:00Z)."
                    });
                }

                await queryService.PurgeLogsAsync(olderThanValue, archive, batchSize.Value, delayInterval, cancellationToken);
                this.logger?.LogDebug("{LogKey}: Purge queued for logs older than {OlderThan}", "Log", olderThanValue);

                return Results.Accepted($"Purge for logs older than {olderThanValue} queued successfully.");
            }
            else if (ageDays.HasValue)
            {
                if (ageDays < 0)
                {
                    return Results.Problem(new ProblemDetails
                    {
                        Status = (int)HttpStatusCode.BadRequest,
                        Title = "Invalid Age",
                        Detail = "AgeDays cannot be negative."
                    });
                }

                var age = ageDays.Value == 0 ? TimeSpan.Zero : TimeSpan.FromDays(ageDays.Value);
                await queryService.PurgeLogsAsync(age, archive, batchSize.Value, delayInterval, cancellationToken);

                this.logger?.LogDebug("{LogKey}: Purge queued for logs older than {AgeDays} days", "Log", ageDays.Value);
                return Results.Accepted($"Purge for logs older than {age.TotalDays} days queued successfully.");
            }
            else
            {
                return Results.Problem(new ProblemDetails
                {
                    Status = (int)HttpStatusCode.BadRequest,
                    Title = "Missing Parameters",
                    Detail = "Either olderThan or ageDays must be specified."
                });
            }
        }
        catch (InvalidOperationException ex)
        {
            this.logger?.LogError(ex, "{LogKey}: Purge operation failed", "Log");
            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.BadRequest,
                Title = "Purge Failed",
                Detail = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            this.logger?.LogError(ex, "{LogKey}: Invalid purge request", "Log");
            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.BadRequest,
                Title = "Invalid Purge Request",
                Detail = $"Invalid request: {ex.Message}"
            });
        }
        catch (Exception ex)
        {
            this.logger?.LogError(ex, "{LogKey}: Error queuing purge operation", "Log");
            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.InternalServerError,
                Title = "Purge Failed",
                Detail = "An error occurred while queuing the purge operation."
            });
        }
    }

    private async Task<IResult> GetLogStatistics(
        [FromServices] ILogQueryService queryService,
        [FromQuery] string startTime,
        [FromQuery] string endTime,
        [FromQuery] double? groupByIntervalHours,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(queryService);

        this.logger?.LogDebug("{LogKey}: Fetching log statistics: startTime={StartTime}, endTime={EndTime}, groupByIntervalHours={GroupByIntervalHours}", "Log", startTime, endTime, groupByIntervalHours);

        try
        {
            DateTimeOffset? parsedStartTime = null;
            if (!string.IsNullOrEmpty(startTime) && !DateTimeOffset.TryParse(startTime, out var startTimeValue))
            {
                return Results.Problem(new ProblemDetails
                {
                    Status = (int)HttpStatusCode.BadRequest,
                    Title = "Invalid Date Format",
                    Detail = "Invalid startTime format. Use ISO 8601 (e.g., 2025-04-15T00:00:00Z)."
                });
            }

            DateTimeOffset? parsedEndTime = null;
            if (!string.IsNullOrEmpty(endTime) && !DateTimeOffset.TryParse(endTime, out var endTimeValue))
            {
                return Results.Problem(new ProblemDetails
                {
                    Status = (int)HttpStatusCode.BadRequest,
                    Title = "Invalid Date Format",
                    Detail = "Invalid endTime format. Use ISO 8601 (e.g., 2025-04-15T00:00:00Z)."
                });
            }

            var stats = await queryService.GetLogStatisticsAsync(
                parsedStartTime,
                parsedEndTime,
                groupByIntervalHours.HasValue ? TimeSpan.FromHours(groupByIntervalHours.Value) : null,
                cancellationToken);
            this.logger?.LogDebug("{LogKey}: Retrieved statistics with {LevelCount} level counts", "Log", stats.LevelCounts.Count);
            return Results.Ok(stats);
        }
        catch (ArgumentException ex)
        {
            this.logger?.LogError(ex, "{LogKey}: Invalid statistics request", "Log");
            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.BadRequest,
                Title = "Invalid Statistics Request",
                Detail = $"Invalid request: {ex.Message}"
            });
        }
        catch (Exception ex)
        {
            this.logger?.LogError(ex, "{LogKey}: Error fetching log statistics", "Log");
            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.InternalServerError,
                Title = "Statistics Failed",
                Detail = "An error occurred while fetching log statistics."
            });
        }
    }

    private async Task<IResult> ExportLogs(
        [FromServices] ILogQueryService queryService,
        [FromQuery] string startTime,
        [FromQuery] double? ageDays,
        [FromQuery] string endTime,
        [FromQuery] LogLevel? level,
        [FromQuery] string traceId,
        [FromQuery] string correlationId,
        [FromQuery] string logKey,
        [FromQuery] string moduleName,
        [FromQuery] string threadId,
        [FromQuery] string shortTypeName,
        [FromQuery] string searchText,
        [FromQuery] int? pageSize,
        [FromQuery] LogExportFormat format,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(queryService);

        this.logger?.LogDebug("{LogKey}: Exporting logs with filters: startTime={StartTime}, ageDays={AgeDays}, endTime={EndTime}, level={Level}, traceId={TraceId}, correlationId={CorrelationId}, logKey={LogKey}, moduleName={ModuleName}, threadId={ThreadId}, shortTypeName={ShortTypeName}, searchText={SearchText}, pageSize={PageSize}, format={Format}", "Log", startTime, ageDays, endTime, level, traceId, correlationId, logKey, moduleName, threadId, shortTypeName, searchText, pageSize, format);

        try
        {
            if (!string.IsNullOrEmpty(startTime) && ageDays.HasValue)
            {
                return Results.Problem(new ProblemDetails
                {
                    Status = (int)HttpStatusCode.BadRequest,
                    Title = "Invalid Query Parameters",
                    Detail = "Specify either startTime or ageDays, not both."
                });
            }

            DateTimeOffset? parsedStartTime = null;
            if (!string.IsNullOrEmpty(startTime) && !DateTimeOffset.TryParse(startTime, out var startTimeValue))
            {
                return Results.Problem(new ProblemDetails
                {
                    Status = (int)HttpStatusCode.BadRequest,
                    Title = "Invalid Date Format",
                    Detail = "Invalid startTime format. Use ISO 8601 (e.g., 2025-04-15T00:00:00Z)."
                });
            }

            DateTimeOffset? parsedEndTime = null;
            if (!string.IsNullOrEmpty(endTime) && !DateTimeOffset.TryParse(endTime, out var endTimeValue))
            {
                return Results.Problem(new ProblemDetails
                {
                    Status = (int)HttpStatusCode.BadRequest,
                    Title = "Invalid Date Format",
                    Detail = "Invalid endTime format. Use ISO 8601 (e.g., 2025-04-15T00:00:00Z)."
                });
            }

            TimeSpan? age = null;
            if (ageDays.HasValue)
            {
                if (ageDays.Value < 0)
                {
                    return Results.Problem(new ProblemDetails
                    {
                        Status = (int)HttpStatusCode.BadRequest,
                        Title = "Invalid Age",
                        Detail = "AgeDays cannot be negative."
                    });
                }
                age = ageDays.Value == 0 ? TimeSpan.Zero : TimeSpan.FromDays(ageDays.Value);
            }

            var request = new LogQueryRequest
            {
                StartTime = parsedStartTime,
                EndTime = parsedEndTime,
                Age = age,
                Level = level,
                TraceId = traceId,
                CorrelationId = correlationId,
                LogKey = logKey,
                ModuleName = moduleName,
                ThreadId = threadId,
                ShortTypeName = shortTypeName,
                SearchText = searchText,
                PageSize = pageSize ?? 1000
            };

            var stream = await queryService.ExportLogsAsync(request, format, cancellationToken);
            var contentType = format switch
            {
                LogExportFormat.Csv => "text/csv",
                LogExportFormat.Json => "application/json",
                LogExportFormat.Txt => "text/plain",
                _ => "application/octet-stream"
            };
            var fileName = $"logs_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.{format.ToString().ToLower()}";

            this.logger?.LogDebug("{LogKey}: Export completed in {Format} format", "Log", format);
            return Results.File(stream, contentType, fileName, enableRangeProcessing: true);
        }
        catch (ArgumentException ex)
        {
            this.logger?.LogError(ex, "{LogKey}: Invalid export request", "Log");
            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.BadRequest,
                Title = "Invalid Export Request",
                Detail = $"Invalid request: {ex.Message}"
            });
        }
        catch (Exception ex)
        {
            this.logger?.LogError(ex, "{LogKey}: Error exporting logs", "Log");
            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.InternalServerError,
                Title = "Export Failed",
                Detail = "An error occurred while exporting logs."
            });
        }
    }
}