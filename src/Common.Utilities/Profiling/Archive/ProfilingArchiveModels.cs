// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.Json.Serialization;

/// <summary>Defines the fixed portable Profiling archive contract.</summary>
/// <example><code>var version = ProfilingArchiveFormat.Version;</code></example>
public static class ProfilingArchiveFormat
{
    /// <summary>Gets the format discriminator written to every archive.</summary>
    /// <example><code>var format = ProfilingArchiveFormat.Identifier;</code></example>
    public const string Identifier = "bitdevkit.profiling.archive";

    /// <summary>Gets the only supported archive version.</summary>
    /// <example><code>var version = ProfilingArchiveFormat.Version;</code></example>
    public const int Version = 1;

    /// <summary>Gets the maximum accepted or produced archive size.</summary>
    /// <example><code>var limit = ProfilingArchiveFormat.MaximumSizeBytes;</code></example>
    public const int MaximumSizeBytes = 25 * 1024 * 1024;
}

/// <summary>Describes the evidence scope contained in an archive.</summary>
/// <example><code>var kind = ProfilingArchiveKind.Session;</code></example>
public enum ProfilingArchiveKind
{
    /// <summary>The archive contains one complete terminal session.</summary>
    Session,

    /// <summary>The archive contains one immutable snapshot and its minimum context.</summary>
    Snapshot,
}

/// <summary>Contains one portable, versioned Profiling archive.</summary>
/// <example><code>var kind = archive.Kind;</code></example>
public sealed record ProfilingArchive
{
    /// <summary>Gets the fixed format discriminator.</summary>
    [JsonRequired]
    public string Format { get; init; } = ProfilingArchiveFormat.Identifier;

    /// <summary>Gets the fixed archive compatibility version.</summary>
    [JsonRequired]
    public int Version { get; init; } = ProfilingArchiveFormat.Version;

    /// <summary>Gets the archive evidence scope.</summary>
    [JsonRequired]
    public ProfilingArchiveKind Kind { get; init; }

    /// <summary>Gets when the archive was created.</summary>
    [JsonRequired]
    public DateTimeOffset ExportedUtc { get; init; }

    /// <summary>Gets the source session metadata.</summary>
    [JsonRequired]
    public ProfilingSession Session { get; init; }

    /// <summary>Gets the included node records without private Broadcast correlation.</summary>
    [JsonRequired]
    public IReadOnlyList<ProfilingNode> Nodes { get; init; } = [];

    /// <summary>Gets the included node participations.</summary>
    [JsonRequired]
    public IReadOnlyList<ProfilingNodeParticipation> Participations { get; init; } = [];

    /// <summary>Gets the included immutable runtime contexts.</summary>
    [JsonRequired]
    public IReadOnlyList<ProfilingRuntimeContext> RuntimeContexts { get; init; } = [];

    /// <summary>Gets the included immutable runtime snapshots.</summary>
    [JsonRequired]
    public IReadOnlyList<ProfilingSnapshot> Snapshots { get; init; } = [];

    /// <summary>Gets the included shared phase markers.</summary>
    [JsonRequired]
    public IReadOnlyList<ProfilingPhaseMarker> PhaseMarkers { get; init; } = [];

    /// <summary>Gets the included node action markers.</summary>
    [JsonRequired]
    public IReadOnlyList<ProfilingActionMarker> ActionMarkers { get; init; } = [];

    /// <summary>Gets the included segments with archive-local relationships.</summary>
    [JsonRequired]
    public IReadOnlyList<ProfilingArchiveSegment> Segments { get; init; } = [];

    /// <summary>Gets the included custom metrics with archive-local segment relationships.</summary>
    [JsonRequired]
    public IReadOnlyList<ProfilingArchiveMetricObservation> MetricObservations { get; init; } = [];
}

/// <summary>Wraps a segment with archive-local reference identifiers.</summary>
/// <param name="Reference">The positive archive-local segment reference.</param>
/// <param name="ParentReference">The optional archive-local parent reference.</param>
/// <param name="Segment">The portable segment values.</param>
/// <example><code>var parent = item.ParentReference;</code></example>
public sealed record ProfilingArchiveSegment(
    int Reference,
    int? ParentReference,
    ProfilingSegment Segment
);

/// <summary>Wraps a metric with its optional archive-local segment reference.</summary>
/// <param name="SegmentReference">The optional archive-local segment reference.</param>
/// <param name="Observation">The portable metric observation values.</param>
/// <example><code>var metric = item.Observation;</code></example>
public sealed record ProfilingArchiveMetricObservation(
    int? SegmentReference,
    ProfilingMetricObservation Observation
);

/// <summary>Reports the fresh readable identities created by one archive import.</summary>
/// <param name="SessionKey">The new imported session key.</param>
/// <param name="NodeKeys">Source-to-imported node key mappings.</param>
/// <param name="SnapshotKeys">Source-to-imported snapshot key mappings.</param>
/// <example><code>var importedSession = result.SessionKey;</code></example>
public sealed record ProfilingArchiveImportResult(
    string SessionKey,
    IReadOnlyDictionary<string, string> NodeKeys,
    IReadOnlyDictionary<string, string> SnapshotKeys
);
