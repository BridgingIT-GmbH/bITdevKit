// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by the LICENSE file.

namespace BridgingIT.DevKit.Presentation.Web;

using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Exposes retained operation and HTTP request profiling through the runtime's shared query service.</summary>
/// <example><code>services.AddMcpHandler&lt;ProfilingMcpHandler&gt;();</code></example>
public sealed class ProfilingMcpHandler(IServiceProvider services) : IMcpHandler
{
    private static readonly JsonSerializerOptions QueryJson = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };
    private static readonly string[] FilterNames =
    [
        "fromUtc", "toUtc", "kind", "key", "keyContains", "nodeId", "applicationVersion", "correlationId",
        "outcomes", "httpMethod", "httpStatusCode", "hasSegmentFailures", "dimensions", "segmentPath",
        "segmentDimensions", "segmentOutcomes", "groupingDimensions"
    ];

    /// <inheritdoc />
    public IReadOnlyCollection<McpCapability> Capabilities { get; } =
    [
        new("profiling.query", McpToolset.Diagnostics, "profiling", "Queries retained operation and HTTP request recordings.") { ArgumentSchema = ProfilingMcpSchemas.Query() },
        new("profiling.details", McpToolset.Diagnostics, "profiling", "Inspects one retained execution and its aggregate segments.") { ArgumentSchema = ProfilingMcpSchemas.Details() },
        new("profiling.insights", McpToolset.Diagnostics, "profiling", "Ranks exact retained operation hotspots.") { ArgumentSchema = ProfilingMcpSchemas.Insights() },
        new("profiling.compare", McpToolset.Diagnostics, "profiling", "Compares independent selections of the same logical operation.") { ArgumentSchema = ProfilingMcpSchemas.Compare() }
    ];

    /// <inheritdoc />
    public async ValueTask<McpResponse> HandleAsync(McpRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var query = services.GetService<IOperationProfilingQueryService>();
            if (query is null) { return Missing(); }

            return request.Operation switch
            {
                "profiling.query" => await this.QueryAsync(query, request.Arguments, cancellationToken).ConfigureAwait(false),
                "profiling.details" => await DetailsAsync(query, request.Arguments, cancellationToken).ConfigureAwait(false),
                "profiling.insights" => await this.InsightsAsync(query, request.Arguments, cancellationToken).ConfigureAwait(false),
                "profiling.compare" => await this.CompareAsync(query, request.Arguments, cancellationToken).ConfigureAwait(false),
                _ => McpResponse.Unavailable(McpErrorCode.FeatureUnavailable, "This profiling operation is not supported.")
            };
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or OverflowException)
        {
            return InvalidArguments();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            return McpResponse.Unavailable("profiling_unavailable", "Profiling evidence could not be read.", "The runtime query service is unavailable. Retry later.");
        }
    }

    /// <summary>Returns bounded profiling evidence for a correlation investigation, independently of log availability.</summary>
    /// <example><code>var evidence = await handler.InspectCorrelationAsync("abc123", arguments, token);</code></example>
    public async Task<McpResponse> InspectCorrelationAsync(string correlationId, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        var filters = JsonSerializer.SerializeToElement(new
        {
            correlationId,
            fromUtc = McpArgumentReader.GetString(arguments, "startTime"),
            toUtc = McpArgumentReader.GetString(arguments, "endTime"),
            take = Math.Clamp(McpArgumentReader.GetInt32(arguments, "profilingTake", 25) ?? 25, 1, 100),
            cursor = McpArgumentReader.GetString(arguments, "profilingCursor")
        });
        return await this.HandleAsync(new("profiling.query", McpToolset.Diagnostics, filters), cancellationToken).ConfigureAwait(false);
    }

    private async Task<McpResponse> QueryAsync(IOperationProfilingQueryService queries, JsonElement arguments, CancellationToken token)
    {
        ValidateNames(arguments, [.. FilterNames, "view", "take", "cursor", "grouped"]);
        var query = this.ReadQuery(arguments);
        var grouped = ReadBoolean(arguments, "grouped");
        if (grouped)
        {
            var result = await queries.GroupAsync(query with { GroupByHttpMethod = true }, token).ConfigureAwait(false);
            if (result.IsFailure) { return Failure(result); }

            var page = result.Value;
            return McpResponse.Success($"Returned {page.Groups.Count} profiling groups.", new
            {
                page.TotalOperationCount, page.TotalGroupCount, page.Boundary, page.NextCursor, page.HasMore, page.StatisticsLimitation,
                groups = page.Groups.Select(group => new
                {
                    group.Key, group.Kind, group.HttpMethod, group.Count, group.Dimensions,
                    totalMilliseconds = group.DurationUnavailable ? (double?)null : group.TotalDuration.TotalMilliseconds,
                    group.DurationUnavailable,
                    representative = Summary(group.Representative)
                }).ToArray(),
                health = queries.GetHealth(),
                healthScope = "Capture counters belong to the serving node and describe its process lifetime, not this selection."
            }, truncated: page.HasMore);
        }

        var response = await queries.QueryAsync(query, token).ConfigureAwait(false);
        if (response.IsFailure) { return Failure(response); }

        var value = response.Value;
        return McpResponse.Success($"Returned {value.Records.Count} retained executions.", new
        {
            value.TotalCount, value.Boundary, value.NextCursor, value.HasMore,
            operations = value.Records.Select(Summary).ToArray(),
            health = queries.GetHealth(),
            healthScope = "Capture counters belong to the serving node and describe its process lifetime, not this selection."
        }, truncated: value.HasMore, next: value.Records.Take(3).Select(record => new McpNextCall("bdk_profiling_details", new { id = record.Id })).ToArray());
    }

    private static async Task<McpResponse> DetailsAsync(IOperationProfilingQueryService queries, JsonElement arguments, CancellationToken token)
    {
        ValidateNames(arguments, ["id", "segmentTake"]);
        var id = ReadString(arguments, "id");
        if (!Guid.TryParse(id, out var parsed) || parsed == Guid.Empty) { return InvalidArguments(); }

        var take = ReadTake(arguments, "segmentTake", 20);
        var result = await queries.FindAsync(parsed, token).ConfigureAwait(false);
        if (result.IsFailure) { return Failure(result); }

        if (result.Value is null) { return McpResponse.Unavailable("profiling_not_found", "The execution is no longer retained or was not recorded."); }

        var record = result.Value;
        return McpResponse.Success("Returned retained execution details.", new
        {
            operation = Summary(record), record.Dimensions, record.Measurements, record.Http, record.Failure,
            record.WallTime,
            segmentCount = record.Segments.Count,
            segments = record.Segments.OrderByDescending(segment => segment.Statistics.TotalSelfDuration).ThenBy(segment => segment.Path.ComparisonKey, StringComparer.Ordinal).Take(take).ToArray()
        }, truncated: record.Segments.Count > take);
    }

    private async Task<McpResponse> InsightsAsync(IOperationProfilingQueryService queries, JsonElement arguments, CancellationToken token)
    {
        ValidateNames(arguments, [.. FilterNames, "take", "rankBy", "segmentTake"]);
        var query = this.ReadQuery(arguments);
        var take = ReadTake(arguments, "take", 25);
        var segmentTake = ReadTake(arguments, "segmentTake", 5);
        var rank = ReadString(arguments, "rankBy") ?? "p95";
        if (rank is not ("p95" or "total" or "count" or "failures")) { return InvalidArguments(); }

        var result = await queries.InsightsAsync(query, token).ConfigureAwait(false);
        if (result.IsFailure) { return Failure(result); }

        var insights = result.Value;
        var ordered = insights.Groups.OrderByDescending(group => rank switch
        {
            "total" => group.CompletedElapsedMilliseconds,
            "count" => group.Count,
            "failures" => group.Count == 0 ? 0 : (double)group.Outcomes.GetValueOrDefault(OperationProfilingOutcome.Failed) / group.Count,
            _ => group.CompletedLatency.DurationUnavailable ? -1 : group.CompletedLatency.P95Milliseconds
        }).ThenBy(group => group.Kind, StringComparer.Ordinal).ThenBy(group => group.Key, StringComparer.Ordinal).ThenBy(group => group.HttpMethod, StringComparer.Ordinal);
        var groups = ordered.Take(take).ToArray();
        return McpResponse.Success($"Ranked {insights.Groups.Count} operation groups from {insights.Count} retained executions.", new
        {
            insights.Count, insights.Outcomes, insights.PartialCount, insights.Boundary,
            groupCount = insights.Groups.Count, rankBy = rank,
            groups = groups.Select(group => new
            {
                group.Key, group.Kind, group.HttpMethod, group.Count, group.Outcomes, group.PartialCount, group.SegmentFailureCount,
                latency = Distribution(group.CompletedLatency, 0),
                totalMilliseconds = group.CompletedElapsedMilliseconds,
                group.RepresentativeId,
                segmentCount = group.Segments.Count,
                segments = Segments(group.Segments, segmentTake),
                segmentsTruncated = group.Segments.Count > segmentTake
            }).ToArray(),
            health = queries.GetHealth(),
            limitation = "Counts describe retained captures. Latency, Total and segment costs use completed executions; Total includes waiting and overlapping roots."
        }, truncated: insights.Groups.Count > take || groups.Any(group => group.Segments.Count > segmentTake));
    }

    private async Task<McpResponse> CompareAsync(IOperationProfilingQueryService queries, JsonElement arguments, CancellationToken token)
    {
        ValidateNames(arguments, ["key", "kind", "httpMethod", "baseline", "candidate", "segmentTake"]);
        var key = ReadString(arguments, "key");
        var kind = ReadString(arguments, "kind");
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(kind)) { return InvalidArguments(); }

        var method = ReadString(arguments, "httpMethod");
        var segmentTake = ReadTake(arguments, "segmentTake", 10);
        var baseline = Side("baseline");
        var candidate = Side("candidate");
        var result = await queries.CompareAsync(baseline, candidate, token).ConfigureAwait(false);
        if (result.IsFailure) { return Failure(result); }

        var comparison = result.Value;
        return McpResponse.Success("Compared retained selections of the same operation.", new
        {
            key, kind, httpMethod = method,
            baseline = Distribution(comparison.Baseline, segmentTake),
            candidate = Distribution(comparison.Candidate, segmentTake),
            comparison.P95ChangePercent,
            limitation = "Independent observed samples; differences do not establish causation."
        }, truncated: comparison.Baseline.Segments.Count > segmentTake || comparison.Candidate.Segments.Count > segmentTake);

        OperationProfilingQuery Side(string name)
        {
            if (!TryProperty(arguments, name, out var side)) { throw new JsonException(); }

            ValidateNames(side, FilterNames.Where(filter => filter is not ("key" or "kind" or "httpMethod" or "groupingDimensions")).ToArray());
            return this.ReadQuery(side, completedOnly: true) with { Key = key, Kind = kind, HttpMethod = method };
        }
    }

    private OperationProfilingQuery ReadQuery(JsonElement arguments, bool completedOnly = false)
    {
        var fields = arguments.EnumerateObject().Where(property => !new[] { "take", "grouped", "rankBy", "segmentTake" }.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(property => property.Name, property => property.Value, StringComparer.OrdinalIgnoreCase);
        var query = JsonSerializer.Deserialize<OperationProfilingQuery>(JsonSerializer.Serialize(fields), QueryJson);
        var limits = services.GetService<ProfilingOptions>()?.Queries;
        return query with
        {
            StartedWithin = true,
            Outcomes = TryProperty(arguments, "outcomes", out _) ? query.Outcomes : completedOnly ? [OperationProfilingOutcome.Completed] : [],
            View = TryProperty(arguments, "view", out _) ? query.View : OperationProfilingView.Recent,
            PageSize = ReadTake(arguments, "take", Math.Min(25, limits?.MaximumPageSize ?? 25)),
            MaximumAnalysisCount = Math.Min(10000, limits?.MaximumAnalysisRecords ?? 10000)
        };
    }

    private static object Summary(OperationProfilingRecord record) => record is null ? null : new
    {
        record.Id, record.Key, record.Kind, record.DisplayName, record.StartedUtc, record.CompletedUtc,
        durationMilliseconds = record.Duration.TotalMilliseconds, outcome = record.Outcome.ToString(), record.CorrelationId, record.ParentOperationId,
        record.NodeId, nodeName = record.Node?.DisplayName ?? record.Node?.HostName, applicationVersion = record.Node?.ApplicationVersion,
        httpMethod = record.Http?.Method, route = record.Http?.Route, httpStatusCode = record.Http?.StatusCode,
        record.Quality, segmentCount = record.Segments.Count,
        hasSegmentFailures = record.Segments.Any(segment => segment.Outcomes.Any(outcome => outcome.Outcome == ProfilingSegmentOutcome.Failed && outcome.Statistics.Count > 0))
    };

    private static object Distribution(OperationProfilingDistribution value, int segmentTake) => new
    {
        value.Count, value.Outcomes, value.PartialOperationCount, value.DurationUnavailable,
        minimumMilliseconds = value.DurationUnavailable ? (double?)null : value.MinimumMilliseconds,
        maximumMilliseconds = value.DurationUnavailable ? (double?)null : value.MaximumMilliseconds,
        meanMilliseconds = value.DurationUnavailable ? (double?)null : value.MeanMilliseconds,
        medianMilliseconds = value.DurationUnavailable ? (double?)null : value.P50Milliseconds,
        p95Milliseconds = value.DurationUnavailable ? (double?)null : value.P95Milliseconds,
        p99Milliseconds = value.DurationUnavailable ? (double?)null : value.P99Milliseconds,
        value.Boundary, value.Sampling, value.Dimensions,
        segmentCount = value.Segments.Count, segments = Segments(value.Segments, segmentTake),
        segmentsTruncated = value.Segments.Count > segmentTake
    };

    private static object[] Segments(IReadOnlyList<OperationProfilingSegmentAnalysis> segments, int take) => segments
        .OrderByDescending(segment => segment.SelfPerOperation.MeanMilliseconds * segment.SelfPerOperation.Count)
        .ThenBy(segment => segment.ComparisonKey, StringComparer.Ordinal).Take(take).Select(segment => (object)new
        {
            segment.Path, segment.OperationCount, segment.InvocationCount, segment.Unavailable, segment.PartialCoverage, segment.Outcomes,
            meanMilliseconds = segment.Unavailable ? (double?)null : segment.InvocationMeanMilliseconds,
            meanSelfMilliseconds = segment.Unavailable ? (double?)null : segment.InvocationSelfMeanMilliseconds,
            totalMilliseconds = segment.Unavailable ? (double?)null : segment.TotalPerOperation.MeanMilliseconds * segment.TotalPerOperation.Count,
            selfMilliseconds = segment.Unavailable ? (double?)null : segment.SelfPerOperation.MeanMilliseconds * segment.SelfPerOperation.Count
        }).ToArray();

    private static void ValidateNames(JsonElement arguments, string[] names)
    {
        if (arguments.ValueKind != JsonValueKind.Object || arguments.EnumerateObject().Any(property => !names.Contains(property.Name, StringComparer.OrdinalIgnoreCase))) { throw new JsonException(); }
    }

    private static bool TryProperty(JsonElement arguments, string name, out JsonElement value)
    {
        value = default;
        if (arguments.ValueKind != JsonValueKind.Object) { return false; }

        foreach (var property in arguments.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) { value = property.Value; return true; }
        }

        return false;
    }

    private static string ReadString(JsonElement arguments, string name)
    {
        if (!TryProperty(arguments, name, out var value) || value.ValueKind == JsonValueKind.Null) { return null; }

        if (value.ValueKind != JsonValueKind.String) { throw new JsonException(); }

        return value.GetString();
    }

    private static int ReadTake(JsonElement arguments, string name, int defaultValue)
    {
        if (!TryProperty(arguments, name, out var value)) { return defaultValue; }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var take) || take is < 1 or > 100) { throw new JsonException(); }

        return take;
    }

    private static bool ReadBoolean(JsonElement arguments, string name)
    {
        if (!TryProperty(arguments, name, out var value)) { return false; }

        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) { throw new JsonException(); }

        return value.GetBoolean();
    }

    private static McpResponse Missing() => McpResponse.Unavailable(McpErrorCode.FeatureUnavailable,
        "Operation profiling queries are not registered in this runtime.", "Register AddProfiling(...).WithOperationProfiling() and enable capture to inspect recordings.");

    private static McpResponse InvalidArguments() => McpResponse.Unavailable("invalid_arguments",
        "Invalid profiling arguments.", "Use the advertised schema, UTC timestamps, typed dimensions and limits from 1 to 100.");

    private static McpResponse Failure(IResult result)
    {
        var code = result.Errors.FirstOrDefault() switch
        {
            ProfilingValidationError => "invalid_arguments",
            ProfilingDisabledError => "profiling_disabled",
            ProfilingBusyError => "profiling_busy",
            ProfilingQueryTimeoutError => McpErrorCode.Timeout,
            ProfilingQueryLimitError => "profiling_limit_exceeded",
            ProfilingQueryBoundaryError => "profiling_boundary_invalid",
            _ => "profiling_unavailable"
        };
        return McpResponse.Unavailable(code, "Profiling evidence could not be read.", code switch
        {
            "profiling_limit_exceeded" => "Narrow the date range or filters; exact analysis is never silently sampled.",
            "profiling_boundary_invalid" => "Restart paging without a cursor; retain the same filters while paging.",
            "profiling_disabled" => "Enable profiling in the selected runtime.",
            "invalid_arguments" => "The shared profiling service rejected the selector. Check dates, typed predicates and configured limits.",
            _ => "The shared query service is busy, unavailable or exceeded its deadline. Retry later."
        });
    }
}
