// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Profiling;

using BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

public sealed class EntityFrameworkOperationProfilingModelTests
{
    /// <summary>The model is complete without any host DbSet properties or convention-based entity discovery.</summary>
    [Fact]
    public void Model_GenericContextContract_RegistersEveryRetainedEntityExplicitly()
    {
        using var sut = Create();
        Type[] entities =
        [
            typeof(RuntimeProfilingSessionEntity), typeof(RuntimeProfilingInvalidSessionEntity), typeof(ProfilingNodeEntity),
            typeof(RuntimeProfilingParticipationEntity), typeof(RuntimeProfilingSnapshotEntity), typeof(RuntimeProfilingMetricObservationEntity),
            typeof(OperationProfilingEntity), typeof(OperationProfilingSegmentEntity), typeof(OperationProfilingDimensionEntity),
            typeof(ProfilingStoreStateEntity), typeof(ProfilingWriterEntity), typeof(ProfilingClearEntity), typeof(ProfilingRuntimeGateEntity),
        ];
        foreach (var entity in entities) { sut.Model.FindEntityType(entity).ShouldNotBeNull(); }

        typeof(ProfilingTestDbContext).GetProperties().ShouldNotContain(property => property.PropertyType.IsGenericType
            && property.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>));
    }

    [Fact]
    public void Model_Operations_HavePortableIdentityAndPublicationIndexes()
    {
        // Arrange
        using var sut = Create();
        var root = sut.Model.FindEntityType(typeof(OperationProfilingEntity));
        var summary = sut.Model.FindEntityType(typeof(OperationProfilingSegmentEntity));
        var dimension = sut.Model.FindEntityType(typeof(OperationProfilingDimensionEntity));

        // Act/Assert
        root.IsOwned().ShouldBeFalse();
        root.FindProperty(nameof(OperationProfilingEntity.KeyBytes)).ClrType.ShouldBe(typeof(byte[]));
        root.FindProperty(nameof(OperationProfilingEntity.CanonicalIdBytes)).GetMaxLength().ShouldBe(32);
        root.GetIndexes().ShouldContain(index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(new[] { "WriterId", "CompletionSequence" }));
        root.GetIndexes().ShouldContain(index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(new[] { "CommitWatermark" }));
        summary.GetIndexes().ShouldContain(index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(new[] { "OperationId", "PathHash" }));
        dimension.GetIndexes().ShouldContain(index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(new[] { "OperationId", "ScopeHash", "KeyHash" }));
        dimension.FindProperty(nameof(OperationProfilingDimensionEntity.ValueScalar)).ClrType.ShouldBe(typeof(string));
        dimension.FindProperty(nameof(OperationProfilingDimensionEntity.ValueType)).ClrType.ShouldBe(typeof(ProfilingValueType?));
    }

    [Fact]
    public void Model_Coordination_UsesBoundedOriginalIdentitiesAndTransactionalEpochState()
    {
        // Arrange
        using var sut = Create();
        var state = sut.Model.FindEntityType(typeof(ProfilingStoreStateEntity));
        var writer = sut.Model.FindEntityType(typeof(ProfilingWriterEntity));

        // Act/Assert
        state.FindProperty(nameof(ProfilingStoreStateEntity.ConcurrencyVersion)).IsConcurrencyToken.ShouldBeTrue();
        state.FindProperty(nameof(ProfilingStoreStateEntity.QuerySecret)).GetMaxLength().ShouldBe(32);
        writer.GetIndexes().ShouldContain(index => index.IsUnique && index.Properties.Single().Name == nameof(ProfilingWriterEntity.AttemptId));
        writer.GetForeignKeys().Single().DeleteBehavior.ShouldBe(DeleteBehavior.Restrict);
        sut.Model.FindEntityType(typeof(ProfilingNodeEntity)).FindProperty(nameof(ProfilingNodeEntity.BroadcastNodeIdentity)).IsNullable.ShouldBeTrue();
    }

    [Fact]
    public void MigrationScript_OperationsAndCoordination_AreHostOwnedExplicitSchema()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ProfilingTestDbContext>();
        options.UseSqlite("Data Source=:memory:");

        using var sut = new ProfilingTestDbContext(options.Options);

        // Act
        var script = sut.Database.GenerateCreateScript();

        // Assert
        script.ShouldContain("__Profiling_Operations");
        script.ShouldContain("__Profiling_OperationSegments");
        script.ShouldContain("__Profiling_OperationDimensions");
        script.ShouldContain("__Profiling_Writers");
        script.ShouldContain("__Profiling_Clears");
        script.ShouldContain("__Profiling_StoreState");
        script.ShouldContain("__Profiling_RuntimeSessions");
        script.ShouldNotContain("__Profiling_Sessions");
        script.ShouldNotContain("__Profiling_OperationMeasurements");
    }

    [Fact]
    public async Task DatabaseCascade_WholeOperationGraph_IsAtomicAndKeepsSharedNode()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var sut = new ProfilingTestDbContext(new DbContextOptionsBuilder<ProfilingTestDbContext>().UseSqlite(connection).Options);
        await sut.Database.EnsureCreatedAsync();
        var node = new ProfilingNodeEntity { Id = Guid.NewGuid(), Key = "node0001", ProcessId = 1 };
        var root = new OperationProfilingEntity
        {
            Id = Guid.NewGuid(), Node = node, NodeId = node.Id, NodeKey = node.Key, Key = "Report", Kind = "Service", CanonicalIdBytes = new byte[32],
            KeyBytes = [1], KindBytes = [2], KeyHash = new byte[32], KindHash = new byte[32], BaseGroupBytes = [3], RecordJson = "{}", CommitWatermark = 1,
        };
        var segment = new OperationProfilingSegmentEntity { Id = Guid.NewGuid(), Key = "Read", KeyBytes = [1], KeyHash = new byte[32], PathBytes = [1], PathHash = new byte[32], SummaryJson = "{}" };
        root.Segments.Add(segment);
        root.Dimensions.Add(new() { Id = Guid.NewGuid(), Key = "tenant", KeyBytes = [1], KeyHash = new byte[32], ScopeHash = new byte[32], Segment = segment });
        sut.Set<OperationProfilingEntity>().Add(root);
        await sut.SaveChangesAsync();
        sut.ChangeTracker.Clear();

        // Act
        var deleted = await sut.Set<OperationProfilingEntity>().Where(operation => operation.Id == root.Id).ExecuteDeleteAsync();

        // Assert
        deleted.ShouldBe(1);
        (await sut.Set<OperationProfilingSegmentEntity>().CountAsync()).ShouldBe(0);
        (await sut.Set<OperationProfilingDimensionEntity>().CountAsync()).ShouldBe(0);
        (await sut.Set<ProfilingNodeEntity>().CountAsync()).ShouldBe(1);
    }

    private static ProfilingTestDbContext Create() => new(new DbContextOptionsBuilder<ProfilingTestDbContext>().UseSqlite("Data Source=:memory:").Options);
}
