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
using BridgingIT.DevKit.Common;
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
public class LogEntryEndpoints(LogEntryEndpointsOptions options = null, ILogger<LogEntryEndpoints> logger = null) : EndpointsBase
{
    private readonly LogEntryEndpointsOptions options = options ?? new LogEntryEndpointsOptions();
    private readonly ILogger<LogEntryEndpoints> logger = logger;

    /// <inheritdoc/>
    public override void Map(IEndpointRouteBuilder app)
    {
        if (!this.options.Enabled)
        {
            return;
        }

        var group = this.MapGroup(app, this.options);

        group.MapGet("", this.GetLogEntries)
            .Produces<LogEntryQueryResponse>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("GetLogEntries")
            .WithDescription("Retrieves a paged list of log entries with optional filters. Dates must be in ISO 8601 format (e.g., 2025-04-15T00:00:00Z).");

        group.MapGet("stream", this.StreamLogEntries)
            .Produces<IEnumerable<LogEntryModel>>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("StreamLogEntries")
            .WithDescription("Streams log entries in real-time based on optional filters. Dates must be in ISO 8601 format (e.g., 2025-04-15T00:00:00Z).");

        group.MapDelete("", this.CleanupLogEntries)
            .Produces<string>((int)HttpStatusCode.Accepted)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("CleanupLogEntries")
            .WithDescription("Queues a maintenance operation for log entries older than a specified date or age, with options to archive, set batch size, and delay interval. Date must be in ISO 8601 format (e.g., 2025-04-01T00:00:00Z).");

        group.MapGet("stats", this.GetLogEntriesStatistics)
            .Produces<LogEntryStatisticsModel>()
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("GetLogEntriesStatistics")
            .WithDescription("Retrieves aggregated statistics for log entries, grouped by time intervals. Dates must be in ISO 8601 format (e.g., 2025-04-15T00:00:00Z).");

        group.MapGet("export", this.ExportLogEntries)
            //.Produces("text/csv")
            //.Produces("application/json")
            //.Produces("text/plain")
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("ExportLogEntries")
            .WithDescription("Exports log entries as a downloadable file in the specified format (csv, json, txt). Dates must be in ISO 8601 format (e.g., 2025-04-15T00:00:00Z).");
    }

    private async Task<IResult> GetLogEntries(
        [FromServices] ILogEntryService queryService,
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

            var request = new LogEntryQueryRequest
            {
                StartTime = parsedStartTime,
                EndTime = parsedEndTime,
                Age = age,
                Level = level,
                TraceId = traceId,
                CorrelationId = correlationId,
                LogKey = logKey,
                ModuleName = moduleName,
                //ThreadId = threadId,
                ShortTypeName = shortTypeName,
                SearchText = searchText,
                PageSize = pageSize ?? 1000,
                ContinuationToken = continuationToken
            };

            var response = await queryService.QueryAsync(request, cancellationToken);

            return Results.Ok(response);
        }
        catch (ArgumentException ex)
        {
            this.logger?.LogError(ex, "{LogKey}: invalid query request", "LOG");
            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.BadRequest,
                Title = "Invalid Query Request",
                Detail = $"Invalid request: {ex.Message}"
            });
        }
        catch (Exception ex)
        {
            this.logger?.LogError(ex, "{LogKey}: error fetching logs", "LOG");
            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.InternalServerError,
                Title = "Query Failed",
                Detail = "An error occurred while fetching logs."
            });
        }
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    private async Task<IResult> StreamLogEntries(
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        [FromServices] ILogEntryService queryService,
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
                await foreach (var log in queryService.StreamAsync(
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
                    await JsonSerializer.SerializeAsync(stream, log, options: DefaultSystemTextJsonSerializerOptions.Create(), cancellationToken: cancellationToken);
                    await stream.WriteAsync(Encoding.UTF8.GetBytes("\n"), cancellationToken);
                }
            }, contentType: "application/x-ndjson");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this.logger?.LogError(ex, "{LogKey}: error streaming logs", "LOG");
            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.InternalServerError,
                Title = "Stream Failed",
                Detail = "An error occurred while streaming logs."
            });
        }
    }

    private async Task<IResult> CleanupLogEntries(
        [FromServices] ILogEntryService queryService,
        [FromQuery] string olderThan,
        [FromQuery] double? ageDays,
        [FromQuery] bool archive = false,
        [FromQuery] int? batchSize = 1000,
        [FromQuery] double? delayIntervalMilliseconds = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queryService);

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

                await queryService.CleanupAsync(olderThanValue, archive, batchSize.Value, delayInterval, cancellationToken);
                return Results.Accepted($"Maintenance for logs older than {olderThanValue} queued successfully.");
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
                await queryService.CleanupAsync(age, archive, batchSize.Value, delayInterval, cancellationToken);
                return Results.Accepted($"Maintenance for logs older than {age.TotalDays} days queued successfully.");
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
            this.logger?.LogError(ex, "{LogKey}: maintenance operation failed", "LOG");
            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.BadRequest,
                Title = "Purge Failed",
                Detail = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            this.logger?.LogError(ex, "{LogKey}: invalid purge request", "LOG");
            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.BadRequest,
                Title = "Invalid Purge Request",
                Detail = $"Invalid request: {ex.Message}"
            });
        }
        catch (Exception ex)
        {
            this.logger?.LogError(ex, "{LogKey}: error queuing purge operation", "LOG");
            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.InternalServerError,
                Title = "Purge Failed",
                Detail = "An error occurred while queuing the purge operation."
            });
        }
    }

    private async Task<IResult> GetLogEntriesStatistics(
        [FromServices] ILogEntryService queryService,
        [FromQuery] string startTime,
        [FromQuery] string endTime,
        [FromQuery] double? groupByIntervalHours,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(queryService);

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

            var stats = await queryService.GetStatisticsAsync(
                parsedStartTime,
                parsedEndTime,
                groupByIntervalHours.HasValue ? TimeSpan.FromHours(groupByIntervalHours.Value) : null,
                cancellationToken);
            return Results.Ok(stats);
        }
        catch (ArgumentException ex)
        {
            this.logger?.LogError(ex, "{LogKey}: invalid statistics request", "LOG");
            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.BadRequest,
                Title = "Invalid Statistics Request",
                Detail = $"Invalid request: {ex.Message}"
            });
        }
        catch (Exception ex)
        {
            this.logger?.LogError(ex, "{LogKey}: error fetching log statistics", "LOG");
            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.InternalServerError,
                Title = "Statistics Failed",
                Detail = "An error occurred while fetching log statistics."
            });
        }
    }

    private async Task<IResult> ExportLogEntries(
        [FromServices] ILogEntryService queryService,
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
        [FromQuery] LogEntryExportFormat format,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(queryService);

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

            var request = new LogEntryQueryRequest
            {
                StartTime = parsedStartTime,
                EndTime = parsedEndTime,
                Age = age,
                Level = level,
                TraceId = traceId,
                CorrelationId = correlationId,
                LogKey = logKey,
                ModuleName = moduleName,
                //ThreadId = threadId,
                ShortTypeName = shortTypeName,
                SearchText = searchText,
                PageSize = pageSize ?? 1000
            };

            var stream = await queryService.ExportAsync(request, format, cancellationToken);
            var contentType = format switch
            {
                LogEntryExportFormat.Csv => "text/csv",
                LogEntryExportFormat.Json => "application/json",
                LogEntryExportFormat.Txt => "text/plain",
                _ => "application/octet-stream"
            };
            var fileName = $"logs_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.{format.ToString().ToLower()}";

            return Results.File(stream, contentType, fileName, enableRangeProcessing: true);
        }
        catch (ArgumentException ex)
        {
            this.logger?.LogError(ex, "{LogKey}: invalid export request", "LOG");
            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.BadRequest,
                Title = "Invalid Export Request",
                Detail = $"Invalid request: {ex.Message}"
            });
        }
        catch (Exception ex)
        {
            this.logger?.LogError(ex, "{LogKey}: error exporting logs", "LOG");
            return Results.Problem(new ProblemDetails
            {
                Status = (int)HttpStatusCode.InternalServerError,
                Title = "Export Failed",
                Detail = "An error occurred while exporting logs."
            });
        }
    }
}