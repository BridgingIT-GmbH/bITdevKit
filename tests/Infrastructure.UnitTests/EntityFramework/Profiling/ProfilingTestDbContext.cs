// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Profiling;

using BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;
using Microsoft.EntityFrameworkCore;

public sealed class ProfilingTestDbContext(DbContextOptions<ProfilingTestDbContext> options)
    : DbContext(options),
        IProfilingDbContext
{
    public static int InstancesCreated;

    public DbSet<OperationProfilingEntity> ProfilingOperations { get; set; }

    public DbSet<OperationProfilingSegmentEntity> ProfilingOperationSegments { get; set; }

    public DbSet<OperationProfilingDimensionEntity> ProfilingOperationDimensions { get; set; }

    public DbSet<OperationProfilingMeasurementEntity> ProfilingOperationMeasurements { get; set; }

    public DbSet<ProfilingStoreStateEntity> ProfilingStoreStates { get; set; }

    public DbSet<ProfilingRuntimeGateEntity> ProfilingRuntimeGates { get; set; }

    public DbSet<ProfilingWriterEntity> ProfilingWriters { get; set; }

    public DbSet<ProfilingClearEntity> ProfilingClears { get; set; }

    public DbSet<RuntimeProfilingSessionEntity> ProfilingSessions { get; set; }

    public DbSet<RuntimeProfilingInvalidSessionEntity> ProfilingInvalidSessions { get; set; }

    public DbSet<ProfilingNodeEntity> ProfilingNodes { get; set; }

    public DbSet<RuntimeProfilingParticipationEntity> ProfilingParticipations { get; set; }

    public DbSet<RuntimeProfilingSnapshotEntity> ProfilingSnapshots { get; set; }

    public DbSet<RuntimeProfilingMetricObservationEntity> ProfilingMetricObservations { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        Interlocked.Increment(ref InstancesCreated);
        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureProfiling();
    }
}
