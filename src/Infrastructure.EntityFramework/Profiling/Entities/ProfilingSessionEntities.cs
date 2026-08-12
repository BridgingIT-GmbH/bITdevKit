// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;

/// <summary>Represents one durable profiling session.</summary>
/// <example><code>public DbSet&lt;ProfilingSessionEntity&gt; ProfilingSessions { get; set; }</code></example>
[Table("__Profiling_Sessions")]
[Index(nameof(Key), IsUnique = true)]
[Index(nameof(LifecycleKey), IsUnique = true)]
[Index(nameof(State), nameof(CompletedUtc))]
public sealed class ProfilingSessionEntity
{
    /// <summary>Gets or sets the session identifier.</summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>Gets or sets the readable session key.</summary>
    [Required]
    [MaxLength(8)]
    public string Key { get; set; }

    /// <summary>Gets or sets the portable unique active-session coordination key.</summary>
    [Required]
    [MaxLength(32)]
    public string LifecycleKey { get; set; }

    /// <summary>Gets or sets the optional display name.</summary>
    [MaxLength(256)]
    public string Name { get; set; }

    /// <summary>Gets or sets the lifecycle state.</summary>
    [Required]
    public ProfilingSessionState State { get; set; }

    /// <summary>Gets or sets the logical start timestamp.</summary>
    [Required]
    public DateTimeOffset StartedUtc { get; set; }

    /// <summary>Gets or sets the original logical end timestamp.</summary>
    [Required]
    public DateTimeOffset EndsUtc { get; set; }

    /// <summary>Gets or sets the terminal transition timestamp.</summary>
    public DateTimeOffset? CompletedUtc { get; set; }

    /// <summary>Gets or sets the configured sampling interval.</summary>
    [Required]
    public TimeSpan SamplingInterval { get; set; }

    /// <summary>Gets or sets the configured duration.</summary>
    [Required]
    public TimeSpan Duration { get; set; }

    /// <summary>Gets or sets whether retention excludes the session.</summary>
    [Required]
    public bool IsPinned { get; set; }

    /// <summary>Gets or sets the optional note.</summary>
    [MaxLength(4000)]
    public string Note { get; set; }

    /// <summary>Gets or sets the optimistic concurrency token.</summary>
    [Required]
    [ConcurrencyCheck]
    public Guid ConcurrencyVersion { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets ordered session tags.</summary>
    public ICollection<ProfilingSessionTagEntity> Tags { get; set; } = [];

    /// <summary>Gets or sets immutable runtime contexts stored in the session document.</summary>
    public ICollection<ProfilingRuntimeContextEntity> RuntimeContexts { get; set; } = [];

    /// <summary>Gets or sets immutable phase markers stored in the session document.</summary>
    public ICollection<ProfilingPhaseMarkerEntity> PhaseMarkers { get; set; } = [];

    /// <summary>Gets or sets immutable action markers stored in the session document.</summary>
    public ICollection<ProfilingActionMarkerEntity> ActionMarkers { get; set; } = [];

    /// <summary>Gets or sets measured segments stored in the session document.</summary>
    public ICollection<ProfilingSegmentEntity> Segments { get; set; } = [];

    /// <summary>Advances the optimistic concurrency token.</summary>
    /// <example><code>entity.AdvanceConcurrencyVersion();</code></example>
    public void AdvanceConcurrencyVersion() => this.ConcurrencyVersion = Guid.NewGuid();
}

/// <summary>Preserves one invalidated session identity after data deletion.</summary>
/// <example><code>public DbSet&lt;ProfilingInvalidSessionEntity&gt; ProfilingInvalidSessions { get; set; }</code></example>
[Table("__Profiling_InvalidSessions")]
[Index(nameof(Key), IsUnique = true)]
public sealed class ProfilingInvalidSessionEntity
{
    /// <summary>Gets or sets the invalidated session identifier.</summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>Gets or sets the invalidated readable session key.</summary>
    [Required]
    [MaxLength(8)]
    public string Key { get; set; }
}

/// <summary>Represents one ordered session tag owned by a profiling session JSON document.</summary>
/// <example><code>session.Tags.Add(new ProfilingSessionTagEntity { Position = 0, Value = "checkout" });</code></example>
public sealed class ProfilingSessionTagEntity
{
    /// <summary>Gets or sets the owning session identifier.</summary>
    public Guid SessionId { get; set; }

    /// <summary>Gets or sets the stable tag position.</summary>
    public int Position { get; set; }

    /// <summary>Gets or sets the trimmed tag value.</summary>
    [Required]
    [MaxLength(256)]
    public string Value { get; set; }
}
