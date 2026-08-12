// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

using System.Text;
using System.Text.Json.Nodes;

public sealed class ProfilingArchiveServiceTests
{
    [Fact]
    public async Task ExportSessionAndImport_CompleteTerminalGraph_CreatesIndependentCopy()
    {
        // Arrange
        var sourceStore = new InMemoryProfilingStore();
        var source = await CreateSessionGraphAsync(sourceStore, terminal: true, snapshotCount: 2);
        var sourceService = CreateService(sourceStore);
        await using var archive = new MemoryStream();

        // Act
        var exportResult = await sourceService.ExportSessionAsync(
            source.Session.Identity.Key,
            archive
        );
        var json = Encoding.UTF8.GetString(archive.ToArray());
        archive.Position = 0;
        var targetStore = new InMemoryProfilingStore();
        var importResult = await CreateService(targetStore).ImportAsync(archive);

        // Assert
        exportResult.IsSuccess.ShouldBeTrue();
        importResult.IsSuccess.ShouldBeTrue();
        importResult.Value.SessionKey.ShouldNotBe(source.Session.Identity.Key);
        importResult.Value.SessionKey.Length.ShouldBe(8);
        json.ShouldContain(ProfilingArchiveFormat.Identifier);
        json.ShouldNotContain(source.Session.Identity.Id.ToString("D"));
        json.ShouldNotContain("broadcast-test-node");

        var imported = (await targetStore.GetSessionDataAsync(importResult.Value.SessionKey)).Value;
        imported.Session.Name.ShouldBe(source.Session.Name);
        imported.Session.State.ShouldBe(ProfilingSessionState.Completed);
        imported.Snapshots.Count.ShouldBe(2);
        imported.Segments.Count.ShouldBe(2);
        imported.Segments.Single(item => item.ParentSegmentId is not null)
            .ParentSegmentId.ShouldBe(imported.Segments.Single(item => item.ParentSegmentId is null).Id);
        imported.MetricObservations.Single().SegmentId.ShouldNotBeNull();
        imported.Nodes.Single().Identity.Key.ShouldBe(
            importResult.Value.NodeKeys[source.Node.Identity.Key]
        );
        imported.Snapshots.ShouldAllBe(item =>
            item.Identity.Key == importResult.Value.SnapshotKeys[
                source.Snapshots.Single(sourceSnapshot => sourceSnapshot.Sequence == item.Sequence)
                    .Identity.Key
            ]
        );
    }

    [Fact]
    public async Task ExportSnapshot_RunningSession_ImportsAsCompletedOneSnapshotSession()
    {
        // Arrange
        var sourceStore = new InMemoryProfilingStore();
        var source = await CreateSessionGraphAsync(sourceStore, terminal: false, snapshotCount: 1);
        var sourceService = CreateService(sourceStore);
        await using var archive = new MemoryStream();

        // Act
        var exportResult = await sourceService.ExportSnapshotAsync(
            source.Session.Identity.Key,
            source.Node.Identity.Key,
            source.Snapshots[0].Identity.Key,
            archive
        );
        archive.Position = 0;
        var targetStore = new InMemoryProfilingStore();
        var importResult = await CreateService(targetStore).ImportAsync(archive);

        // Assert
        exportResult.IsSuccess.ShouldBeTrue();
        importResult.IsSuccess.ShouldBeTrue();
        var imported = (await targetStore.GetSessionDataAsync(importResult.Value.SessionKey)).Value;
        imported.Session.State.ShouldBe(ProfilingSessionState.Completed);
        imported.Session.Name.ShouldContain("Imported snapshot");
        imported.Snapshots.Count.ShouldBe(1);
        imported.Nodes.Count.ShouldBe(1);
        imported.Segments.ShouldBeEmpty();
        (await targetStore.GetActiveSessionAsync()).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task Import_InvalidOrOversizedArchive_RejectsWithoutStoreMutation()
    {
        // Arrange
        var store = new InMemoryProfilingStore();
        var service = CreateService(store);
        await using var invalid = new MemoryStream(Encoding.UTF8.GetBytes("{\"format\":\"wrong\"}"));
        await using var oversized = new MemoryStream(
            new byte[ProfilingArchiveFormat.MaximumSizeBytes + 1]
        );

        // Act
        var invalidResult = await service.ImportAsync(invalid);
        var oversizedResult = await service.ImportAsync(oversized);

        // Assert
        invalidResult.IsFailure.ShouldBeTrue();
        oversizedResult.IsFailure.ShouldBeTrue();
        invalidResult.Errors.ShouldContain(error => error is ProfilingArchiveError);
        oversizedResult.Errors.ShouldContain(error => error is ProfilingArchiveError);
        (await store.ListSessionsAsync()).Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task Import_ArchiveWithUnknownProperty_RejectsWithoutStoreMutation()
    {
        // Arrange
        var sourceStore = new InMemoryProfilingStore();
        var source = await CreateSessionGraphAsync(sourceStore, terminal: true, snapshotCount: 1);
        await using var exported = new MemoryStream();
        (await CreateService(sourceStore).ExportSessionAsync(source.Session.Identity.Key, exported))
            .IsSuccess.ShouldBeTrue();
        var document = JsonNode.Parse(exported.ToArray()).AsObject();
        document["unknown"] = true;
        await using var archive = new MemoryStream(Encoding.UTF8.GetBytes(document.ToJsonString()));
        var targetStore = new InMemoryProfilingStore();

        // Act
        var result = await CreateService(targetStore).ImportAsync(archive);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(error => error is ProfilingArchiveError);
        (await targetStore.ListSessionsAsync()).Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExportSession_RunningSession_RejectsWithoutWritingDestination()
    {
        // Arrange
        var store = new InMemoryProfilingStore();
        var source = await CreateSessionGraphAsync(store, terminal: false, snapshotCount: 1);
        await using var destination = new MemoryStream();

        // Act
        var result = await CreateService(store).ExportSessionAsync(
            source.Session.Identity.Key,
            destination
        );

        // Assert
        result.IsFailure.ShouldBeTrue();
        destination.Length.ShouldBe(0);
    }

    private static ProfilingArchiveService CreateService(IProfilingStore store) =>
        new(new ProfilingOptions { Enabled = true }, store);

    private static async Task<SourceGraph> CreateSessionGraphAsync(
        IProfilingStore store,
        bool terminal,
        int snapshotCount
    )
    {
        var startedUtc = DateTimeOffset.Parse("2026-08-08T10:00:00Z");
        var session = (
            await store.GetOrCreateActiveSessionAsync(
                new(
                    ProfilingSessionIdentity.Create(),
                    "archive test",
                    startedUtc,
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(30),
                    ["local"]
                )
            )
        ).Value.Session;
        var correlation = new ProfilingNodeCorrelation("broadcast-test-node", startedUtc.AddMinutes(-1));
        var node = (
            await store.GetOrCreateNodeAsync(
                correlation,
                new()
                {
                    Identity = ProfilingNodeIdentity.Create(),
                    Correlation = correlation,
                    HostName = "test-host",
                    ProcessId = 1234,
                }
            )
        ).Value;
        await store.UpsertParticipationAsync(
            new()
            {
                SessionId = session.Identity.Id,
                SessionKey = session.Identity.Key,
                NodeId = node.Identity.Id,
                NodeKey = node.Identity.Key,
                Role = ProfilingNodeRole.ExpectedParticipant,
                State = terminal
                    ? ProfilingParticipationState.Completed
                    : ProfilingParticipationState.Collecting,
                JoinedUtc = startedUtc,
                CompletedUtc = terminal ? startedUtc.AddSeconds(5) : null,
                SuccessfulCaptureCount = snapshotCount,
            }
        );
        await store.AddRuntimeContextAsync(
            new()
            {
                SessionId = session.Identity.Id,
                SessionKey = session.Identity.Key,
                NodeId = node.Identity.Id,
                NodeKey = node.Identity.Key,
                ApplicationName = "Tests",
                RuntimeDescription = ".NET",
                ProcessStartedUtc = startedUtc.AddMinutes(-1),
            }
        );
        var snapshots = new List<ProfilingSnapshot>();
        for (var sequence = 1; sequence <= snapshotCount; sequence++)
        {
            var timestamp = startedUtc.AddSeconds(sequence);
            var snapshot = new ProfilingSnapshot
            {
                Identity = ProfilingSnapshotIdentity.Create(),
                SessionId = session.Identity.Id,
                SessionKey = session.Identity.Key,
                NodeId = node.Identity.Id,
                NodeKey = node.Identity.Key,
                TimestampUtc = timestamp,
                HostName = node.HostName,
                ProcessId = node.ProcessId,
                Sequence = sequence,
                ScheduledElapsed = TimeSpan.FromSeconds(sequence),
                CaptureStartedElapsed = TimeSpan.FromSeconds(sequence),
                CaptureDuration = TimeSpan.FromMilliseconds(2),
                CpuUsagePercent = 10 + sequence,
                ManagedMemoryBytes = 1024 * sequence,
            };
            (await store.AddSnapshotAsync(snapshot)).IsSuccess.ShouldBeTrue();
            snapshots.Add(snapshot);
        }

        if (snapshotCount > 1)
        {
            var parent = new ProfilingSegment
            {
                Id = Guid.NewGuid(),
                SessionId = session.Identity.Id,
                SessionKey = session.Identity.Key,
                NodeId = node.Identity.Id,
                NodeKey = node.Identity.Key,
                Name = "parent",
                StartedUtc = startedUtc.AddSeconds(1),
                Outcome = ProfilingSegmentOutcome.Open,
            };
            var child = parent with
            {
                Id = Guid.NewGuid(),
                Name = "child",
                ParentSegmentId = parent.Id,
            };
            (await store.UpsertSegmentAsync(parent)).IsSuccess.ShouldBeTrue();
            (await store.UpsertSegmentAsync(child)).IsSuccess.ShouldBeTrue();
            (await store.UpsertSegmentAsync(parent with
            {
                EndedUtc = startedUtc.AddSeconds(2),
                Elapsed = TimeSpan.FromSeconds(1),
                Outcome = ProfilingSegmentOutcome.Success,
            })).IsSuccess.ShouldBeTrue();
            (await store.UpsertSegmentAsync(child with
            {
                EndedUtc = startedUtc.AddSeconds(2),
                Elapsed = TimeSpan.FromSeconds(1),
                Outcome = ProfilingSegmentOutcome.Success,
            })).IsSuccess.ShouldBeTrue();
            (await store.AddMetricObservationAsync(
                new()
                {
                    Id = Guid.NewGuid(),
                    SessionId = session.Identity.Id,
                    SessionKey = session.Identity.Key,
                    NodeId = node.Identity.Id,
                    NodeKey = node.Identity.Key,
                    SegmentId = child.Id,
                    MetricIdentifier = "tests.counter",
                    Kind = ProfilingMetricKind.Counter,
                    Value = 1,
                    TimestampUtc = startedUtc.AddSeconds(2),
                }
            )).IsSuccess.ShouldBeTrue();
        }

        if (terminal)
        {
            session = (
                await store.TryTransitionSessionAsync(
                    session.Identity.Id,
                    [ProfilingSessionState.Running],
                    ProfilingSessionState.Completed,
                    startedUtc.AddSeconds(5)
                )
            ).Value;
        }

        return new(session, node, snapshots);
    }

    private sealed record SourceGraph(
        ProfilingSession Session,
        ProfilingNode Node,
        IReadOnlyList<ProfilingSnapshot> Snapshots
    );
}
