// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;

using Microsoft.EntityFrameworkCore;

/// <summary>Defines the Entity Framework sets required by durable profiling persistence.</summary>
/// <example>
/// <code>
/// public sealed class AppDbContext : DbContext, IProfilingDbContext
/// {
///     public DbSet&lt;RuntimeProfilingSessionEntity&gt; ProfilingSessions { get; set; }
///
///     protected override void OnModelCreating(ModelBuilder modelBuilder)
///     {
///         base.OnModelCreating(modelBuilder);
///         modelBuilder.ConfigureProfiling();
///     }
/// }
/// </code>
/// </example>
public interface IProfilingDbContext
{
    /// <summary>Gets or sets profiling session rows.</summary>
    DbSet<RuntimeProfilingSessionEntity> ProfilingSessions { get; set; }

    /// <summary>Gets or sets invalidated session identity rows.</summary>
    DbSet<RuntimeProfilingInvalidSessionEntity> ProfilingInvalidSessions { get; set; }

    /// <summary>Gets or sets stable profiling node rows.</summary>
    DbSet<ProfilingNodeEntity> ProfilingNodes { get; set; }

    /// <summary>Gets or sets mutable session participation rows.</summary>
    DbSet<RuntimeProfilingParticipationEntity> ProfilingParticipations { get; set; }

    /// <summary>Gets or sets immutable runtime snapshot rows.</summary>
    DbSet<RuntimeProfilingSnapshotEntity> ProfilingSnapshots { get; set; }

    /// <summary>Gets or sets immutable custom metric observation rows.</summary>
    DbSet<RuntimeProfilingMetricObservationEntity> ProfilingMetricObservations { get; set; }
    /// <summary>Gets or sets immutable operation root graphs.</summary>
    /// <example><code>var set = context.ProfilingOperations;</code></example>
    DbSet<OperationProfilingEntity> ProfilingOperations { get; set; }

    /// <summary>Gets or sets aggregate operation segment summaries.</summary>
    /// <example><code>var set = context.ProfilingOperationSegments;</code></example>
    DbSet<OperationProfilingSegmentEntity> ProfilingOperationSegments { get; set; }

    /// <summary>Gets or sets portable typed metadata projections.</summary>
    /// <example><code>var set = context.ProfilingOperationDimensions;</code></example>
    DbSet<OperationProfilingDimensionEntity> ProfilingOperationDimensions { get; set; }

    /// <summary>Gets or sets root and aggregate measurement projections.</summary>
    /// <example><code>var set = context.ProfilingOperationMeasurements;</code></example>
    DbSet<OperationProfilingMeasurementEntity> ProfilingOperationMeasurements { get; set; }

    /// <summary>Gets or sets provider-wide publication and deletion coordination.</summary>
    /// <example><code>var set = context.ProfilingStoreStates;</code></example>
    DbSet<ProfilingStoreStateEntity> ProfilingStoreStates { get; set; }

    /// <summary>Gets or sets bounded active writer leases.</summary>
    /// <example><code>var set = context.ProfilingWriters;</code></example>
    DbSet<ProfilingWriterEntity> ProfilingWriters { get; set; }

    /// <summary>Gets or sets bounded clear preparation and sealed fences.</summary>
    /// <example><code>var set = context.ProfilingClears;</code></example>
    DbSet<ProfilingClearEntity> ProfilingClears { get; set; }
    /// <summary>Gets or sets independent Runtime maintenance coordination.</summary>
    /// <example><code>var set = context.ProfilingRuntimeGates;</code></example>
    DbSet<ProfilingRuntimeGateEntity> ProfilingRuntimeGates { get; set; }
}
