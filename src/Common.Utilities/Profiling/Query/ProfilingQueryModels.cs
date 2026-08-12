// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Contains one selected node's complete profiling read model.</summary>
/// <example><code>var latest = data.LatestSnapshot;</code></example>
public sealed record ProfilingNodeSessionData
{
    /// <summary>Gets the selected session.</summary>
    public ProfilingSession Session { get; init; }

    /// <summary>Gets the selected public node key.</summary>
    public string NodeKey { get; init; }

    /// <summary>Gets node metadata when it was recorded.</summary>
    public ProfilingNode Node { get; init; }

    /// <summary>Gets expected or ad-hoc participation state when it was recorded.</summary>
    public ProfilingNodeParticipation Participation { get; init; }

    /// <summary>Gets immutable runtime context when it was recorded.</summary>
    public ProfilingRuntimeContext RuntimeContext { get; init; }

    /// <summary>Gets the latest persisted snapshot when one exists.</summary>
    public ProfilingSnapshot LatestSnapshot { get; init; }

    /// <summary>Gets the selected node's snapshots in node-local order.</summary>
    public IReadOnlyList<ProfilingSnapshot> Snapshots { get; init; } = [];

    /// <summary>Gets immutable shared phase markers for the selected session.</summary>
    public IReadOnlyList<ProfilingPhaseMarker> PhaseMarkers { get; init; } = [];

    /// <summary>Gets immutable action markers owned by the selected node.</summary>
    public IReadOnlyList<ProfilingActionMarker> ActionMarkers { get; init; } = [];

    /// <summary>Gets measured segments owned by the selected node.</summary>
    public IReadOnlyList<ProfilingSegment> Segments { get; init; } = [];

    /// <summary>Gets custom metric observations produced by the selected node.</summary>
    public IReadOnlyList<ProfilingMetricObservation> MetricObservations { get; init; } = [];

    /// <summary>Gets the compact current sampling status without running evaluation.</summary>
    public ProfilingSamplingStatus SamplingStatus { get; init; }
}

/// <summary>Describes the current persisted sampling state for one selected node.</summary>
/// <param name="SuccessfulCaptureCount">The latest successful-capture total.</param>
/// <param name="SkippedCaptureCount">The latest skipped-opportunity total.</param>
/// <param name="FailedCaptureCount">The latest failed-capture total.</param>
/// <param name="LatestCaptureDuration">The latest capture overhead when available.</param>
/// <param name="LatestSamplingDelay">The latest scheduled-to-start delay when available.</param>
/// <example><code>var failures = status.FailedCaptureCount;</code></example>
public sealed record ProfilingSamplingStatus(
    long SuccessfulCaptureCount,
    long SkippedCaptureCount,
    long FailedCaptureCount,
    TimeSpan? LatestCaptureDuration,
    TimeSpan? LatestSamplingDelay
);

/// <summary>Contains raw differences for two ordered same-node snapshots.</summary>
/// <param name="SessionKey">The public session key.</param>
/// <param name="NodeKey">The public node key.</param>
/// <param name="EarlierSnapshotKey">The earlier public snapshot key.</param>
/// <param name="LaterSnapshotKey">The later public snapshot key.</param>
/// <param name="Metrics">The fixed quantitative runtime metric rows.</param>
/// <example><code>var cpu = comparison.Metrics.Single(x => x.Identifier == "cpu-usage");</code></example>
public sealed record ProfilingSnapshotComparison(
    string SessionKey,
    string NodeKey,
    string EarlierSnapshotKey,
    string LaterSnapshotKey,
    IReadOnlyList<ProfilingSnapshotMetricDelta> Metrics
);

/// <summary>Describes one raw quantitative metric difference.</summary>
/// <param name="Identifier">The stable metric identifier.</param>
/// <param name="Unit">The fixed metric unit.</param>
/// <param name="EarlierValue">The earlier raw value when available.</param>
/// <param name="LaterValue">The later raw value when available.</param>
/// <param name="Difference">The signed later-minus-earlier difference when available.</param>
/// <param name="PercentageDifference">The signed percentage difference when safely available.</param>
/// <example><code>var changed = delta.Difference is not null;</code></example>
public sealed record ProfilingSnapshotMetricDelta(
    string Identifier,
    string Unit,
    decimal? EarlierValue,
    decimal? LaterValue,
    decimal? Difference,
    decimal? PercentageDifference
);