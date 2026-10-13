// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

public sealed class ProfilingCoordinationSnapshotTests
{
    [Fact]
    public void WriterRegistry_RestoredState_PreservesOriginalLeaseSettlementAndGeneration()
    {
        // Arrange
        var epoch = Guid.NewGuid();
        var utc = DateTimeOffset.UtcNow;
        var original = new ProfilingWriterRegistry(epoch, 2);
        var request = new ProfilingOpenWriterRequest { AttemptId = Guid.NewGuid(), Node = new ProfilingNodeIdentityProvider().GetNode() };
        var lease = original.Open(request, utc, TimeSpan.FromSeconds(30)).Value;
        original.Synchronize(new() { Lease = lease, SettledThrough = 10 }, utc.AddSeconds(1), []);
        var sut = new ProfilingWriterRegistry(epoch, 2);

        // Act
        sut.Restore(original.Generation, original.Snapshot());
        var retry = sut.Open(request, utc.AddSeconds(2), TimeSpan.FromSeconds(30));
        var boundary = sut.Prepare(utc.AddSeconds(2));
        var replacement = sut.Open(request with { AttemptId = Guid.NewGuid() }, utc.AddSeconds(2), TimeSpan.FromSeconds(30));

        // Assert
        retry.Value.WriterId.ShouldBe(lease.WriterId);
        retry.Value.Token.ShouldBe(lease.Token);
        retry.Value.SettledThrough.ShouldBe(10);
        boundary.Generation.ShouldBe(lease.Generation);
        replacement.Value.Generation.ShouldBeGreaterThan(boundary.Generation);
    }

    [Fact]
    public void WriterRegistry_InvalidOrOversizedSnapshot_IsRejectedBeforeUse()
    {
        // Arrange
        var sut = new ProfilingWriterRegistry(Guid.NewGuid(), 1);
        var values = new ProfilingWriterRegistrationState[] { new(Guid.NewGuid(), new(), false), new(Guid.NewGuid(), new(), false) };

        // Act/Assert
        Should.Throw<ArgumentException>(() => sut.Restore(1, values));
        Should.Throw<ArgumentException>(() => sut.Restore(1, [values[0]]));
        sut.Count.ShouldBe(0);
    }
}
