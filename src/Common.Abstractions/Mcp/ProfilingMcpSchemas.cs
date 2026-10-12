// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by the LICENSE file.

namespace BridgingIT.DevKit.Common;

/// <summary>Shares profiling argument schemas between CLI tools and runtime capabilities without a transport SDK dependency.</summary>
/// <example><code>var schema = ProfilingMcpSchemas.Query();</code></example>
public static class ProfilingMcpSchemas
{
    /// <summary>Defines independent log and profiling paging for correlation investigations.</summary>
    /// <example><code>var schema = ProfilingMcpSchemas.Correlation();</code></example>
    public static object Correlation()
        => new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "correlationId" },
            properties = new
            {
                correlationId = String("Correlation id to inspect."),
                startTime = DateTime("Inclusive lower timestamp bound."),
                endTime = DateTime("Inclusive log timestamp bound; exclusive profiling execution-start bound. Use UTC."),
                take = Integer("Maximum number of log entries to return."),
                continuationToken = String("Continuation token from a previous log response."),
                profilingTake = ProfilingTake("Maximum number of retained profiling executions to return.", 25),
                profilingCursor = String("Profiling nextCursor from a previous correlation response; keep the same correlation id, dates and profilingTake.")
            }
        };

    private static object ProfilingTake(string description, int defaultValue)
        => new { type = "integer", description, minimum = 1, maximum = 100, @default = defaultValue };

    private static object ProfilingObject(Dictionary<string, object> properties, string[] required = null)
        => new { type = "object", additionalProperties = false, properties, required = required ?? [] };

    private static Dictionary<string, object> ProfilingFilters()
        => new()
        {
            ["fromUtc"] = DateTime("Inclusive execution start bound in UTC. Omitted: fifteen minutes before the resolved end."),
            ["toUtc"] = DateTime("Exclusive execution start bound in UTC. Omitted: now, or the existing cursor boundary."),
            ["kind"] = String("Operation kind, including HttpRequest, Job, Pipeline, FileStorage, DocumentStorage or BlobStorage."),
            ["key"] = String("Exact logical operation key; use the route template for HTTP requests."),
            ["keyContains"] = String("Case-insensitive substring of the logical operation key."),
            ["nodeId"] = Guid("Executing process node id."),
            ["applicationVersion"] = String("Exact captured application version."),
            ["correlationId"] = String("Lookup-only correlation id."),
            ["outcomes"] = new { type = "array", description = "Root outcomes. Query and insights default to all; comparison defaults to Completed. Empty array means all.", maxItems = 5, items = Enum("Root outcome.", "Completed", "Failed", "Canceled", "Aborted", "Incomplete") },
            ["httpMethod"] = String("HTTP method, for example GET."),
            ["httpStatusCode"] = new { type = "integer", minimum = 100, maximum = 599 },
            ["hasSegmentFailures"] = Bool("Whether a root contains a failed segment, independently of root outcome."),
            ["dimensions"] = ProfilingPredicates(false),
            ["segmentPath"] = ProfilingObject(new() { ["components"] = new { type = "array", minItems = 1, items = String("Exact segment path component.") } }, ["components"]),
            ["segmentDimensions"] = ProfilingPredicates(true),
            ["segmentOutcomes"] = new { type = "array", maxItems = 4, items = Enum("Observed segment outcome.", "Completed", "Failed", "Canceled", "Incomplete") },
            ["groupingDimensions"] = new { type = "array", description = "Up to four operation dimensions. Roots missing any selected dimension are excluded.", maxItems = 4, items = String("Recorded operation dimension name.") }
        };

    private static object ProfilingPredicates(bool segment)
        => new
        {
            type = "array", maxItems = 8,
            description = "Typed dimension conditions; operation and segment predicates together are limited by the runtime.",
            items = ProfilingObject(new()
            {
                ["key"] = String("Recorded dimension name."),
                ["operator"] = segment ? Enum("Predicate; default Equal.", "Equal", "Present", "Missing", "Mixed") : Enum("Predicate; default Equal.", "Equal", "Present", "Missing"),
                ["value"] = ProfilingObject(new()
                {
                    ["type"] = Enum("Exact scalar type.", "String", "Boolean", "Int64", "Decimal", "Double", "UtcDateTime"),
                    ["scalar"] = String("Canonical scalar as a string. Required for Equal; omit value for presence, absence or mixed conditions.")
                }, ["type", "scalar"])
            }, ["key"])
        };

    /// <summary>Defines bounded selectors for retained operation and request recordings.</summary>
    /// <example><code>var schema = ProfilingMcpSchemas.Query();</code></example>
    public static object Query()
    {
        var properties = ProfilingFilters();
        properties["view"] = Enum("Ranking mode; default Recent.", "Recent", "Slow", "ByCount");
        properties["take"] = ProfilingTake("Maximum executions, or groups when grouped=true, subject to runtime query limits.", 25);
        properties["cursor"] = String("Opaque nextCursor. Keep all filters, view, grouped and take unchanged while paging.");
        properties["grouped"] = Bool("Return logical groups with counts and representative occurrences. HTTP methods remain separate.");
        return ProfilingObject(properties);
    }

    /// <summary>Defines an execution identifier and bounded segment output.</summary>
    /// <example><code>var schema = ProfilingMcpSchemas.Details();</code></example>
    public static object Details() => ProfilingObject(new()
    {
        ["id"] = Guid("Recorded execution id from query or insights."),
        ["segmentTake"] = ProfilingTake("Maximum segment paths, ordered by accumulated self time.", 20)
    }, ["id"]);

    /// <summary>Defines hotspot filters, ranking and bounded output.</summary>
    /// <example><code>var schema = ProfilingMcpSchemas.Insights();</code></example>
    public static object Insights()
    {
        var properties = ProfilingFilters();
        properties["rankBy"] = Enum("Hotspot rank; default p95. Latency and total use completed samples.", "p95", "total", "count", "failures");
        properties["take"] = ProfilingTake("Maximum groups after analyzing the entire bounded selection.", 25);
        properties["segmentTake"] = ProfilingTake("Maximum bottleneck paths per group.", 5);
        return ProfilingObject(properties);
    }

    /// <summary>Defines independent selections of one logical operation.</summary>
    /// <example><code>var schema = ProfilingMcpSchemas.Compare();</code></example>
    public static object Compare()
    {
        var side = ProfilingFilters();
        foreach (var name in new[] { "key", "kind", "httpMethod", "groupingDimensions" }) { side.Remove(name); }

        return ProfilingObject(new()
        {
            ["key"] = String("Exact operation key shared by both selections."),
            ["kind"] = String("Operation kind shared by both selections."),
            ["httpMethod"] = String("HTTP method shared by both selections."),
            ["baseline"] = ProfilingObject(side),
            ["candidate"] = ProfilingObject(side),
            ["segmentTake"] = ProfilingTake("Maximum aggregate segment paths in each selection.", 10)
        }, ["key", "kind", "baseline", "candidate"]);
    }

    private static object String(string description) => new { type = "string", description };
    private static object Bool(string description) => new { type = "boolean", description };
    private static object Integer(string description) => new { type = "integer", description, minimum = 1 };
    private static object DateTime(string description) => new { type = "string", format = "date-time", description };
    private static object Guid(string description) => new { type = "string", format = "uuid", description };
    private static object Enum(string description, params string[] values) => new { type = "string", description, @enum = values };
}
