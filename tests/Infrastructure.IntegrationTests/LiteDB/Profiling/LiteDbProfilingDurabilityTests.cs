// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.Profiling;

public sealed partial class LiteDbProfilingContractTests
{
    [Fact]
    public async Task Reopen_PreservesWholeOperationsWriterOwnershipAndSignedQueryBoundaries()
    {
        var h = await this.CreateAsync();
        var first = h.Record();
        var second = h.Record(durationMs: 20);
        (await h.Store.AppendAsync([h.Envelope(first, 1), h.Envelope(second, 2)])).Value.Records.ShouldAllBe(row => row.Outcome == ProfilingWriteOutcome.Accepted);
        var query = h.Query() with { PageSize = 1 };
        var page = (await h.Store.QueryAsync(query)).Value;
        page.NextCursor.ShouldNotBeNull();
        var request = new ProfilingOpenWriterRequest { AttemptId = Guid.NewGuid(), Node = h.Node };
        var lease = (await h.Store.OpenWriterAsync(request)).Value;
        var reopened = await this.ReopenAsync(h.Provider);
        reopened.Capabilities.Name.ShouldBe("LiteDB");
        reopened.Capabilities.Shared.ShouldBeFalse();
        reopened.Capabilities.Scope.ShouldBe(h.Provider.Capabilities.Scope);
        var retained = (await reopened.Operations.FindAsync(first.Id)).Value;
        retained.Node.Identity.ShouldBe(h.Node.Identity);
        retained.Dimensions.Count.ShouldBe(first.Dimensions.Count);
        var continuation = await reopened.Operations.QueryAsync(query with { Cursor = page.NextCursor });
        continuation.IsSuccess.ShouldBeTrue();
        continuation.Value.Records.Single().Id.ShouldNotBe(page.Records.Single().Id);
        (await reopened.Operations.OpenWriterAsync(request)).Value.Token.ShouldBe(lease.Token);
        (await reopened.Operations.AppendAsync([h.Envelope(first, 1)])).Value.Records.Single().Outcome.ShouldBe(ProfilingWriteOutcome.AlreadyStored);
    }

    [Fact]
    public async Task Reopen_ResumesSealedBatchedClear_AndRejectsQueuedOldEvidence()
    {
        var options = new ProfilingOptions();
        options.Storage.MaximumMaintenanceRoots = 1;
        var h = await this.CreateAsync(options);
        var records = new[] { h.Envelope(h.Record(), 1), h.Envelope(h.Record(), 2) };
        await h.Store.AppendAsync(records);
        var clearing = h.Provider.ClearAsync(new() { DataSet = ProfilingDataSet.Operations });
        var synchronization = await h.Store.SynchronizeWriterAsync(new() { Lease = h.Lease });
        var pending = synchronization.Value.PendingClears.Single();
        (await h.Store.AcknowledgeClearAsync(new() { ClearId = pending.Id, Lease = h.Lease, CompletionCutoff = 2 })).IsSuccess.ShouldBeTrue();
        var progress = await clearing.WaitAsync(TimeSpan.FromSeconds(10));
        progress.IsSuccess.ShouldBeTrue();
        progress.Value.State.ShouldBe(ProfilingClearState.Applying);
        var reopened = await this.ReopenAsync(h.Provider);
        var resumed = await reopened.ResumeMaintenanceAsync(new() { MaximumRoots = 1 });
        resumed.IsSuccess.ShouldBeTrue();
        resumed.Value.RemainingClears.ShouldBe(0);
        (await reopened.Operations.FindAsync(records[0].Record.Id)).Value.ShouldBeNull();
        (await reopened.Operations.FindAsync(records[1].Record.Id)).Value.ShouldBeNull();
        (await reopened.Operations.AppendAsync([records[0]])).Value.Records.Single().Outcome.ShouldBe(ProfilingWriteOutcome.Cleared);
    }

    [Fact]
    public async Task Reopen_PreservesRuntimeSessionIdentityPinAndDeletionFence()
    {
        var h = await this.CreateAsync();
        var session = await AddTerminalRuntimeAsync(h, h.Clock.GetUtcNow().AddMinutes(-1), pinned: true);
        var reopened = await this.ReopenAsync(h.Provider);
        var found = await reopened.Runtime.FindSessionAsync(session.Identity.Key);
        found.IsSuccess.ShouldBeTrue();
        found.Value.Identity.ShouldBe(session.Identity);
        found.Value.IsPinned.ShouldBeTrue();
        (await reopened.Runtime.DeleteSessionAsync(session.Identity.Key)).Value.ShouldBeTrue();
        var again = await this.ReopenAsync(reopened);
        (await again.Runtime.FindSessionAsync(session.Identity.Key)).Value.ShouldBeNull();
        var recreate = await again.Runtime.GetOrCreateActiveSessionAsync(new(session.Identity, "replayed", h.Clock.GetUtcNow(), TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1), []));
        recreate.IsFailure.ShouldBeTrue();
    }
}
