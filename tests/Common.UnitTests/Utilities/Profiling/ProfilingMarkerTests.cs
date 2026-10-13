// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

using System.Text.Json;

public sealed class ProfilingMarkerTests
{
    [Fact]
    public void SessionMarker_Json_HasNoNodeAndWritesUtc()
    {
        // Arrange
        var marker = new ProfilingMarker(Guid.NewGuid(), Guid.NewGuid(), "session1", "Baseline", DateTimeOffset.Parse("2026-10-08T10:00:00Z"));

        // Act
        var json = JsonSerializer.Serialize(marker);
        var copy = JsonSerializer.Deserialize<ProfilingMarker>(json);

        // Assert
        marker.Scope.ShouldBe(ProfilingMarkerScope.Session);
        marker.NodeId.ShouldBeNull();
        marker.NodeKey.ShouldBeNull();
        copy.NodeId.ShouldBeNull();
        copy.NodeKey.ShouldBeNull();
        copy.Kind.ShouldBe("Annotation");
        copy.TimestampUtc.ShouldBe(marker.TimestampUtc);
        json.ShouldContain("2026-10-08T10:00:00.0000000Z");
        json.ShouldNotContain(marker.Id.ToString());
    }

    [Fact]
    public void NodeMarker_ExplicitScopeAndKind_RetainsOwner()
    {
        // Arrange
        var nodeId = Guid.NewGuid();

        // Act
        var marker = new ProfilingMarker(Guid.NewGuid(), Guid.NewGuid(), nodeId, "session1", "node0001", "Manual GC", DateTimeOffset.UtcNow, "GarbageCollection");

        // Assert
        marker.Scope.ShouldBe(ProfilingMarkerScope.Node);
        marker.NodeId.ShouldBe(nodeId);
        marker.NodeKey.ShouldBe("node0001");
        marker.Kind.ShouldBe("GarbageCollection");
    }

    [Fact]
    public void RuntimeSegment_OpenInterval_HasNoTerminalOutcome()
    {
        // Arrange
        var segment = new ProfilingSegment { StartedUtc = DateTimeOffset.UtcNow };

        // Act
        var closed = segment with { EndedUtc = segment.StartedUtc.AddSeconds(1), Outcome = ProfilingSegmentOutcome.Completed };

        // Assert
        segment.State.ShouldBe(RuntimeProfilingSegmentState.Open);
        segment.Outcome.ShouldBeNull();
        closed.State.ShouldBe(RuntimeProfilingSegmentState.Closed);
        closed.Outcome.ShouldBe(ProfilingSegmentOutcome.Completed);
    }
}
