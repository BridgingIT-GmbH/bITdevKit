// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;

/// <summary>Stores one instantaneous Runtime annotation with explicit scope.</summary>
/// <example><code>session.Markers.Add(new ProfilingMarkerEntity { Name = "Baseline" });</code></example>
public sealed class ProfilingMarkerEntity
{
    /// <summary>Gets or sets the marker identity.</summary>
    /// <example><code>var id = marker.Id;</code></example>
    public Guid Id { get; set; }
    /// <summary>Gets or sets the owning session identity.</summary>
    /// <example><code>var id = marker.SessionId;</code></example>
    public Guid SessionId { get; set; }
    /// <summary>Gets or sets an optional real node identity.</summary>
    /// <example><code>var id = marker.NodeId;</code></example>
    public Guid? NodeId { get; set; }
    /// <summary>Gets or sets annotation scope.</summary>
    /// <example><code>var scope = marker.Scope;</code></example>
    public ProfilingMarkerScope Scope { get; set; }
    /// <summary>Gets or sets the stable bounded kind.</summary>
    /// <example><code>marker.Kind = "Annotation";</code></example>
    [Required, MaxLength(128)]
    public string Kind { get; set; }
    /// <summary>Gets or sets the plain label.</summary>
    /// <example><code>marker.Name = "Baseline";</code></example>
    [Required, MaxLength(100)]
    public string Name { get; set; }
    /// <summary>Gets or sets observation UTC.</summary>
    /// <example><code>var utc = marker.TimestampUtc;</code></example>
    public DateTimeOffset TimestampUtc { get; set; }
}

/// <summary>Represents one measured segment owned by a profiling session JSON document.</summary>
/// <example><code>session.Segments.Add(new RuntimeProfilingSegmentEntity { Id = segmentId });</code></example>
public sealed class RuntimeProfilingSegmentEntity
{
    /// <summary>Gets or sets the segment identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the session identifier.</summary>
    public Guid SessionId { get; set; }

    /// <summary>Gets or sets the node identifier.</summary>
    public Guid NodeId { get; set; }

    /// <summary>Gets or sets the segment name.</summary>
    [Required]
    [MaxLength(256)]
    public string Name { get; set; }

    /// <summary>Gets or sets the start timestamp.</summary>
    [Required]
    public DateTimeOffset StartedUtc { get; set; }

    /// <summary>Gets or sets the end timestamp.</summary>
    public DateTimeOffset? EndedUtc { get; set; }

    /// <summary>Gets or sets the elapsed duration.</summary>
    public TimeSpan? Elapsed { get; set; }

    /// <summary>Gets or sets the segment outcome.</summary>
    public ProfilingSegmentOutcome? Outcome { get; set; }

    /// <summary>Gets or sets the safe exception type.</summary>
    [MaxLength(512)]
    public string ExceptionType { get; set; }

    /// <summary>Gets or sets the safe exception message.</summary>
    [MaxLength(4000)]
    public string ExceptionMessage { get; set; }

    /// <summary>Gets or sets an optional note.</summary>
    [MaxLength(4000)]
    public string Note { get; set; }

    /// <summary>Gets or sets an optional correlation identifier.</summary>
    [MaxLength(256)]
    public string CorrelationId { get; set; }

    /// <summary>Gets or sets the optional parent segment identifier.</summary>
    public Guid? ParentSegmentId { get; set; }

    /// <summary>Gets or sets whether collection ended before the operation.</summary>
    [Required]
    public bool CollectionEndedBeforeOperation { get; set; }

    /// <summary>Gets or sets ordered plain tags.</summary>
    public ICollection<RuntimeProfilingSegmentTagEntity> Tags { get; set; } = [];
}

/// <summary>Represents one ordered tag nested in a segment JSON document.</summary>
/// <example><code>segment.Tags.Add(new RuntimeProfilingSegmentTagEntity { Position = 0, Value = "database" });</code></example>
public sealed class RuntimeProfilingSegmentTagEntity
{
    /// <summary>Gets or sets the owning segment identifier.</summary>
    public Guid SegmentId { get; set; }

    /// <summary>Gets or sets the stable tag position.</summary>
    public int Position { get; set; }

    /// <summary>Gets or sets the trimmed tag value.</summary>
    [Required]
    [MaxLength(256)]
    public string Value { get; set; }
}

/// <summary>Represents one immutable custom profiling metric observation.</summary>
/// <example><code>public DbSet&lt;RuntimeProfilingMetricObservationEntity&gt; ProfilingMetricObservations { get; set; }</code></example>
[Table("__Profiling_RuntimeMetricObservations")]
[Index(nameof(SessionId), nameof(NodeId), nameof(TimestampUtc))]
[Index(nameof(SessionId), nameof(MetricIdentifier), nameof(TimestampUtc))]
public sealed class RuntimeProfilingMetricObservationEntity
{
    /// <summary>Gets or sets the observation identifier.</summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>Gets or sets the session identifier.</summary>
    public Guid SessionId { get; set; }

    /// <summary>Gets or sets the node identifier.</summary>
    public Guid NodeId { get; set; }

    /// <summary>Gets or sets the optional ambient segment identifier.</summary>
    public Guid? SegmentId { get; set; }

    /// <summary>Gets or sets the stable metric identifier.</summary>
    [Required]
    [MaxLength(256)]
    public string MetricIdentifier { get; set; }

    /// <summary>Gets or sets the metric kind.</summary>
    [Required]
    public ProfilingMetricKind Kind { get; set; }

    /// <summary>Gets or sets the observed value.</summary>
    [Required]
    public double Value { get; set; }

    /// <summary>Gets or sets the optional unit.</summary>
    [MaxLength(64)]
    public string Unit { get; set; }

    /// <summary>Gets or sets the observation timestamp.</summary>
    [Required]
    public DateTimeOffset TimestampUtc { get; set; }

    /// <summary>Gets or sets the owning session.</summary>
    [Required]
    [ForeignKey(nameof(SessionId))]
    public RuntimeProfilingSessionEntity Session { get; set; }

    /// <summary>Gets or sets the producing node.</summary>
    [Required]
    [ForeignKey(nameof(NodeId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public ProfilingNodeEntity Node { get; set; }
}
