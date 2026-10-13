// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.Json.Serialization;

/// <summary>Defines the fixed portable Profiling archive contract.</summary>
/// <example><code>var version = RuntimeProfilingArchiveFormat.Version;</code></example>
public static class RuntimeProfilingArchiveFormat
{
    /// <summary>Gets the format discriminator written to every archive.</summary>
    /// <example><code>var format = RuntimeProfilingArchiveFormat.Identifier;</code></example>
    public const string Identifier = "bitdevkit.profiling.archive";

    /// <summary>Gets the only supported archive version.</summary>
    /// <example><code>var version = RuntimeProfilingArchiveFormat.Version;</code></example>
    public const int Version = 2;

    /// <summary>Gets the maximum accepted or produced archive size.</summary>
    /// <example><code>var limit = RuntimeProfilingArchiveFormat.MaximumSizeBytes;</code></example>
    public const int MaximumSizeBytes = 25 * 1024 * 1024;
}

/// <summary>Describes the evidence scope contained in an archive.</summary>
/// <example><code>var kind = RuntimeProfilingArchiveKind.Session;</code></example>
public enum RuntimeProfilingArchiveKind
{
    /// <summary>The archive contains one complete terminal session.</summary>
    Session,

    /// <summary>The archive contains one immutable snapshot and its minimum context.</summary>
    Snapshot,
}

/// <summary>Contains one portable, versioned Profiling archive.</summary>
/// <example><code>var kind = archive.Kind;</code></example>
public sealed record RuntimeProfilingArchive
{
    /// <summary>Gets the fixed format discriminator.</summary>
    [JsonRequired]
    public string Format { get; init; } = RuntimeProfilingArchiveFormat.Identifier;

    /// <summary>Gets the fixed archive compatibility version.</summary>
    [JsonRequired]
    public int Version { get; init; } = RuntimeProfilingArchiveFormat.Version;

    /// <summary>Gets the archive evidence scope.</summary>
    [JsonRequired]
    public RuntimeProfilingArchiveKind Kind { get; init; }

    /// <summary>Gets when the archive was created.</summary>
    [JsonRequired]
    [System.Text.Json.Serialization.JsonConverter(typeof(ProfilingUtcJsonConverter))]
    public DateTimeOffset ExportedUtc { get; init; }

    /// <summary>Gets the source session metadata.</summary>
    [JsonRequired]
    public RuntimeProfilingSession Session { get; init; }

    /// <summary>Gets the included node records without private Broadcast correlation.</summary>
    [JsonRequired]
    public IReadOnlyList<ProfilingNode> Nodes { get; init; } = [];

    /// <summary>Gets the included node participations.</summary>
    [JsonRequired]
    public IReadOnlyList<RuntimeProfilingNodeParticipation> Participations { get; init; } = [];

    /// <summary>Gets the included immutable runtime contexts.</summary>
    [JsonRequired]
    public IReadOnlyList<RuntimeProfilingContext> RuntimeContexts { get; init; } = [];

    /// <summary>Gets the included immutable runtime snapshots.</summary>
    [JsonRequired]
    public IReadOnlyList<RuntimeProfilingSnapshot> Snapshots { get; init; } = [];

    /// <summary>Gets the included scoped instantaneous markers.</summary>
    [JsonRequired]
    public IReadOnlyList<ProfilingMarker> Markers { get; init; } = [];

    /// <summary>Gets the included segments with archive-local relationships.</summary>
    [JsonRequired]
    public IReadOnlyList<RuntimeProfilingArchiveSegment> Segments { get; init; } = [];

    /// <summary>Gets the included custom metrics with archive-local segment relationships.</summary>
    [JsonRequired]
    public IReadOnlyList<RuntimeProfilingArchiveMetricObservation> MetricObservations { get; init; } = [];
}

/// <summary>Wraps a segment with archive-local reference identifiers.</summary>
/// <param name="Reference">The positive archive-local segment reference.</param>
/// <param name="ParentReference">The optional archive-local parent reference.</param>
/// <param name="Segment">The portable segment values.</param>
/// <example><code>var parent = item.ParentReference;</code></example>
public sealed record RuntimeProfilingArchiveSegment(
    int Reference,
    int? ParentReference,
    ProfilingSegment Segment
);

/// <summary>Wraps a metric with its optional archive-local segment reference.</summary>
/// <param name="SegmentReference">The optional archive-local segment reference.</param>
/// <param name="Observation">The portable metric observation values.</param>
/// <example><code>var metric = item.Observation;</code></example>
public sealed record RuntimeProfilingArchiveMetricObservation(
    int? SegmentReference,
    ProfilingMetricObservation Observation
);

/// <summary>Reports the fresh readable identities created by one archive import.</summary>
/// <param name="SessionKey">The new imported session key.</param>
/// <param name="NodeKeys">Source-to-imported node key mappings.</param>
/// <param name="SnapshotKeys">Source-to-imported snapshot key mappings.</param>
/// <example><code>var importedSession = result.SessionKey;</code></example>
public sealed record RuntimeProfilingArchiveImportResult(
    string SessionKey,
    IReadOnlyDictionary<string, string> NodeKeys,
    IReadOnlyDictionary<string, string> SnapshotKeys
);
