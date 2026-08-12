// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;

using Microsoft.EntityFrameworkCore;

/// <summary>Defines the Entity Framework sets required by durable profiling persistence.</summary>
/// <example>
/// <code>
/// public sealed class AppDbContext : DbContext, IProfilingContext
/// {
///     public DbSet&lt;ProfilingSessionEntity&gt; ProfilingSessions { get; set; }
///
///     protected override void OnModelCreating(ModelBuilder modelBuilder)
///     {
///         base.OnModelCreating(modelBuilder);
///         modelBuilder.ConfigureProfiling();
///     }
/// }
/// </code>
/// </example>
public interface IProfilingContext
{
    /// <summary>Gets or sets profiling session rows.</summary>
    DbSet<ProfilingSessionEntity> ProfilingSessions { get; set; }

    /// <summary>Gets or sets invalidated session identity rows.</summary>
    DbSet<ProfilingInvalidSessionEntity> ProfilingInvalidSessions { get; set; }

    /// <summary>Gets or sets stable profiling node rows.</summary>
    DbSet<ProfilingNodeEntity> ProfilingNodes { get; set; }

    /// <summary>Gets or sets mutable session participation rows.</summary>
    DbSet<ProfilingParticipationEntity> ProfilingParticipations { get; set; }

    /// <summary>Gets or sets immutable runtime snapshot rows.</summary>
    DbSet<ProfilingSnapshotEntity> ProfilingSnapshots { get; set; }

    /// <summary>Gets or sets immutable custom metric observation rows.</summary>
    DbSet<ProfilingMetricObservationEntity> ProfilingMetricObservations { get; set; }
}
