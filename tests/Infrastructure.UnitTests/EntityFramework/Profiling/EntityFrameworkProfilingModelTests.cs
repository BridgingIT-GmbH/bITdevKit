// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Profiling;

using BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public sealed class EntityFrameworkProfilingModelTests
{
    [Fact]
    public void Model_ProfilingAggregates_HaveIndependentTablesAndRuntimeOwnedDocuments()
    {
        // Arrange
        using var context = CreateContext();
        var model = context.Model;
        var profilingTypes = model
            .GetEntityTypes()
            .Where(type => type.ClrType.Namespace?.EndsWith(".Profiling") == true)
            .ToArray();

        // Act
        var session = model.FindEntityType(typeof(RuntimeProfilingSessionEntity));
        var participation = model.FindEntityType(typeof(RuntimeProfilingParticipationEntity));
        var segment = model.FindEntityType(typeof(RuntimeProfilingSegmentEntity));
        var snapshot = model.FindEntityType(typeof(RuntimeProfilingSnapshotEntity));
        var runtimeContext = model.FindEntityType(typeof(RuntimeProfilingRuntimeContextEntity));
        var marker = model.FindEntityType(typeof(ProfilingMarkerEntity));
        var observation = model.FindEntityType(typeof(RuntimeProfilingMetricObservationEntity));
        var sessionTag = model.FindEntityType(typeof(RuntimeProfilingSessionTagEntity));
        var segmentTag = model.FindEntityType(typeof(RuntimeProfilingSegmentTagEntity));

        // Assert
        profilingTypes.ShouldNotBeEmpty();
        profilingTypes
            .Select(type => type.GetTableName())
            .Where(name => name?.StartsWith("__Profiling_", StringComparison.Ordinal) == true)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ShouldBe(
                [
                    "__Profiling_Clears",
                    "__Profiling_Nodes",
                    "__Profiling_OperationDimensions",
                    "__Profiling_OperationMeasurements",
                    "__Profiling_OperationSegments",
                    "__Profiling_Operations",
                    "__Profiling_RuntimeGate",
                    "__Profiling_RuntimeInvalidSessions",
                    "__Profiling_RuntimeMetricObservations",
                    "__Profiling_RuntimeParticipations",
                    "__Profiling_RuntimeSessions",
                    "__Profiling_RuntimeSnapshots",
                    "__Profiling_StoreState",
                    "__Profiling_Writers"
                ]
            );
        session
            .GetIndexes()
            .Single(index =>
                index.Properties.Select(property => property.Name).SequenceEqual(["LifecycleKey"])
            )
            .IsUnique.ShouldBeTrue();
        session.FindProperty("Key").GetMaxLength().ShouldBe(8);
        session.FindProperty("ConcurrencyVersion").IsConcurrencyToken.ShouldBeTrue();
        sessionTag.IsOwned().ShouldBeTrue();
        sessionTag.GetContainerColumnName().ShouldBe("Tags");
        participation.IsOwned().ShouldBeFalse();
        participation.FindProperty("ConcurrencyVersion").IsConcurrencyToken.ShouldBeTrue();
        runtimeContext.IsOwned().ShouldBeTrue();
        runtimeContext.GetContainerColumnName().ShouldBe("RuntimeContexts");
        marker.IsOwned().ShouldBeTrue();
        marker.GetContainerColumnName().ShouldBe("Markers");
        segment.IsOwned().ShouldBeTrue();
        segment.GetContainerColumnName().ShouldBe("Segments");
        segmentTag.IsOwned().ShouldBeTrue();
        segmentTag.GetContainerColumnName().ShouldBe("Segments");
        observation.IsOwned().ShouldBeFalse();
        snapshot
            .GetIndexes()
            .Single(index =>
                index
                    .Properties.Select(property => property.Name)
                    .SequenceEqual(["SessionId", "NodeId", "Sequence"])
            )
            .IsUnique.ShouldBeTrue();
    }

    [Fact]
    public void Model_SessionOwnedRowsCascadeWhileNodesRemainRestricted()
    {
        // Arrange
        using var context = CreateContext();
        var model = context.Model;

        // Act
        var snapshot = model.FindEntityType(typeof(RuntimeProfilingSnapshotEntity));
        var sessionForeignKey = snapshot
            .GetForeignKeys()
            .Single(key => key.PrincipalEntityType.ClrType == typeof(RuntimeProfilingSessionEntity));
        var nodeForeignKey = snapshot
            .GetForeignKeys()
            .Single(key => key.PrincipalEntityType.ClrType == typeof(ProfilingNodeEntity));

        // Assert
        sessionForeignKey.DeleteBehavior.ShouldBe(DeleteBehavior.Cascade);
        nodeForeignKey.DeleteBehavior.ShouldBe(DeleteBehavior.Restrict);
    }

    [Fact]
    public async Task Store_UsesOperationOwnedContexts_FromSingletonRegistration()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var services = new ServiceCollection();
        services.AddDbContext<ProfilingTestDbContext>(options => options.UseSqlite(connection));
        services
            .AddProfiling(options => options.Enabled()).WithRuntimeProfiling()
            .WithEntityFrameworkProvider<ProfilingTestDbContext>();
        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true }
        );
        using (var scope = provider.CreateScope())
        {
            scope
                .ServiceProvider.GetRequiredService<ProfilingTestDbContext>()
                .Database.EnsureCreated();
        }

        var store = provider.GetRequiredService<IRuntimeProfilingStore>();
        var before = Volatile.Read(ref ProfilingTestDbContext.InstancesCreated);

        // Act
        await store.ListSessionsAsync();
        await store.ListSessionsAsync();

        // Assert
        (
            Volatile.Read(ref ProfilingTestDbContext.InstancesCreated) - before
        ).ShouldBeGreaterThanOrEqualTo(2);
        provider.GetRequiredService<IRuntimeProfilingStore>().ShouldBeSameAs(store);
    }

    [Fact]
    public void Registration_ConflictingProvider_Throws()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IRuntimeProfilingStore>());
        var builder = services.AddProfiling(options => options.Enabled()).WithRuntimeProfiling();

        // Act
        var action = () => builder.WithEntityFrameworkProvider<ProfilingTestDbContext>();

        // Assert
        action
            .ShouldThrow<InvalidOperationException>()
            .Message.ShouldContain("different profiling store provider");
    }

    private static ProfilingTestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ProfilingTestDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        return new ProfilingTestDbContext(options);
    }
}
