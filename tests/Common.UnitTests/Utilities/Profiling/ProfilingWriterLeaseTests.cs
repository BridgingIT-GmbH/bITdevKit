// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

using Microsoft.Extensions.Time.Testing;

public sealed class ProfilingWriterLeaseTests
{
    [Fact]
    public async Task Open_RetriesStableAttempt_WithoutConsumingAnotherLease()
    {
        // Arrange
        var (sut, clock, node) = Create(maximum: 1);
        var request = new ProfilingOpenWriterRequest { AttemptId = Guid.NewGuid(), Node = node };

        // Act
        var first = await sut.OpenWriterAsync(request);
        clock.Advance(TimeSpan.FromSeconds(5));
        var retry = await sut.OpenWriterAsync(request);
        var other = await sut.OpenWriterAsync(request with { AttemptId = Guid.NewGuid() });

        // Assert
        first.Value.ShouldBe(retry.Value);
        other.IsFailure.ShouldBeTrue();
        other.Errors.ShouldContain(e => e is ProfilingBusyError);
        first.Value.ExpiresUtc.ShouldBe(clock.GetUtcNow().AddSeconds(25));
    }

    [Fact]
    public async Task Renewal_AcceptsOriginalEnvelope_AndSettlementNeverMovesBackward()
    {
        // Arrange
        var (sut, clock, node) = Create();
        var lease = (await sut.OpenWriterAsync(new() { AttemptId = Guid.NewGuid(), Node = node })).Value;
        clock.Advance(TimeSpan.FromSeconds(25));

        // Act
        var first = await sut.SynchronizeWriterAsync(new() { Lease = lease, SettledThrough = 10 });
        clock.Advance(TimeSpan.FromSeconds(10));
        var second = await sut.SynchronizeWriterAsync(new() { Lease = lease, SettledThrough = 4 });

        // Assert
        first.Value.Active.ShouldBeTrue();
        second.Value.Active.ShouldBeTrue();
        second.Value.Lease.SettledThrough.ShouldBe(10);
        second.Value.Lease.WriterId.ShouldBe(lease.WriterId);
        second.Value.Lease.Token.ShouldBe(lease.Token);
        second.Value.Lease.ExpiresUtc.ShouldBe(clock.GetUtcNow().AddSeconds(30));
    }

    [Theory]
    [InlineData("token")]
    [InlineData("writer")]
    [InlineData("node")]
    [InlineData("epoch")]
    [InlineData("generation")]
    public async Task Synchronization_AlteredIdentity_IsNeverActive(string altered)
    {
        // Arrange
        var (sut, _, node) = Create();
        var lease = (await sut.OpenWriterAsync(new() { AttemptId = Guid.NewGuid(), Node = node })).Value;
        var changed = altered switch
        {
            "token" => lease with { Token = Guid.NewGuid() }, "writer" => lease with { WriterId = Guid.NewGuid() },
            "node" => lease with { NodeId = Guid.NewGuid() }, "epoch" => lease with { StoreEpoch = Guid.NewGuid() },
            _ => lease with { Generation = lease.Generation + 1 },
        };

        // Act
        var result = await sut.SynchronizeWriterAsync(new() { Lease = changed });

        // Assert
        result.Value.Active.ShouldBeFalse();
    }

    [Fact]
    public async Task ExpiredToken_NeverRevives_AfterRegistryPruningOrReplacement()
    {
        // Arrange
        var (sut, clock, node) = Create(maximum: 1);
        var request = new ProfilingOpenWriterRequest { AttemptId = Guid.NewGuid(), Node = node };
        var old = (await sut.OpenWriterAsync(request)).Value;
        clock.Advance(TimeSpan.FromSeconds(30));

        // Act
        var oldAttempt = await sut.OpenWriterAsync(request);
        var replacement = await sut.OpenWriterAsync(request with { AttemptId = Guid.NewGuid() });
        var delayed = await sut.SynchronizeWriterAsync(new() { Lease = old });

        // Assert
        oldAttempt.IsFailure.ShouldBeTrue();
        replacement.Value.WriterId.ShouldNotBe(old.WriterId);
        replacement.Value.Token.ShouldNotBe(old.Token);
        replacement.Value.Generation.ShouldBeGreaterThan(old.Generation);
        delayed.Value.Active.ShouldBeFalse();
    }

    [Fact]
    public async Task Close_RetiresOriginalIdentity_AndFreesBoundedRegistration()
    {
        // Arrange
        var (sut, _, node) = Create(maximum: 1);
        var lease = (await sut.OpenWriterAsync(new() { AttemptId = Guid.NewGuid(), Node = node })).Value;

        // Act
        await sut.CloseWriterAsync(lease);
        var replacement = await sut.OpenWriterAsync(new() { AttemptId = Guid.NewGuid(), Node = node });
        var delayed = await sut.SynchronizeWriterAsync(new() { Lease = lease });
        await sut.CloseWriterAsync(lease);

        // Assert
        replacement.IsSuccess.ShouldBeTrue();
        delayed.Value.Active.ShouldBeFalse();
    }

    [Fact]
    public async Task EpochReset_RejectsLeaseFromDifferentProviderScope()
    {
        // Arrange
        var (first, _, node) = Create();
        var (sut, _, _) = Create();
        var lease = (await first.OpenWriterAsync(new() { AttemptId = Guid.NewGuid(), Node = node })).Value;

        // Act
        var result = await sut.SynchronizeWriterAsync(new() { Lease = lease });

        // Assert
        result.Value.Active.ShouldBeFalse();
        sut.Capabilities.Scope.ShouldNotBe(first.Capabilities.Scope);
    }

    [Fact]
    public async Task OpenAndSynchronize_InvalidBounds_ReturnValidationBeforeMutation()
    {
        // Arrange
        var (sut, _, node) = Create();

        // Act
        var empty = await sut.OpenWriterAsync(new() { AttemptId = Guid.Empty, Node = node });
        var excessive = await sut.OpenWriterAsync(new() { AttemptId = Guid.NewGuid(), Node = node, LeaseDuration = TimeSpan.FromDays(1) });
        var nullSync = await sut.SynchronizeWriterAsync(null);

        // Assert
        empty.IsFailure.ShouldBeTrue();
        excessive.IsFailure.ShouldBeTrue();
        nullSync.IsFailure.ShouldBeTrue();
    }

    internal static (InMemoryProfilingStorageProvider Provider, FakeTimeProvider Clock, ProfilingNode Node) Create(int maximum = 128)
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 8, 10, 0, 0, TimeSpan.Zero));
        var options = new ProfilingOptions();
        options.Storage.MaximumWriterLeases = maximum;
        return (new(options, clock), clock, new ProfilingNodeIdentityProvider(clock).GetNode());
    }
}
