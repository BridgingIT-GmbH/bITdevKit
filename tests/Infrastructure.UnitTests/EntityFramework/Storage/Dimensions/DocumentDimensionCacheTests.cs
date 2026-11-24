// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Storage;

using Infrastructure.EntityFramework;

public sealed class DocumentDimensionCacheTests
{
    [Fact]
    public async Task Publish_PositiveEntriesStayBoundedAndClearRejectsOlderSessions()
    {
        var sut = new DocumentDimensionNameCache();
        await using var session = new DocumentDimensionMutationSession(sut);
        for (var index = 0; index < 8192; index++) session.Pending[new("database", "scope", $"NAME{index}")] = index + 1;
        sut.Count.ShouldBe(0);
        sut.Publish(session);
        sut.Count.ShouldBe(4096);
        sut.TryGet(new("database", "scope", "NAME0"), out _).ShouldBeFalse();
        sut.TryGet(new("database", "scope", "NAME8191"), out var latest).ShouldBeTrue();
        latest.ShouldBe(8192);
        sut.TryGet(new("other-database", "scope", "NAME8191"), out _).ShouldBeFalse();
        sut.TryGet(new("database", "other-scope", "NAME8191"), out _).ShouldBeFalse();
        sut.Clear();
        sut.Publish(session);
        sut.Count.ShouldBe(0);
        await using var next = new DocumentDimensionMutationSession(sut);
        next.Pending[new("database", "scope", "NEW")] = 42;
        sut.Publish(next);
        sut.TryGet(new("database", "scope", "NEW"), out var id).ShouldBeTrue();
        id.ShouldBe(42);
    }

    [Fact]
    public async Task Admission_FourMutationsHoldPermitsAndFifthCanCancel()
    {
        var sut = new DocumentDimensionNameCache();
        var sessions = Enumerable.Range(0, 4).Select(_ => new DocumentDimensionMutationSession(sut)).ToArray();
        await using var waiting = new DocumentDimensionMutationSession(sut);
        try
        {
            foreach (var session in sessions) await session.AdmitAsync(CancellationToken.None);
            using var cancellation = new CancellationTokenSource();
            var pending = waiting.AdmitAsync(cancellation.Token);
            pending.IsCompleted.ShouldBeFalse();
            cancellation.Cancel();
            await Should.ThrowAsync<OperationCanceledException>(() => pending);
        }
        finally
        {
            foreach (var session in sessions) await session.DisposeAsync();
        }

        await waiting.AdmitAsync(CancellationToken.None);
        sut.RegistrationAdmission.CurrentCount.ShouldBe(3);
    }
}
