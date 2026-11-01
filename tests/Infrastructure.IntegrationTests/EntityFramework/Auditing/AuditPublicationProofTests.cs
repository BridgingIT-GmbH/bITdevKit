// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Auditing;

using BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Auditing.Fixtures;

[Collection(nameof(AuditRelationalCollection))]
public class AuditPublicationProofTests(AuditRelationalFixture fixture)
{
    [Theory]
    [InlineData("SqlServer")]
    [InlineData("Postgres")]
    [InlineData("Sqlite")]
    public async Task AppendGraphAsync_UncommittedEarlierWriter_CannotAppearBelowPublishedBoundary(string backend)
    {
        var createConnection = await fixture.CreateDatabaseAsync(backend);
        var sut = new AuditPublicationProofStore(backend, createConnection);
        await sut.InitializeAsync();
        var inserted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var competing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = sut.AppendGraphAsync(Guid.NewGuid().ToString("N"), 100, async () =>
        {
            inserted.SetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10));
        });
        await inserted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        // A separate instance represents a second process, not a shared lock.
        var secondStore = new AuditPublicationProofStore(backend, createConnection);
        var window = Task.Run(() => secondStore.OpenWindowAsync(beforeGate: () => competing.SetResult()));
        try
        {
            await competing.Task.WaitAsync(TimeSpan.FromSeconds(10));
            window.IsCompleted.ShouldBeFalse();
        }
        finally
        {
            release.TrySetResult();
        }

        (await first).ShouldBe(1);
        var committedWindow = await window.WaitAsync(TimeSpan.FromSeconds(15));
        committedWindow.State.Position.ShouldBe(1);
        (await sut.ReadWindowAsync(committedWindow)).Count.ShouldBe(1);
        (await secondStore.AppendGraphAsync(Guid.NewGuid().ToString("N"), 50)).ShouldBe(2);
        (await sut.ReadWindowAsync(committedWindow)).Count.ShouldBe(1);
        var nextWindow = (await sut.OpenWindowAsync()) with { Lower = committedWindow.State.Position };
        (await sut.ReadWindowAsync(nextWindow)).Single().Position.ShouldBe(2);
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("Postgres")]
    [InlineData("Sqlite")]
    public async Task AppendGraphAsync_Rollback_NeverPublishesPartialGraph(string backend)
    {
        var sut = new AuditPublicationProofStore(backend, await fixture.CreateDatabaseAsync(backend));
        await sut.InitializeAsync();

        await Should.ThrowAsync<InvalidOperationException>(() => sut.AppendGraphAsync(
            Guid.NewGuid().ToString("N"), 100, () => throw new InvalidOperationException("InjectedBeforeCommit")));

        (await sut.CountGraphRowsAsync()).ShouldBe(0);
        (await sut.OpenWindowAsync()).State.Position.ShouldBe(0);
        (await sut.AppendGraphAsync(Guid.NewGuid().ToString("N"), 100)).ShouldBe(1);
        (await sut.CountGraphRowsAsync()).ShouldBe(3);
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("Postgres")]
    [InlineData("Sqlite")]
    public async Task ArchiveAsync_SelectedLifecycleChanges_InvalidatesActiveWindowButNotAll(string backend)
    {
        var sut = new AuditPublicationProofStore(backend, await fixture.CreateDatabaseAsync(backend));
        await sut.InitializeAsync();
        var id = Guid.NewGuid().ToString("N");
        await sut.AppendGraphAsync(id, 100);
        var active = await sut.OpenWindowAsync();
        var all = await sut.OpenWindowAsync(all: true);

        await sut.ArchiveAsync(101);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => sut.ReadWindowAsync(active));
        exception.Message.ShouldBe("LifecycleChanged");
        var retained = (await sut.ReadWindowAsync(all)).Single();
        retained.Id.ShouldBe(id);
        retained.Position.ShouldBe(1);
        retained.Archived.ShouldBeTrue();
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("Postgres")]
    [InlineData("Sqlite")]
    public async Task RemoveAsync_EmptyStoreAndNewInstance_RetainsControlStateAndRejectsReplay(string backend)
    {
        var createConnection = await fixture.CreateDatabaseAsync(backend);
        var sut = new AuditPublicationProofStore(backend, createConnection);
        await sut.InitializeAsync();
        var id = Guid.NewGuid().ToString("N");
        await sut.AppendGraphAsync(id, 100);
        var oldWindow = await sut.OpenWindowAsync(all: true);
        await sut.ArchiveAsync(101);
        await sut.RemoveAsync(101);
        (await sut.CountGraphRowsAsync()).ShouldBe(0);

        var restarted = new AuditPublicationProofStore(backend, createConnection);
        var state = await restarted.VerifyDurableCoordinationAsync();
        state.StoreId.ShouldBe(oldWindow.State.StoreId);
        state.Epoch.ShouldBe(oldWindow.State.Epoch);
        state.Position.ShouldBe(1);
        state.RemovedThrough.ShouldBe(1);
        state.Generation.ShouldBe(2);
        var gap = await Should.ThrowAsync<InvalidOperationException>(() => restarted.ReadWindowAsync(oldWindow));
        gap.Message.ShouldBe("HistoryGap");
        // A fresh retained-only selection can establish a new availability baseline.
        // The earlier window still cannot silently acknowledge missing evidence.
        (await restarted.ReadWindowAsync(await restarted.OpenWindowAsync(all: true))).ShouldBeEmpty();
        var replay = await Should.ThrowAsync<InvalidOperationException>(() => restarted.AppendGraphAsync(id, 100));
        replay.Message.ShouldBe("EvidenceExpired");
        (await restarted.AppendGraphAsync(Guid.NewGuid().ToString("N"), 200)).ShouldBe(2);
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("Postgres")]
    [InlineData("Sqlite")]
    public async Task RotateEpochAfterRestoreAsync_PositionMeaningChanges_RejectsEarlierWindow(string backend)
    {
        var sut = new AuditPublicationProofStore(backend, await fixture.CreateDatabaseAsync(backend));
        await sut.InitializeAsync();
        var window = await sut.OpenWindowAsync(all: true);

        await sut.RotateEpochAfterRestoreAsync();

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => sut.ReadWindowAsync(window));
        exception.Message.ShouldBe("EpochChanged");
    }
}