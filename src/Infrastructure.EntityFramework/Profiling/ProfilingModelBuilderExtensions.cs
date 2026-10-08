// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;

using Microsoft.EntityFrameworkCore;

/// <summary>Provides Entity Framework model configuration for durable Profiling storage.</summary>
/// <example>
/// <code>
/// protected override void OnModelCreating(ModelBuilder modelBuilder)
/// {
///     base.OnModelCreating(modelBuilder);
///     modelBuilder.ConfigureProfiling();
/// }
/// </code>
/// </example>
public static class ProfilingModelBuilderExtensions
{
    /// <summary>
    /// Configures low-volume session-owned records as JSON documents while retaining hot,
    /// high-volume, and independently addressable records as tables.
    /// </summary>
    /// <param name="modelBuilder">The application model builder.</param>
    /// <returns>The supplied model builder.</returns>
    /// <example><code>modelBuilder.ConfigureProfiling();</code></example>
    public static ModelBuilder ConfigureProfiling(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // The host's generic Set<TEntity> contract deliberately supplies no DbSet properties.
        // Register every independently queried Runtime entity explicitly, including tombstones.
        modelBuilder.Entity<RuntimeProfilingInvalidSessionEntity>();
        modelBuilder.Entity<ProfilingNodeEntity>();
        modelBuilder.Entity<RuntimeProfilingParticipationEntity>();
        modelBuilder.Entity<RuntimeProfilingSnapshotEntity>();
        modelBuilder.Entity<RuntimeProfilingMetricObservationEntity>();
        modelBuilder.Entity<RuntimeProfilingSnapshotEntity>().Property(entity => entity.TimestampUtc).HasField("timestampUtc").UsePropertyAccessMode(PropertyAccessMode.FieldDuringConstruction);
        modelBuilder.Entity<RuntimeProfilingSnapshotEntity>().HasIndex(entity => new { entity.NodeId, entity.SessionId, entity.TimestampUtcTicks });
        modelBuilder.Entity<RuntimeProfilingParticipationEntity>().Property(entity => entity.JoinedUtc).HasField("joinedUtc").UsePropertyAccessMode(PropertyAccessMode.FieldDuringConstruction);
        modelBuilder.Entity<RuntimeProfilingParticipationEntity>().Property(entity => entity.CompletedUtc).HasField("completedUtc").UsePropertyAccessMode(PropertyAccessMode.FieldDuringConstruction);
        modelBuilder.Entity<RuntimeProfilingParticipationEntity>().HasIndex(entity => new { entity.NodeId, entity.JoinedUtcTicks, entity.CompletedUtcTicks });

        var session = modelBuilder.Entity<RuntimeProfilingSessionEntity>();
        session.HasIndex(entity => new { entity.State, entity.CompletionUtcTicks });

        session.OwnsMany(
            x => x.Tags,
            owned =>
            {
                owned.ToJson("Tags");
                owned.WithOwner().HasForeignKey(x => x.SessionId);
            }
        );

        session.OwnsMany(
            x => x.RuntimeContexts,
            owned =>
            {
                owned.ToJson("RuntimeContexts");
                owned.WithOwner().HasForeignKey(x => x.SessionId);
            }
        );

        session.OwnsMany(
            x => x.Markers,
            owned =>
            {
                owned.ToJson("Markers");
                owned.WithOwner().HasForeignKey(x => x.SessionId);
            }
        );

        session.OwnsMany(
            x => x.Segments,
            owned =>
            {
                owned.ToJson("Segments");
                owned.WithOwner().HasForeignKey(x => x.SessionId);
                owned.OwnsMany(x => x.Tags);
            }
        );

        ConfigureOperations(modelBuilder);
        ConfigureCoordination(modelBuilder);
        return modelBuilder;
    }
    private static void ConfigureOperations(ModelBuilder modelBuilder)
    {
        var operation = modelBuilder.Entity<OperationProfilingEntity>();
        operation.ToTable("__Profiling_Operations");
        operation.HasKey(entity => entity.Id);
        operation.Property(entity => entity.CanonicalIdBytes).IsRequired().HasMaxLength(32);
        operation.Property(entity => entity.Key).IsRequired();
        operation.Property(entity => entity.NodeKey).IsRequired().HasMaxLength(8);
        operation.Property(entity => entity.Kind).IsRequired();
        operation.Property(entity => entity.KeyBytes).IsRequired();
        operation.Property(entity => entity.KindBytes).IsRequired();
        operation.Property(entity => entity.BaseGroupBytes).IsRequired();
        operation.Property(entity => entity.KeyHash).IsRequired().HasMaxLength(32);
        operation.Property(entity => entity.KindHash).IsRequired().HasMaxLength(32);
        operation.Property(entity => entity.HttpRouteHash).HasMaxLength(32);
        operation.Property(entity => entity.HttpMethodBytes).HasMaxLength(256);
        operation.Property(entity => entity.RecordJson).IsRequired();
        operation.HasOne(entity => entity.Node).WithMany().HasForeignKey(entity => entity.NodeId).OnDelete(DeleteBehavior.Restrict);
        operation.HasIndex(entity => new { entity.WriterId, entity.CompletionSequence }).IsUnique();
        operation.HasIndex(entity => new { entity.CompletedUtcTicks, entity.CanonicalIdBytes });
        operation.HasIndex(entity => new { entity.DurationTicks, entity.CanonicalIdBytes });
        operation.HasIndex(entity => new { entity.NodeId, entity.StartedUtcTicks });
        operation.HasIndex(entity => new { entity.KindHash, entity.KeyHash, entity.CompletedUtcTicks });
        operation.HasIndex(entity => new { entity.HttpRouteHash, entity.HttpStatusCode, entity.CompletedUtcTicks });
        operation.HasIndex(entity => entity.CommitWatermark).IsUnique();
        operation.HasIndex(entity => new { entity.WriterGeneration, entity.WriterId, entity.CompletionSequence });

        var segment = modelBuilder.Entity<OperationProfilingSegmentEntity>();
        segment.ToTable("__Profiling_OperationSegments");
        segment.HasKey(entity => entity.Id);
        segment.Property(entity => entity.Key).IsRequired();
        segment.Property(entity => entity.KeyBytes).IsRequired();
        segment.Property(entity => entity.KeyHash).IsRequired().HasMaxLength(32);
        segment.Property(entity => entity.PathBytes).IsRequired();
        segment.Property(entity => entity.PathHash).IsRequired().HasMaxLength(32);
        segment.Property(entity => entity.ParentPathHash).HasMaxLength(32);
        segment.Property(entity => entity.SummaryJson).IsRequired();
        segment.HasOne(entity => entity.Operation).WithMany(entity => entity.Segments).HasForeignKey(entity => entity.OperationId).OnDelete(DeleteBehavior.Cascade);
        segment.HasIndex(entity => new { entity.OperationId, entity.PathHash }).IsUnique();
        segment.HasIndex(entity => new { entity.KeyHash, entity.OperationId });
        segment.HasIndex(entity => new { entity.OperationId, entity.ParentPathHash });

        var dimension = modelBuilder.Entity<OperationProfilingDimensionEntity>();
        dimension.ToTable("__Profiling_OperationDimensions");
        dimension.HasKey(entity => entity.Id);
        dimension.Property(entity => entity.Key).IsRequired();
        dimension.Property(entity => entity.KeyBytes).IsRequired();
        dimension.Property(entity => entity.KeyHash).IsRequired().HasMaxLength(32);
        dimension.Property(entity => entity.ScopeHash).IsRequired().HasMaxLength(32);
        dimension.HasOne(entity => entity.Operation).WithMany(entity => entity.Dimensions).HasForeignKey(entity => entity.OperationId).OnDelete(DeleteBehavior.Cascade);
        dimension.HasOne(entity => entity.Segment).WithMany().HasForeignKey(entity => entity.SegmentId).OnDelete(DeleteBehavior.NoAction);
        dimension.HasIndex(entity => new { entity.OperationId, entity.ScopeHash, entity.KeyHash }).IsUnique();
        dimension.HasIndex(entity => new { entity.KeyHash, entity.ValueType, entity.OperationId });
        dimension.HasIndex(entity => new { entity.SegmentId, entity.KeyHash, entity.ValueType });
    }

    private static void ConfigureCoordination(ModelBuilder modelBuilder)
    {
        var state = modelBuilder.Entity<ProfilingStoreStateEntity>();
        state.ToTable("__Profiling_StoreState");
        state.HasKey(entity => entity.Id);
        state.Property(entity => entity.Id).ValueGeneratedNever();
        state.Property(entity => entity.QuerySecret).IsRequired().HasMaxLength(32);
        state.Property(entity => entity.ConcurrencyVersion).IsConcurrencyToken();

        var runtimeGate = modelBuilder.Entity<ProfilingRuntimeGateEntity>();
        runtimeGate.ToTable("__Profiling_RuntimeGate");
        runtimeGate.HasKey(entity => entity.Id);
        runtimeGate.Property(entity => entity.Id).ValueGeneratedNever();
        runtimeGate.Property(entity => entity.ConcurrencyVersion).IsConcurrencyToken();

        var writer = modelBuilder.Entity<ProfilingWriterEntity>();
        writer.ToTable("__Profiling_Writers");
        writer.HasKey(entity => entity.Id);
        writer.HasIndex(entity => entity.AttemptId).IsUnique();
        writer.HasIndex(entity => entity.Token).IsUnique();
        writer.HasIndex(entity => new { entity.ExpiresUtcTicks, entity.Retired });
        writer.HasOne(entity => entity.Node).WithMany().HasForeignKey(entity => entity.NodeId).OnDelete(DeleteBehavior.Restrict);

        var clear = modelBuilder.Entity<ProfilingClearEntity>();
        clear.ToTable("__Profiling_Clears");
        clear.HasKey(entity => entity.Id);
        clear.Property(entity => entity.EligibleWritersJson).IsRequired();
        clear.Property(entity => entity.AcknowledgementsJson).IsRequired();
        clear.HasIndex(entity => new { entity.State, entity.DeadlineUtcTicks });
        clear.HasIndex(entity => new { entity.GenerationBoundary, entity.SealedUtcTicks });
    }
}
