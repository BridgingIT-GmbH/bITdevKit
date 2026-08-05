// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Broadcasting;

public class InMemoryBroadcastRegistryStoreTests
{
    [Fact]
    public async Task UpsertAsync_RepeatedIdentity_ReplacesScopesAndReactivates()
    {
        // Arrange
        var options = new BroadcastingOptions { UnreachableFailureThreshold = 1 };
        var sut = new InMemoryBroadcastRegistryStore(options, TimeProvider.System);
        var now = DateTimeOffset.UtcNow;
        await sut.UpsertAsync(new("node-a", null, ["Alpha"], now, now, null));
        await sut.RecordDeliveryAsync("node-a", false, "offline");

        // Act
        await sut.UpsertAsync(new("node-a", null, ["Beta", "beta"], now, now.AddSeconds(1), null));
        var registrations = await sut.ListAsync();

        // Assert
        registrations.Count.ShouldBe(1);
        registrations[0].IsActive.ShouldBeTrue();
        registrations[0].Scopes.ShouldBe(["Beta"]);
        registrations[0].ConsecutiveFailureCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetActiveAsync_MultipleMatchingScopes_ReturnsNodeOnce()
    {
        // Arrange
        var sut = new InMemoryBroadcastRegistryStore(
            new BroadcastingOptions(),
            TimeProvider.System
        );
        var now = DateTimeOffset.UtcNow;
        await sut.UpsertAsync(new("node-a", null, ["Alpha", "Beta"], now, now, null));

        // Act
        var result = await sut.GetActiveAsync(["alpha", "BETA"]);

        // Assert
        result.Count.ShouldBe(1);
        result[0].NodeIdentity.ShouldBe("node-a");
    }

    [Fact]
    public async Task RecordDeliveryAsync_ThresholdReached_DeactivatesUntilSuccess()
    {
        // Arrange
        var options = new BroadcastingOptions { UnreachableFailureThreshold = 2 };
        var sut = new InMemoryBroadcastRegistryStore(options, TimeProvider.System);
        var now = DateTimeOffset.UtcNow;
        await sut.UpsertAsync(new("node-a", null, ["Alpha"], now, now, null));

        // Act
        await sut.RecordDeliveryAsync("node-a", false, "offline");
        var afterFirstFailure = await sut.FindAsync("node-a");
        await sut.RecordDeliveryAsync("node-a", false, "offline");
        var afterSecondFailure = await sut.FindAsync("node-a");
        await sut.RecordDeliveryAsync("node-a", true, null);
        var afterSuccess = await sut.FindAsync("node-a");

        // Assert
        afterFirstFailure.IsActive.ShouldBeTrue();
        afterFirstFailure.ConsecutiveFailureCount.ShouldBe(1);
        afterSecondFailure.IsActive.ShouldBeFalse();
        afterSecondFailure.ConsecutiveFailureCount.ShouldBe(2);
        afterSuccess.IsActive.ShouldBeTrue();
        afterSuccess.ConsecutiveFailureCount.ShouldBe(0);
        afterSuccess.LastFailure.ShouldBeNull();
    }

    [Fact]
    public async Task ExpireLeasesAsync_ExpiredRegistration_MarksInactive()
    {
        // Arrange
        var sut = new InMemoryBroadcastRegistryStore(
            new BroadcastingOptions(),
            TimeProvider.System
        );
        var now = DateTimeOffset.UtcNow;
        await sut.UpsertAsync(
            new("node-a", null, ["Alpha"], now, now, now.AddMinutes(1))
        );

        // Act
        await sut.ExpireLeasesAsync(now.AddMinutes(1));

        // Assert
        (await sut.FindAsync("node-a")).IsActive.ShouldBeFalse();
        (await sut.GetActiveAsync(["Alpha"])).ShouldBeEmpty();
    }
}
