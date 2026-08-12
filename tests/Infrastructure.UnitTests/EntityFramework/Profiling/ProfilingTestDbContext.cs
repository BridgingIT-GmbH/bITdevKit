// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Profiling;

using BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;
using Microsoft.EntityFrameworkCore;

public sealed class ProfilingTestDbContext(DbContextOptions<ProfilingTestDbContext> options)
    : DbContext(options),
        IProfilingContext
{
    public static int InstancesCreated;

    public DbSet<ProfilingSessionEntity> ProfilingSessions { get; set; }

    public DbSet<ProfilingInvalidSessionEntity> ProfilingInvalidSessions { get; set; }

    public DbSet<ProfilingNodeEntity> ProfilingNodes { get; set; }

    public DbSet<ProfilingParticipationEntity> ProfilingParticipations { get; set; }

    public DbSet<ProfilingSnapshotEntity> ProfilingSnapshots { get; set; }

    public DbSet<ProfilingMetricObservationEntity> ProfilingMetricObservations { get; set; }

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
