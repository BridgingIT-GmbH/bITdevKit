// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;

/// <summary>Represents one immutable phase marker owned by a profiling session JSON document.</summary>
/// <example><code>session.PhaseMarkers.Add(new ProfilingPhaseMarkerEntity { Id = markerId });</code></example>
public sealed class ProfilingPhaseMarkerEntity
{
    /// <summary>Gets or sets the marker identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the session identifier.</summary>
    public Guid SessionId { get; set; }

    /// <summary>Gets or sets the marker name.</summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; }

    /// <summary>Gets or sets the marker timestamp.</summary>
    [Required]
    public DateTimeOffset TimestampUtc { get; set; }
}

/// <summary>Represents one immutable action marker owned by a profiling session JSON document.</summary>
/// <example><code>session.ActionMarkers.Add(new ProfilingActionMarkerEntity { Id = markerId });</code></example>
public sealed class ProfilingActionMarkerEntity
{
    /// <summary>Gets or sets the marker identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the session identifier.</summary>
    public Guid SessionId { get; set; }

    /// <summary>Gets or sets the node identifier.</summary>
    public Guid NodeId { get; set; }

    /// <summary>Gets or sets the action name.</summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; }

    /// <summary>Gets or sets the action timestamp.</summary>
    [Required]
    public DateTimeOffset TimestampUtc { get; set; }
}

/// <summary>Represents one measured segment owned by a profiling session JSON document.</summary>
/// <example><code>session.Segments.Add(new ProfilingSegmentEntity { Id = segmentId });</code></example>
public sealed class ProfilingSegmentEntity
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
    [Required]
    public ProfilingSegmentOutcome Outcome { get; set; }

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
    public ICollection<ProfilingSegmentTagEntity> Tags { get; set; } = [];
}

/// <summary>Represents one ordered tag nested in a segment JSON document.</summary>
/// <example><code>segment.Tags.Add(new ProfilingSegmentTagEntity { Position = 0, Value = "database" });</code></example>
public sealed class ProfilingSegmentTagEntity
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
/// <example><code>public DbSet&lt;ProfilingMetricObservationEntity&gt; ProfilingMetricObservations { get; set; }</code></example>
[Table("__Profiling_MetricObservations")]
[Index(nameof(SessionId), nameof(NodeId), nameof(TimestampUtc))]
[Index(nameof(SessionId), nameof(MetricIdentifier), nameof(TimestampUtc))]
public sealed class ProfilingMetricObservationEntity
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
    public ProfilingSessionEntity Session { get; set; }

    /// <summary>Gets or sets the producing node.</summary>
    [Required]
    [ForeignKey(nameof(NodeId))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public ProfilingNodeEntity Node { get; set; }
}
