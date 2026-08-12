// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

using System.Text;
using System.Text.Json;

public sealed class ProfilingPerfettoExportServiceTests
{
    [Fact]
    public async Task ExportSessionAsync_CompleteTerminalEvidence_WritesPerfettoTraceEvents()
    {
        // Arrange
        var data = CreateSessionData(ProfilingSessionState.Completed);
        var store = Substitute.For<IProfilingStore>();
        store
            .GetSessionDataAsync("sess0001", Arg.Any<CancellationToken>())
            .Returns(Result<ProfilingSessionData>.Success(data));
        var service = new ProfilingPerfettoExportService(
            new ProfilingOptions { Enabled = true },
            store
        );
        await using var destination = new MemoryStream();

        // Act
        var result = await service.ExportSessionAsync("sess0001", destination);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        destination.CanWrite.ShouldBeTrue();
        var json = Encoding.UTF8.GetString(destination.ToArray());
        json.ShouldNotContain(data.Session.Identity.Id.ToString("D"));
        json.ShouldNotContain(data.Nodes[0].Identity.Id.ToString("D"));

        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("displayTimeUnit").GetString().ShouldBe("ms");
        var events = document.RootElement.GetProperty("traceEvents").EnumerateArray().ToArray();
        events.ShouldContain(item =>
            GetString(item, "ph") == "M"
            && GetString(item, "name") == "process_name"
            && item.GetProperty("args").GetProperty("name").GetString()
                == "test-host · PID 1234 · node0001"
        );

        var cpu = events.Single(item =>
            GetString(item, "ph") == "C" && GetString(item, "name") == "CPU usage"
        );
        cpu.GetProperty("ts").GetInt64().ShouldBe(1_500_000);
        cpu.GetProperty("args").GetProperty("percent").GetDouble().ShouldBe(42.5);
        events.ShouldContain(item =>
            GetString(item, "ph") == "i"
            && GetString(item, "cat") == "profiling.phase"
            && GetString(item, "name") == "workload"
        );
        events.ShouldContain(item =>
            GetString(item, "ph") == "i"
            && GetString(item, "cat") == "profiling.action"
            && GetString(item, "name") == "GC requested"
        );

        var segment = events.Single(item =>
            GetString(item, "ph") == "X" && GetString(item, "name") == "load customers"
        );
        segment.GetProperty("dur").GetInt64().ShouldBe(1_500_000);
        segment.GetProperty("args").GetProperty("outcome").GetString().ShouldBe("Success");
        events.ShouldContain(item =>
            GetString(item, "ph") == "C"
            && GetString(item, "name") == "devkit.jobs.completed (jobs)"
            && item.GetProperty("args").GetProperty("value").GetDouble() == 7
        );
    }

    [Fact]
    public async Task ExportSessionAsync_RunningSession_RejectsWithoutWritingDestination()
    {
        // Arrange
        var store = Substitute.For<IProfilingStore>();
        store
            .GetSessionDataAsync("sess0001", Arg.Any<CancellationToken>())
            .Returns(
                Result<ProfilingSessionData>.Success(
                    CreateSessionData(ProfilingSessionState.Running)
                )
            );
        var service = new ProfilingPerfettoExportService(
            new ProfilingOptions { Enabled = true },
            store
        );
        await using var destination = new MemoryStream();

        // Act
        var result = await service.ExportSessionAsync("sess0001", destination);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(error => error is ProfilingInvalidStateError);
        destination.Length.ShouldBe(0);
    }

    private static ProfilingSessionData CreateSessionData(ProfilingSessionState state)
    {
        var startedUtc = DateTimeOffset.Parse("2026-08-11T10:00:00Z");
        var sessionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var nodeId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var segmentId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var session = new ProfilingSession
        {
            Identity = new(sessionId, "sess0001"),
            Name = "comparison run",
            State = state,
            StartedUtc = startedUtc,
            EndsUtc = startedUtc.AddSeconds(30),
            CompletedUtc = state == ProfilingSessionState.Running
                ? null
                : startedUtc.AddSeconds(3),
            SamplingInterval = TimeSpan.FromSeconds(1),
            Duration = TimeSpan.FromSeconds(30),
            Tags = ["local"],
        };
        var node = new ProfilingNode
        {
            Identity = new(nodeId, "node0001"),
            HostName = "test-host",
            ProcessId = 1234,
        };

        return new ProfilingSessionData
        {
            Session = session,
            Nodes = [node],
            RuntimeContexts =
            [
                new ProfilingRuntimeContext
                {
                    SessionId = sessionId,
                    SessionKey = session.Identity.Key,
                    NodeId = nodeId,
                    NodeKey = node.Identity.Key,
                    ApplicationName = "Tests",
                    RuntimeDescription = ".NET 10",
                    ProcessStartedUtc = startedUtc.AddMinutes(-1),
                },
            ],
            Snapshots =
            [
                new ProfilingSnapshot
                {
                    Identity = new(
                        Guid.Parse("44444444-4444-4444-4444-444444444444"),
                        "snap0001"
                    ),
                    SessionId = sessionId,
                    SessionKey = session.Identity.Key,
                    NodeId = nodeId,
                    NodeKey = node.Identity.Key,
                    TimestampUtc = startedUtc.AddMilliseconds(1500),
                    HostName = node.HostName,
                    ProcessId = node.ProcessId,
                    Sequence = 1,
                    ScheduledElapsed = TimeSpan.FromSeconds(1),
                    CaptureStartedElapsed = TimeSpan.FromMilliseconds(1100),
                    CaptureDuration = TimeSpan.FromMilliseconds(4),
                    CpuUsagePercent = 42.5,
                    ManagedMemoryBytes = 8 * 1024 * 1024,
                    AllocationRateBytesPerSecond = 2 * 1024 * 1024,
                    Gen0CollectionCount = 3,
                },
            ],
            PhaseMarkers =
            [
                new(
                    Guid.Parse("55555555-5555-5555-5555-555555555555"),
                    sessionId,
                    session.Identity.Key,
                    "workload",
                    startedUtc.AddMilliseconds(500)
                ),
            ],
            ActionMarkers =
            [
                new(
                    Guid.Parse("66666666-6666-6666-6666-666666666666"),
                    sessionId,
                    nodeId,
                    session.Identity.Key,
                    node.Identity.Key,
                    "GC requested",
                    startedUtc.AddSeconds(1)
                ),
            ],
            Segments =
            [
                new ProfilingSegment
                {
                    Id = segmentId,
                    SessionId = sessionId,
                    SessionKey = session.Identity.Key,
                    NodeId = nodeId,
                    NodeKey = node.Identity.Key,
                    Name = "load customers",
                    StartedUtc = startedUtc.AddMilliseconds(750),
                    EndedUtc = startedUtc.AddMilliseconds(2250),
                    Elapsed = TimeSpan.FromMilliseconds(1500),
                    Outcome = ProfilingSegmentOutcome.Success,
                },
            ],
            MetricObservations =
            [
                new ProfilingMetricObservation
                {
                    Id = Guid.Parse("77777777-7777-7777-7777-777777777777"),
                    SessionId = sessionId,
                    SessionKey = session.Identity.Key,
                    NodeId = nodeId,
                    NodeKey = node.Identity.Key,
                    SegmentId = segmentId,
                    MetricIdentifier = "devkit.jobs.completed",
                    Kind = ProfilingMetricKind.Counter,
                    Value = 7,
                    Unit = "jobs",
                    TimestampUtc = startedUtc.AddSeconds(2),
                },
            ],
        };
    }

    private static string GetString(JsonElement element, string propertyName) =>
        element.GetProperty(propertyName).GetString();
}