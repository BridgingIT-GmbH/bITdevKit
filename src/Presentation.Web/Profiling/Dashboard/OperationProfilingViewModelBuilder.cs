// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Profiling.Dashboard;

using System.Globalization;
using BridgingIT.DevKit.Common;

/// <summary>Supplies an elapsed segment value and the operation duration used as its percentage denominator.</summary>
/// <param name="Duration">The observed segment duration.</param>
/// <param name="OperationDuration">The owning operation duration.</param>
/// <example><code>var timing = new OperationProfilingTiming(segment.Statistics.TotalDuration, record.Duration);</code></example>
public sealed record OperationProfilingTiming(TimeSpan Duration, TimeSpan OperationDuration);

/// <summary>Contains one retained-history view and its node-local freshness observation.</summary>
/// <example><code>var count = model.Groups?.TotalOperationCount;</code></example>
public sealed record OperationProfilingDashboardModel
{
    /// <summary>Gets the selector used for this view.</summary>
    /// <example><code>var selection = model.Query;</code></example>
    public OperationProfilingQuery Query { get; init; } = new();
    /// <summary>Gets ranked groups with complete retained counts.</summary>
    /// <example><code>var groups = model.Groups;</code></example>
    public OperationProfilingGroupPage Groups { get; init; }
    /// <summary>Gets an exactly looked-up occurrence, or null when not retained.</summary>
    /// <example><code>var record = model.Record;</code></example>
    public OperationProfilingRecord Record { get; init; }
    /// <summary>Gets independent Runtime coverage.</summary>
    /// <example><code>var coverage = model.Overlay;</code></example>
    public OperationProfilingRuntimeOverlay Overlay { get; init; }
    /// <summary>Gets the serving node's capture and persistence counters.</summary>
    /// <example><code>var node = model.Health?.CountersNodeId;</code></example>
    public OperationProfilingHealth Health { get; init; }
    /// <summary>Gets this view's refresh UTC, distinct from publication UTC.</summary>
    /// <example><code>var refreshed = model.RefreshedUtc;</code></example>
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset RefreshedUtc { get; init; }
    /// <summary>Gets whether the Requests adapter fixes HTTP kind.</summary>
    /// <example><code>var requests = model.RequestsOnly;</code></example>
    public bool RequestsOnly { get; init; }
    /// <summary>Gets a safe unavailable or validation message for HTML rendering.</summary>
    /// <example><code>var error = model.Error;</code></example>
    public string Error { get; init; }
    /// <summary>Gets the consistent HTTP status for an unavailable view.</summary>
    /// <example><code>context.Response.StatusCode = model.StatusCode;</code></example>
    public int StatusCode { get; init; } = 200;
    /// <summary>Gets an optional overlay-specific limitation while retaining a valid operation detail.</summary>
    /// <example><code>var error = model.OverlayError;</code></example>
    public string OverlayError { get; init; }
}

/// <summary>Builds initial, content and detail views through the same DI query service without reading queues or EF.</summary>
/// <example><code>var model = await builder.BuildListAsync(query, requestsOnly: true, token);</code></example>
public sealed class OperationProfilingViewModelBuilder(IOperationProfilingQueryService queries = null, TimeProvider clock = null)
{
    /// <summary>Builds ranked groups and local freshness under one admission and deadline.</summary>
    /// <example><code>var model = await builder.BuildListAsync(new(), false, token);</code></example>
    public async Task<OperationProfilingDashboardModel> BuildListAsync(OperationProfilingQuery query, bool requestsOnly, CancellationToken cancellationToken = default)
    {
        if (requestsOnly) { query = query with { Kind = OperationProfilingKind.HttpRequest.ToString() }; }

        if (queries is null) { return this.Unavailable(query, requestsOnly); }

        var result = await queries.BuildViewAsync<OperationProfilingDashboardModel>(async (session, token) =>
        {
            var groups = await session.GroupAsync(query, token).ConfigureAwait(false);
            return groups.IsFailure ? Result<OperationProfilingDashboardModel>.Failure(groups)
                : Result<OperationProfilingDashboardModel>.Success(new() { Query = query, RequestsOnly = requestsOnly, Groups = groups.Value, Health = session.GetHealth(), RefreshedUtc = this.Now() });
        }, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? result.Value : new() { Query = query, RequestsOnly = requestsOnly, Error = Error(result), StatusCode = ErrorStatus(result), Health = queries.GetHealth(), RefreshedUtc = this.Now() };
    }

    /// <summary>Looks up one occurrence independently of list filters and joins bounded non-owning Runtime evidence.</summary>
    /// <example><code>var model = await builder.BuildDetailAsync(id, false, token);</code></example>
    public async Task<OperationProfilingDashboardModel> BuildDetailAsync(Guid id, bool requestsOnly, CancellationToken cancellationToken = default)
    {
        if (queries is null) { return this.Unavailable(new() { Id = id }, requestsOnly); }

        var result = await queries.BuildViewAsync<OperationProfilingDashboardModel>(async (session, token) =>
        {
            var record = await session.FindAsync(id, token).ConfigureAwait(false);
            if (record.IsFailure) { return Result<OperationProfilingDashboardModel>.Failure(record); }

            var selected = record.Value;
            if (requestsOnly && selected is not null && !ProfilingKeyComparer.Instance.Equals(selected.Kind, OperationProfilingKind.HttpRequest.ToString())) { selected = null; }

            var overlay = selected is null ? null : await session.GetRuntimeOverlayAsync(id, token).ConfigureAwait(false);
            return Result<OperationProfilingDashboardModel>.Success(new()
            {
                Query = new() { Id = id }, RequestsOnly = requestsOnly, Record = selected, Health = session.GetHealth(), RefreshedUtc = this.Now(),
                Overlay = overlay?.IsSuccess == true ? overlay.Value : null, OverlayError = overlay?.IsFailure == true ? Error(overlay) : null,
            });
        }, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? result.Value : new() { Query = new() { Id = id }, RequestsOnly = requestsOnly, Error = Error(result), StatusCode = ErrorStatus(result), Health = queries.GetHealth(), RefreshedUtc = this.Now() };
    }

    /// <summary>Formats UTC timestamps without converting to machine or browser local time.</summary>
    /// <example><code>var label = OperationProfilingViewModelBuilder.Utc(record.CompletedUtc);</code></example>
    public static string Utc(DateTimeOffset value) => value == default ? "Unavailable" : value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
    /// <summary>Formats observed elapsed time in milliseconds or seconds.</summary>
    /// <example><code>var label = OperationProfilingViewModelBuilder.Duration(record.Duration);</code></example>
    public static string Duration(TimeSpan value) => value.TotalMilliseconds < 1000 ? value.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture) + " ms" : value.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture) + " s";
    /// <summary>Formats a segment duration as a percentage of its owner, without clamping repeated or parallel totals.</summary>
    /// <example><code>var label = OperationProfilingViewModelBuilder.Percentage(segment.Statistics.TotalDuration, record.Duration);</code></example>
    public static string Percentage(TimeSpan value, TimeSpan operationDuration) => operationDuration <= TimeSpan.Zero ? "—" : ((double)value.Ticks / operationDuration.Ticks * 100).ToString("0.##", CultureInfo.InvariantCulture) + "%";
    /// <summary>Describes partial, truncated or skewed capture independently of business outcome.</summary>
    /// <example><code>var quality = OperationProfilingViewModelBuilder.Quality(record);</code></example>
    public static string Quality(OperationProfilingRecord record) => string.Join(", ", new[]
    {
        record.Quality.Truncated ? "Truncated" : null, record.Quality.PartialCoverage || record.Quality.IncompleteSegmentCount > 0 ? "Partial" : null,
        record.Quality.ClockDiscontinuity ? "Clock discontinuity" : null, record.Quality.ActualCompletionUnobserved ? "Completion unobserved" : null,
    }.Where(value => value is not null));
    /// <summary>Returns a safe query error category for display without exposing provider details.</summary>
    /// <example><code>var label = OperationProfilingViewModelBuilder.Error(result);</code></example>
    public static string Error(BridgingIT.DevKit.Common.IResult result) => result.Errors.Any(error => error is ProfilingBusyError) ? "Busy: profiling query capacity is occupied. Previous data is stale."
        : result.Errors.Any(error => error is ProfilingQueryTimeoutError) ? "Query deadline exceeded. Narrow the selection; previous data is stale."
        : result.Errors.Any(error => error is ProfilingQueryBoundaryError) ? "The paging boundary expired or was invalidated. Refresh the selection."
        : result.Errors.Any(error => error is ProfilingQueryLimitError) ? "Exact analysis limit exceeded. Narrow the selection."
        : result.Errors.Any(error => error is ProfilingValidationError or ProfilingInvalidKeyError) ? "Invalid bounded profiling selector."
        : "Profiling history is unavailable.";

    /// <summary>Maps safe typed query failures consistently for HTML and dashboard JSON.</summary>
    /// <example><code>var status = OperationProfilingViewModelBuilder.ErrorStatus(result);</code></example>
    public static int ErrorStatus(BridgingIT.DevKit.Common.IResult result) => result.Errors.Any(error => error is ProfilingBusyError) ? 429
        : result.Errors.Any(error => error is ProfilingQueryTimeoutError) ? 504
        : result.Errors.Any(error => error is ProfilingQueryBoundaryError) ? 409
        : result.Errors.Any(error => error is ProfilingQueryLimitError) ? 422
        : result.Errors.Any(error => error is ProfilingDisabledError or ProfilingUnavailableError) ? 503 : 400;

    /// <summary>Explains non-owning Runtime coverage without exposing internal reason tokens in the page.</summary>
    /// <example><code>var label = OperationProfilingViewModelBuilder.RuntimeCoverage(model.Overlay?.Limitation);</code></example>
    public static string RuntimeCoverage(string limitation) => limitation switch
    {
        "OperationClockDiscontinuity" => "Operation UTC clock changed; time correlation is unavailable",
        "CorrelationUnavailable" => "The selected Runtime provider does not support bounded correlation",
        "NoMatchingRuntimeEvidence" => "No matching Runtime samples were retained",
        "SurroundingSamplesOnly" => "Only surrounding Runtime samples are available",
        "PartialRuntimeCoverage" => "Runtime sampling has gaps in this interval",
        "ProcessMetricsAreNotOperationMetrics" => "Matching process samples are shown as context",
        _ => "Runtime coverage is unavailable",
    };

    private DateTimeOffset Now() { try { return (clock ?? TimeProvider.System).GetUtcNow().ToUniversalTime(); } catch (Exception) { return DateTimeOffset.UtcNow; } }
    private OperationProfilingDashboardModel Unavailable(OperationProfilingQuery query, bool requestsOnly) => new() { Query = query, RequestsOnly = requestsOnly, Error = "Profiling is not registered in this host.", StatusCode = 503, RefreshedUtc = this.Now() };
}
