// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

public class InMemoryProfilingStoreTests
{
    [Fact]
    public async Task GetOrCreateNodeAsync_SameBroadcastProcess_ReturnsStableProfilingNode()
    {
        // Arrange
        var sut = new InMemoryProfilingStore();
        var processStartedUtc = new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);
        var correlation = new ProfilingNodeCorrelation("node-a", processStartedUtc);
        var firstProposal = CreateNode(correlation);
        var secondProposal = CreateNode(correlation);

        // Act
        var first = await sut.GetOrCreateNodeAsync(correlation, firstProposal);
        var second = await sut.GetOrCreateNodeAsync(correlation, secondProposal);
        var restartedCorrelation = correlation with
        {
            ProcessStartedUtc = processStartedUtc.AddMinutes(1),
        };
        var restarted = await sut.GetOrCreateNodeAsync(
            restartedCorrelation,
            CreateNode(restartedCorrelation)
        );

        // Assert
        first.Value.Identity.ShouldBe(second.Value.Identity);
        first.Value.Identity.ShouldNotBe(restarted.Value.Identity);
    }

    [Fact]
    public async Task PhaseMarkerAndStopAsync_CompetingMutations_RemainAtomic()
    {
        // Arrange
        var sut = new InMemoryProfilingStore();
        var startedUtc = new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);
        var session = (await sut.GetOrCreateActiveSessionAsync(CreateSessionRequest(startedUtc)))
            .Value
            .Session;
        using var barrier = new Barrier(2);

        // Act
        var markerTask = Task.Run(async () =>
        {
            barrier.SignalAndWait();
            return await sut.AddPhaseMarkerAsync(
                new(
                    Guid.NewGuid(),
                    session.Identity.Id,
                    session.Identity.Key,
                    "race",
                    startedUtc.AddSeconds(1)
                )
            );
        });
        var stopTask = Task.Run(async () =>
        {
            barrier.SignalAndWait();
            return await sut.TryTransitionSessionAsync(
                session.Identity.Id,
                [ProfilingSessionState.Running],
                ProfilingSessionState.Stopped,
                startedUtc.AddSeconds(1)
            );
        });
        await Task.WhenAll(markerTask, stopTask);
        var markerResult = await markerTask;
        var stopResult = await stopTask;
        var data = (await sut.GetSessionDataAsync(session.Identity.Key)).Value;

        // Assert
        stopResult.IsSuccess.ShouldBeTrue();
        data.Session.State.ShouldBe(ProfilingSessionState.Stopped);
        if (markerResult.IsSuccess)
        {
            data.PhaseMarkers.Count.ShouldBe(1);
        }
        else
        {
            data.PhaseMarkers.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task StartAndClearAsync_CompetingMutations_LeaveOneCompleteActiveSession()
    {
        // Arrange
        var sut = new InMemoryProfilingStore();
        var request = CreateSessionRequest(new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.Zero));
        using var barrier = new Barrier(2);

        // Act
        var startTask = Task.Run(async () =>
        {
            barrier.SignalAndWait();
            return await sut.GetOrCreateActiveSessionAsync(request);
        });
        var clearTask = Task.Run(async () =>
        {
            barrier.SignalAndWait();
            return await sut.ClearAsync();
        });
        await Task.WhenAll(startTask, clearTask);
        var startResult = await startTask;
        var clearResult = await clearTask;
        var sessions = (await sut.ListSessionsAsync()).Value;

        // Assert
        startResult.IsSuccess.ShouldBeTrue();
        sessions.ShouldHaveSingleItem().State.ShouldBe(ProfilingSessionState.Running);
        if (clearResult.IsSuccess)
        {
            clearResult.Value.ShouldBe(new ProfilingClearResult(0, 0));
        }
        else
        {
            clearResult.Errors.ShouldContain(error => error is ProfilingInvalidStateError);
        }
    }

    [Fact]
    public async Task SnapshotAfterStoppedSession_InsideOriginalWindow_IsAccepted()
    {
        // Arrange
        var sut = new InMemoryProfilingStore();
        var startedUtc = new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);
        var session = (await sut.GetOrCreateActiveSessionAsync(CreateSessionRequest(startedUtc)))
            .Value
            .Session;
        var node = (
            await sut.GetOrCreateNodeAsync(
                new("node-a", startedUtc),
                CreateNode(new("node-a", startedUtc))
            )
        ).Value;
        await sut.TryTransitionSessionAsync(
            session.Identity.Id,
            [ProfilingSessionState.Running],
            ProfilingSessionState.Stopped,
            startedUtc.AddSeconds(2)
        );

        // Act
        var inside = await sut.AddSnapshotAsync(
            CreateSnapshot(session, node, startedUtc.AddSeconds(3), 1)
        );
        var outside = await sut.AddSnapshotAsync(
            CreateSnapshot(session, node, session.EndsUtc.AddMilliseconds(1), 2)
        );

        // Assert
        inside.IsSuccess.ShouldBeTrue();
        outside.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task ClearAndSnapshotAsync_CompetingMutations_CannotLeaveOrphanedData()
    {
        // Arrange
        var sut = new InMemoryProfilingStore();
        var startedUtc = new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);
        var session = (await sut.GetOrCreateActiveSessionAsync(CreateSessionRequest(startedUtc)))
            .Value
            .Session;
        var correlation = new ProfilingNodeCorrelation("node-a", startedUtc);
        var node = (await sut.GetOrCreateNodeAsync(correlation, CreateNode(correlation))).Value;
        await sut.TryTransitionSessionAsync(
            session.Identity.Id,
            [ProfilingSessionState.Running],
            ProfilingSessionState.Stopped,
            startedUtc.AddSeconds(2)
        );
        using var barrier = new Barrier(2);

        // Act
        var writeTask = Task.Run(async () =>
        {
            barrier.SignalAndWait();
            return await sut.AddSnapshotAsync(
                CreateSnapshot(session, node, startedUtc.AddSeconds(3), 1)
            );
        });
        var clearTask = Task.Run(async () =>
        {
            barrier.SignalAndWait();
            return await sut.ClearAsync();
        });
        await Task.WhenAll(writeTask, clearTask);
        var writeResult = await writeTask;
        var clearResult = await clearTask;

        // Assert
        clearResult.IsSuccess.ShouldBeTrue();
        (await sut.ListSessionsAsync()).Value.ShouldBeEmpty();
        (await sut.GetSessionDataAsync(session.Identity.Key)).IsFailure.ShouldBeTrue();
        if (writeResult.IsSuccess)
        {
            clearResult.Value.RemovedSnapshotCount.ShouldBe(1);
        }
        else
        {
            clearResult.Value.RemovedSnapshotCount.ShouldBe(0);
        }
    }

    private static ProfilingSessionCreateRequest CreateSessionRequest(DateTimeOffset startedUtc) =>
        new(
            ProfilingSessionIdentity.Create(),
            "session",
            startedUtc,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(30),
            []
        );

    private static ProfilingNode CreateNode(ProfilingNodeCorrelation correlation) =>
        new()
        {
            Identity = ProfilingNodeIdentity.Create(),
            Correlation = correlation,
            HostName = "host",
            ProcessId = 1234,
        };

    private static ProfilingSnapshot CreateSnapshot(
        ProfilingSession session,
        ProfilingNode node,
        DateTimeOffset timestampUtc,
        long sequence
    ) =>
        new()
        {
            Identity = ProfilingSnapshotIdentity.Create(),
            SessionId = session.Identity.Id,
            SessionKey = session.Identity.Key,
            NodeId = node.Identity.Id,
            NodeKey = node.Identity.Key,
            TimestampUtc = timestampUtc,
            Sequence = sequence,
            HostName = node.HostName,
            ProcessId = node.ProcessId,
            ScheduledElapsed = timestampUtc - session.StartedUtc,
            CaptureStartedElapsed = timestampUtc - session.StartedUtc,
        };
}
