// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Tests.Profiling;

using BridgingIT.DevKit.Common;
using Shouldly;
using Xunit;

/// <summary>Verifies bounded shared maintenance against memory and actual database providers.</summary>
/// <example>Run the Profiling suites to exercise each backend with identical retained graphs.</example>
public abstract partial class ProfilingStorageContractTestsBase
{
    /// <summary>The actual background worker maintains retained Runtime history with both capture capabilities disabled.</summary>
    /// <example>Run with the HostedMaintenance_DisabledCapture filter.</example>
    [Fact]
    public async Task HostedMaintenance_DisabledCapture_EnforcesRuntimeAgeAndCountPreservingPinnedAndActive()
    {
        var options = new ProfilingOptions { Enabled = true };
        options.Runtime.MaximumRetainedSessions = 1;
        options.Runtime.MaximumSessionAge = TimeSpan.FromDays(2);
        options.Storage.MaximumMaintenanceRoots = 1;
        options.Storage.MaintenanceTimeBudget = TimeSpan.FromSeconds(2);
        var h = await this.CreateAsync(options);
        options.Runtime.Enabled.ShouldBeFalse();
        options.Operations.Enabled.ShouldBeFalse();
        var expired = await AddTerminalRuntimeAsync(h, h.Clock.GetUtcNow().AddDays(-30));
        var pinned = await AddTerminalRuntimeAsync(h, h.Clock.GetUtcNow().AddDays(-30), pinned: true);
        var older = await AddTerminalRuntimeAsync(h, h.Clock.GetUtcNow().AddMinutes(-20));
        var newest = await AddTerminalRuntimeAsync(h, h.Clock.GetUtcNow().AddMinutes(-10));
        var active = (await h.Provider.Runtime.GetOrCreateActiveSessionAsync(RuntimeRequest(h))).Value.Session;
        var health = new OperationProfilingHealthState(new OperationProfilingCompletionQueue(options, h.Clock), h.Provider,
            new ProfilingNodeIdentityProvider(h.Clock), options);
        using var sut = new ProfilingMaintenanceService(h.Provider, options, health, clock: h.Clock);

        await sut.StartAsync(CancellationToken.None);
        try
        {
            await WaitForRuntimeCountAsync(h.Provider.Runtime, 4);
            h.Clock.Advance(options.Storage.MaintenanceInterval);
            await WaitForRuntimeCountAsync(h.Provider.Runtime, 3);
            var retained = (await h.Provider.Runtime.ListSessionsAsync()).Value;
            retained.Select(session => session.Identity.Id).Order().ShouldBe(new[] { pinned.Identity.Id, newest.Identity.Id, active.Identity.Id }.Order());
            retained.ShouldNotContain(session => session.Identity.Id == expired.Identity.Id || session.Identity.Id == older.Identity.Id);
        }
        finally
        {
            await sut.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>Both datasets consume one root budget and Runtime removal invalidates an operation publication boundary.</summary>
    /// <example>Run with the Maintenance_RuntimeAndOperations filter.</example>
    [Fact]
    public async Task Maintenance_RuntimeAndOperations_SharesRootBudgetAndInvalidatesCursors()
    {
        var options = new ProfilingOptions();
        options.Storage.MaximumMaintenanceRoots = 1;
        options.Storage.MaintenanceTimeBudget = TimeSpan.FromSeconds(2);
        var h = await this.CreateAsync(options);
        var expired = await AddTerminalRuntimeAsync(h, h.Clock.GetUtcNow().AddDays(-30));
        await h.Store.AppendAsync([h.Envelope(h.Record(), 1), h.Envelope(h.Record(), 2)]);
        await h.SettleAsync(2);
        var query = h.Query() with { PageSize = 1 };
        var page = (await h.Store.QueryAsync(query)).Value;
        var request = new ProfilingMaintenanceRequest
        {
            MaximumRoots = 1, Budget = options.Storage.MaintenanceTimeBudget, MaximumOperationCount = 1,
            MaximumRuntimeSessions = 1, MaximumRuntimeSessionAge = TimeSpan.FromDays(7),
        };

        var first = (await h.Provider.ResumeMaintenanceAsync(request)).Value;
        first.RemovedOperations.ShouldBe(0);
        first.RemovedRuntimeSessions.ShouldBe(1);
        first.DeletionRevision.ShouldBeGreaterThan(page.Boundary.DeletionRevision);
        (await h.Store.QueryAsync(query with { Cursor = page.NextCursor })).Errors.ShouldContain(error => error is ProfilingQueryBoundaryError);
        (await h.Provider.Runtime.ListSessionsAsync()).Value.ShouldBeEmpty();
        // Deleted session identities cannot be used to resurrect an expired graph.
        (await h.Provider.Runtime.GetOrCreateActiveSessionAsync(RuntimeRequest(h) with { Identity = expired.Identity })).IsFailure.ShouldBeTrue();

        var second = (await h.Provider.ResumeMaintenanceAsync(request)).Value;
        second.RemovedRuntimeSessions.ShouldBe(0);
        second.RetentionRemovedOperations.ShouldBe(1);
        (await h.Store.QueryAsync(query)).Value.TotalCount.ShouldBe(1);
    }

    /// <summary>Pin changes are reflected immediately in oldest-first retention selection.</summary>
    /// <example>Run with the Maintenance_PinChanges filter.</example>
    [Fact]
    public async Task Maintenance_PinChanges_RemovesUnpinnedHistoryAndKeepsPinnedNewest()
    {
        var h = await this.CreateAsync();
        var older = await AddTerminalRuntimeAsync(h, h.Clock.GetUtcNow().AddMinutes(-2), pinned: true);
        var newer = await AddTerminalRuntimeAsync(h, h.Clock.GetUtcNow().AddMinutes(-1));
        await h.Provider.Runtime.UpdateSessionMetadataAsync(older.Identity.Key, new(null, [], null, false));
        await h.Provider.Runtime.UpdateSessionMetadataAsync(newer.Identity.Key, new(null, [], null, true));

        var result = await h.Provider.ResumeMaintenanceAsync(new() { MaximumRuntimeSessionAge = TimeSpan.FromSeconds(10) });

        result.IsSuccess.ShouldBeTrue();
        result.Value.RemovedRuntimeSessions.ShouldBe(1);
        (await h.Provider.Runtime.ListSessionsAsync()).Value.Single().Identity.ShouldBe(newer.Identity);
    }

    /// <summary>Recovering an applying clear consumes the same deletion budget as both kinds of retention.</summary>
    /// <example>Run with the Maintenance_ApplyingClear filter.</example>
    [Fact]
    public async Task Maintenance_ApplyingClear_DefersRuntimeRetentionUntilRootBudgetAvailable()
    {
        var options = new ProfilingOptions();
        options.Storage.MaximumMaintenanceRoots = 1;
        options.Storage.MaintenanceTimeBudget = TimeSpan.FromSeconds(2);
        var h = await this.CreateAsync(options);
        await AddTerminalRuntimeAsync(h, h.Clock.GetUtcNow().AddDays(-30));
        await h.Store.AppendAsync([h.Envelope(h.Record(), 1), h.Envelope(h.Record(), 2), h.Envelope(h.Record(), 3)]);
        var clearing = h.Provider.ClearAsync(new() { DataSet = ProfilingDataSet.Operations });
        var pending = await PendingAsync(h, h.Lease);
        (await AckAsync(h, pending.Id, h.Lease, 3)).IsSuccess.ShouldBeTrue();
        (await clearing).Value.State.ShouldBe(ProfilingClearState.Applying);
        var request = new ProfilingMaintenanceRequest { MaximumRoots = 1, Budget = options.Storage.MaintenanceTimeBudget };

        for (var index = 0; index < 2; index++)
        {
            var result = (await h.Provider.ResumeMaintenanceAsync(request)).Value;
            result.RemovedOperations.ShouldBe(1);
            result.RemovedRuntimeSessions.ShouldBe(0);
            (await h.Provider.Runtime.ListSessionsAsync()).Value.Count.ShouldBe(1);
        }

        var retained = (await h.Provider.ResumeMaintenanceAsync(request)).Value;
        retained.RemovedOperations.ShouldBe(0);
        retained.RemovedRuntimeSessions.ShouldBe(1);
    }

    /// <summary>Runtime root deletion removes its graph without deleting a node still owned by an operation or writer.</summary>
    /// <example>Run with the Maintenance_RuntimeGraph filter.</example>
    [Fact]
    public async Task Maintenance_RuntimeGraph_KeepsOperationAndSharedNode()
    {
        var h = await this.CreateAsync();
        var operation = h.Record();
        (await h.Store.AppendAsync([h.Envelope(operation, 1)])).IsSuccess.ShouldBeTrue();
        var runtime = h.Provider.Runtime;
        var completed = h.Clock.GetUtcNow().AddDays(-30);
        var request = RuntimeRequest(h) with { StartedUtc = completed.AddSeconds(-30) };
        var session = (await runtime.GetOrCreateActiveSessionAsync(request)).Value.Session;
        var correlation = new RuntimeProfilingNodeCorrelation("shared-node", h.Node.ProcessStartedUtc);
        (await runtime.GetOrCreateNodeAsync(correlation, h.Node)).IsSuccess.ShouldBeTrue();
        (await runtime.UpsertParticipationAsync(new()
        {
            SessionId = session.Identity.Id, SessionKey = session.Identity.Key, NodeId = h.Node.Identity.Id, NodeKey = h.Node.Identity.Key,
            JoinedUtc = request.StartedUtc, State = RuntimeProfilingParticipationState.Collecting, Role = RuntimeProfilingNodeRole.AdHocContributor,
        })).IsSuccess.ShouldBeTrue();
        (await runtime.AddSnapshotAsync(new()
        {
            Identity = ProfilingIdentityFactory.CreateRuntimeSnapshot(), SessionId = session.Identity.Id, SessionKey = session.Identity.Key,
            NodeId = h.Node.Identity.Id, NodeKey = h.Node.Identity.Key, HostName = h.Node.HostName, ProcessId = h.Node.ProcessId,
            TimestampUtc = request.StartedUtc.AddSeconds(1), Sequence = 1,
        })).IsSuccess.ShouldBeTrue();
        (await runtime.TryTransitionSessionAsync(session.Identity.Id, [RuntimeProfilingSessionState.Running],
            RuntimeProfilingSessionState.Stopped, completed)).IsSuccess.ShouldBeTrue();
        (await runtime.GetSessionDataAsync(session.Identity.Key)).Value.Snapshots.Count.ShouldBe(1);

        var result = await h.Provider.ResumeMaintenanceAsync(new());

        result.IsSuccess.ShouldBeTrue();
        result.Value.RemovedRuntimeSessions.ShouldBe(1);
        (await runtime.GetSessionDataAsync(session.Identity.Key)).IsFailure.ShouldBeTrue();
        (await runtime.FindNodeAsync(correlation)).Value.Identity.ShouldBe(h.Node.Identity);
        (await h.Store.FindAsync(operation.Id)).Value.Node.Identity.ShouldBe(h.Node.Identity);
    }

    /// <summary>Creates a closed Runtime session for backend conformance checks.</summary>
    /// <example><code>var session = await AddTerminalRuntimeAsync(h, h.Clock.GetUtcNow().AddDays(-30));</code></example>
    protected static async Task<RuntimeProfilingSession> AddTerminalRuntimeAsync(Harness h, DateTimeOffset completed, bool pinned = false)
    {
        var request = RuntimeRequest(h) with { StartedUtc = completed.AddSeconds(-30) };
        var created = await h.Provider.Runtime.GetOrCreateActiveSessionAsync(request);
        created.IsSuccess.ShouldBeTrue();
        var session = created.Value.Session;
        (await h.Provider.Runtime.TryTransitionSessionAsync(session.Identity.Id, [RuntimeProfilingSessionState.Running],
            RuntimeProfilingSessionState.Stopped, completed)).IsSuccess.ShouldBeTrue();
        if (pinned)
        {
            (await h.Provider.Runtime.UpdateSessionMetadataAsync(session.Identity.Key, new(null, [], null, true))).IsSuccess.ShouldBeTrue();
        }

        return session;
    }

    private static async Task WaitForRuntimeCountAsync(IRuntimeProfilingStore store, int count)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while ((await store.ListSessionsAsync(deadline.Token)).Value.Count != count)
        {
            await Task.Delay(10, deadline.Token);
        }
    }
}
