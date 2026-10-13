// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Identifies legacy built-in dimensions that describe individual executions instead of workloads.</summary>
/// <example><code>var executionId = ProfilingDimensionKeys.IsExecutionIdentifier("orchestration.instanceId");</code></example>
public static class ProfilingDimensionKeys
{
    /// <summary>Gets built-in execution identifiers omitted from dimension discovery and dashboard grouping.</summary>
    /// <example><code>var legacyKeys = ProfilingDimensionKeys.ExecutionIdentifiers;</code></example>
    public static IReadOnlyList<string> ExecutionIdentifiers { get; } = Array.AsReadOnly(new[]
    {
        "orchestration.instanceId", "orchestration.correlationId", "job.executionId", "pipeline.executionId",
    });

    /// <summary>Checks a built-in execution identifier without excluding application-defined business identifiers.</summary>
    /// <param name="key">The recorded dimension name.</param>
    /// <returns>True for a legacy built-in execution identifier, ignoring name casing.</returns>
    /// <example><code>var excluded = ProfilingDimensionKeys.IsExecutionIdentifier("pipeline.executionId");</code></example>
    public static bool IsExecutionIdentifier(string key) => ExecutionIdentifiers.Contains(key, StringComparer.OrdinalIgnoreCase);
}

/// <summary>Identifies the metadata owner used for dimension discovery.</summary>
/// <example><code>var scope = ProfilingDimensionScope.Operation;</code></example>
public enum ProfilingDimensionScope
{
    /// <summary>Uses dimensions recorded on the operation itself.</summary>
    /// <example><code>var scope = ProfilingDimensionScope.Operation;</code></example>
    Operation,
    /// <summary>Uses one explicitly selected full segment path.</summary>
    /// <example><code>var scope = ProfilingDimensionScope.Segment;</code></example>
    Segment,
}

/// <summary>Reports the execution count for one exact typed value, without extrapolating samples.</summary>
/// <example><code>var count = choice.Count;</code></example>
public sealed record ProfilingDimensionValueChoice
{
    /// <summary>Gets the canonical typed value; large integers remain strings in its scalar.</summary>
    /// <example><code>var scalar = choice.Value.Scalar;</code></example>
    public ProfilingValue Value { get; init; }
    /// <summary>Gets executions with this complete, uniform value.</summary>
    /// <example><code>var count = choice.Count;</code></example>
    public long Count { get; init; }
}

/// <summary>Describes one dimension and its most frequent bounded, searchable values.</summary>
/// <example><code>var choices = dimension.Values;</code></example>
public sealed record ProfilingDimensionChoice
{
    /// <summary>Gets the recorded name, matched case-insensitively.</summary>
    /// <example><code>var name = dimension.Key;</code></example>
    public string Key { get; init; }
    /// <summary>Gets executions where this dimension was observed, including mixed segment values.</summary>
    /// <example><code>var count = dimension.PresentCount;</code></example>
    public long PresentCount { get; init; }
    /// <summary>Gets executions without this dimension on the selected owner.</summary>
    /// <example><code>var count = dimension.MissingCount;</code></example>
    public long MissingCount { get; init; }
    /// <summary>Gets executions with mixed values within the selected segment summary.</summary>
    /// <example><code>var count = dimension.MixedCount;</code></example>
    public long MixedCount { get; init; }
    /// <summary>Gets executions with partial dimension coverage within the selected segment summary.</summary>
    /// <example><code>var count = dimension.PartialCount;</code></example>
    public long PartialCount { get; init; }
    /// <summary>Gets distinct complete typed values before value search.</summary>
    /// <example><code>var count = dimension.DistinctValueCount;</code></example>
    public int DistinctValueCount { get; init; }
    /// <summary>Gets whether more values match the search than can be returned.</summary>
    /// <example><code>if (dimension.ValuesTruncated) SearchMore();</code></example>
    public bool ValuesTruncated { get; init; }
    /// <summary>Gets choices ranked by count, then exact type and ordinal scalar.</summary>
    /// <example><code>var choices = dimension.Values;</code></example>
    public IReadOnlyList<ProfilingDimensionValueChoice> Values { get; init; } = [];
}

/// <summary>Returns exact dimension choices across a bounded retained selection, independently of visible rows.</summary>
/// <example><code>var names = discovery.Dimensions.Select(dimension => dimension.Key);</code></example>
public sealed record OperationProfilingDimensionDiscovery
{
    /// <summary>Gets bounded recorded segment paths for choosing the owner of segment filters.</summary>
    /// <example><code>var paths = discovery.SegmentPaths;</code></example>
    public IReadOnlyList<ProfilingSegmentPathChoice> SegmentPaths { get; init; } = [];
    /// <summary>Gets whether more recorded paths exist than can be returned.</summary>
    /// <example><code>if (discovery.PathsTruncated) NarrowSelection();</code></example>
    public bool PathsTruncated { get; init; }
    /// <summary>Gets the selected metadata scope.</summary>
    /// <example><code>var scope = discovery.Scope;</code></example>
    public ProfilingDimensionScope Scope { get; init; }
    /// <summary>Gets the explicit segment path, or null for operation dimensions.</summary>
    /// <example><code>var path = discovery.SegmentPath;</code></example>
    public ProfilingSegmentPath SegmentPath { get; init; }
    /// <summary>Gets contributing retained executions, including owners with missing metadata.</summary>
    /// <example><code>var count = discovery.OperationCount;</code></example>
    public long OperationCount { get; init; }
    /// <summary>Gets dimension names matching the search before the name limit.</summary>
    /// <example><code>var count = discovery.MatchingNameCount;</code></example>
    public int MatchingNameCount { get; init; }
    /// <summary>Gets whether additional matching names were omitted.</summary>
    /// <example><code>if (discovery.NamesTruncated) SearchNames();</code></example>
    public bool NamesTruncated { get; init; }
    /// <summary>Gets bounded names and value choices.</summary>
    /// <example><code>var dimensions = discovery.Dimensions;</code></example>
    public IReadOnlyList<ProfilingDimensionChoice> Dimensions { get; init; } = [];
    /// <summary>Gets the provider's captured interval and publication boundary.</summary>
    /// <example><code>var from = discovery.Boundary.FromUtc;</code></example>
    public ProfilingQueryBoundary Boundary { get; init; }
}

/// <summary>Reports retained executions owning one complete segment path.</summary>
/// <example><code>var count = choice.Count;</code></example>
public sealed record ProfilingSegmentPathChoice
{
    /// <summary>Gets the full nested path, with case-insensitive component identity.</summary>
    /// <example><code>var components = choice.Path.Components;</code></example>
    public ProfilingSegmentPath Path { get; init; }
    /// <summary>Gets the number of executions with this path.</summary>
    /// <example><code>var count = choice.Count;</code></example>
    public long Count { get; init; }
}
