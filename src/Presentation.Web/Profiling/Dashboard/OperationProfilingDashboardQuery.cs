// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Profiling.Dashboard;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Http;

/// <summary>Translates dashboard controls to the shared bounded query contract without implementing storage predicates.</summary>
/// <example><code>var selection = OperationProfilingDashboardQuery.Read(context.Request.Query);</code></example>
public static class OperationProfilingDashboardQuery
{
    private static readonly JsonSerializerOptions json = new() { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };

    /// <summary>Reads basic controls or a bounded typed JSON selector; Requests always enforces HttpRequest kind.</summary>
    /// <example><code>var selection = OperationProfilingDashboardQuery.Read(context.Request.Query, requestsOnly: true);</code></example>
    public static BridgingIT.DevKit.Common.IResult<OperationProfilingQuery> Read(IQueryCollection input, bool requestsOnly = false)
    {
        try
        {
            if (input.Any(pair => pair.Value.Count > 1 || pair.Value.Sum(value => value?.Length ?? 0) > 16384) || input.Count > 48)
            { throw new ArgumentException("Oversized selector."); }

            var query = new OperationProfilingQuery { PageSize = 25, View = OperationProfilingView.Recent };
            if (Value("query") is string document)
            { query = JsonSerializer.Deserialize<OperationProfilingQuery>(document, json) ?? throw new ArgumentException("Empty selector."); }

            query = query with
            {
                Key = input.ContainsKey("key") ? null : query.Key, KeyContains = String("key", query.KeyContains), Kind = String("kind", query.Kind), Cursor = String("cursor", query.Cursor),
                FromUtc = input.ContainsKey("fromUtc") ? Date("fromUtc") : query.FromUtc, ToUtc = input.ContainsKey("toUtc") ? Date("toUtc") : query.ToUtc,
                NodeId = input.ContainsKey("nodeId") ? Id("nodeId") : query.NodeId, Id = input.ContainsKey("id") ? Id("id") : query.Id, ParentOperationId = input.ContainsKey("parentOperationId") ? Id("parentOperationId") : query.ParentOperationId,
                CorrelationId = String("correlationId", query.CorrelationId), ApplicationVersion = String("applicationVersion", query.ApplicationVersion),
                HttpMethod = String("method", query.HttpMethod), Route = String("route", query.Route),
                HttpStatusCode = Value("status") is string status ? int.Parse(status, CultureInfo.InvariantCulture) : query.HttpStatusCode,
                ApplicationRequestId = String("applicationRequestId", query.ApplicationRequestId),
                SamplingStrategyKey = String("samplingStrategy", query.SamplingStrategyKey), SamplingConfigurationKey = String("samplingConfiguration", query.SamplingConfigurationKey),
                SegmentKey = String("segmentKey", query.SegmentKey),
                SegmentPath = Value("segmentPath") is string path ? new(JsonSerializer.Deserialize<string[]>(path, json)) : query.SegmentPath,
                View = Value("view") is string view ? Enum.Parse<OperationProfilingView>(view, true) : query.View,
                PageSize = Value("limit") is string limit ? int.Parse(limit, CultureInfo.InvariantCulture) : query.PageSize,
                Outcomes = input.ContainsKey("outcomes") ? Outcomes<OperationProfilingOutcome>("outcomes") : query.Outcomes,
                SegmentOutcomes = input.ContainsKey("segmentOutcomes") ? Outcomes<ProfilingSegmentOutcome>("segmentOutcomes") : query.SegmentOutcomes,
                Dimensions = Value("dimensions") is string dimensions ? JsonSerializer.Deserialize<ProfilingDimensionPredicate[]>(dimensions, json) : query.Dimensions,
                SegmentDimensions = Value("segmentDimensions") is string segmentDimensions ? JsonSerializer.Deserialize<ProfilingDimensionPredicate[]>(segmentDimensions, json) : query.SegmentDimensions,
                GroupingDimensions = input.ContainsKey("groupBy") ? (Value("groupBy") ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) : query.GroupingDimensions,
                GroupByHttpMethod = Value("groupByMethod") is string groupMethod ? bool.Parse(groupMethod) : query.GroupByHttpMethod,
                HasSegmentFailures = Value("segmentFailures") is string failures ? bool.Parse(failures) : query.HasSegmentFailures,
                IntervalOverlap = Value("overlap") is string overlap ? bool.Parse(overlap) : query.IntervalOverlap,
            };
            if (requestsOnly) { query = query with { Kind = OperationProfilingKind.HttpRequest.ToString() }; }

            return Result<OperationProfilingQuery>.Success(query);

            string Value(string key) => input[key].FirstOrDefault() is string value && value.Length > 0 ? value : null;
            string String(string key, string fallback) => input.ContainsKey(key) ? Value(key) : fallback;
            Guid? Id(string key) => Value(key) is string value ? Guid.Parse(value) : null;
            DateTimeOffset? Date(string key) => Value(key) is string value ? DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToUniversalTime() : null;
            T[] Outcomes<T>(string key) where T : struct, Enum => (Value(key) ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(value => Enum.Parse<T>(value, true)).ToArray();
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException or FormatException or OverflowException)
        {
            return Result<OperationProfilingQuery>.Failure(new ProfilingValidationError("Invalid profiling selector. Use typed dimensions, UTC dates, supported outcomes and bounded limits."));
        }
    }
}
