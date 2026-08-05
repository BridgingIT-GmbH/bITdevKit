// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Broadcasting;

public class RecentBroadcastTrackerTests
{
    [Fact]
    public void TryReserve_ConcurrentCalls_AllowsOneReservation()
    {
        // Arrange
        var sut = new RecentBroadcastTracker(new BroadcastingOptions());
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        // Act
        var accepted = Enumerable
            .Range(0, 100)
            .AsParallel()
            .Count(_ => sut.TryReserve(id, now));

        // Assert
        accepted.ShouldBe(1);
    }

    [Fact]
    public void Release_UncommittedReservation_AllowsRetry()
    {
        // Arrange
        var sut = new RecentBroadcastTracker(new BroadcastingOptions());
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        sut.TryReserve(id, now).ShouldBeTrue();

        // Act
        sut.Release(id);

        // Assert
        sut.TryReserve(id, now).ShouldBeTrue();
    }

    [Fact]
    public void Commit_BeforeRetentionExpires_RejectsDuplicate()
    {
        // Arrange
        var options = new BroadcastingOptions
        {
            DuplicateRetention = TimeSpan.FromMinutes(1),
        };
        var sut = new RecentBroadcastTracker(options);
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        sut.TryReserve(id, now).ShouldBeTrue();
        sut.Commit(id, now);

        // Act
        var result = sut.TryReserve(id, now.AddSeconds(59));

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Commit_AfterRetentionExpires_AllowsRetry()
    {
        // Arrange
        var options = new BroadcastingOptions
        {
            DuplicateRetention = TimeSpan.FromMinutes(1),
        };
        var sut = new RecentBroadcastTracker(options);
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        sut.TryReserve(id, now).ShouldBeTrue();
        sut.Commit(id, now);

        // Act
        var result = sut.TryReserve(id, now.AddMinutes(1));

        // Assert
        result.ShouldBeTrue();
    }
}
