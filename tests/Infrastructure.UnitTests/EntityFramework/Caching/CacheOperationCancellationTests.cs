// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Caching;

using System.Data.Common;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

public class CacheOperationCancellationTests
{
    [Fact]
    public async Task RemoteCancellation_BeforeAdmissionPreservesEntriesAndCollectionEpoch()
    {
        await using var fixture = await CacheRelationalTestStore.CreateAsync();
        (await fixture.WriteAsync("key", [1])).IsSuccess.ShouldBeTrue();
        var operation = await CreateRunningAsync(fixture);
        await using var remote = new EntityFrameworkCacheStoreProvider("remote", new(), fixture.Context);
        (await remote.InitializeAsync([fixture.Definition])).IsSuccess.ShouldBeTrue();
        var requested = await remote.SaveOperationAsync(operation with { CancellationRequested = true }, operation.Revision);
        requested.IsSuccess.ShouldBeTrue();
        long epoch;
        await using (var before = fixture.Context()) { epoch = (await before.CacheCollections.SingleAsync()).Epoch; }

        var result = await fixture.Provider.InvalidateAsync(operation.Request);

        result.IsSuccess.ShouldBeTrue();
        result.Value.AffectedCount.ShouldBe(0);
        result.Value.Completed.ShouldBeFalse();
        var stopped = (await fixture.Provider.GetOperationAsync(operation.Id)).Value;
        stopped.State.ShouldBe(CacheOperationState.Cancelled);
        stopped.CancellationRequested.ShouldBeTrue();
        (await fixture.Provider.GetQuotaAsync()).Value.CurrentEntries.ShouldBe(1);
        await using var after = fixture.Context();
        (await after.CacheCollections.SingleAsync()).Epoch.ShouldBe(epoch);
    }

    [Fact]
    public async Task CancellationDuringAdmittedUnit_PreservesClaimUntilCheckpointThenResumesExactProgress()
    {
        var pause = new MetadataPause();
        await using var fixture = await CacheRelationalTestStore.CreateAsync(interceptor: pause);
        for (var index = 0; index < 130; index++) { (await fixture.WriteAsync("key-" + index, [1])).IsSuccess.ShouldBeTrue(); }

        var operation = await CreateRunningAsync(fixture);
        await using var remote = new EntityFrameworkCacheStoreProvider("remote", new(), fixture.Context);
        (await remote.InitializeAsync([fixture.Definition])).IsSuccess.ShouldBeTrue();
        pause.Arm();
        var admitted = fixture.Provider.InvalidateAsync(operation.Request).AsTask();
        await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            string claimNode;
            long? claimUntil;
            await using (var before = fixture.Context())
            {
                var claim = await before.CacheOperations.SingleAsync();
                claimNode = claim.ClaimNode;
                claimUntil = claim.ClaimUntilTicks;
            }

            var current = (await remote.GetOperationAsync(operation.Id)).Value;
            var requested = await remote.SaveOperationAsync(current with { CancellationRequested = true }, current.Revision);
            requested.IsSuccess.ShouldBeTrue();
            (await remote.SaveOperationAsync(requested.Value with { State = CacheOperationState.Cancelled }, requested.Value.Revision)).IsFailure.ShouldBeTrue();
            (await fixture.Provider.SaveOperationAsync(requested.Value with { State = CacheOperationState.Cancelled }, requested.Value.Revision)).IsFailure.ShouldBeTrue();
            (await remote.SaveOperationAsync(requested.Value with { CancellationRequested = false }, requested.Value.Revision)).IsFailure.ShouldBeTrue();
            await using var verify = fixture.Context();
            var row = await verify.CacheOperations.SingleAsync();
            row.ClaimNode.ShouldBe(claimNode);
            row.ClaimUntilTicks.ShouldBe(claimUntil);
            (await fixture.Provider.GetQuotaAsync()).Value.CurrentEntries.ShouldBe(130);
        }
        finally
        {
            pause.Release.TrySetResult();
            _ = await admitted.WaitAsync(TimeSpan.FromSeconds(10));
        }

        var completedUnit = await admitted.WaitAsync(TimeSpan.FromSeconds(10));
        completedUnit.IsSuccess.ShouldBeTrue(string.Join(", ", completedUnit.Errors.Select(e => e.Message)));
        completedUnit.Value.Completed.ShouldBeFalse();
        completedUnit.Value.AffectedCount.ShouldBe(128);
        var stopped = (await fixture.Provider.GetOperationAsync(operation.Id)).Value;
        stopped.State.ShouldBe(CacheOperationState.Cancelled);
        stopped.Outcome.Continuation.ShouldBe(completedUnit.Value.Continuation);
        var quota = (await fixture.Provider.GetQuotaAsync()).Value;
        quota.CurrentEntries.ShouldBe(2);
        quota.RetiredBytes.ShouldBe(128);
        (await fixture.Provider.InvalidateAsync(operation.Request with { Continuation = stopped.Outcome.Continuation })).IsFailure.ShouldBeTrue();
        var resumed = await remote.SaveOperationAsync(stopped with { State = CacheOperationState.Pending, CancellationRequested = false }, stopped.Revision);
        resumed.IsSuccess.ShouldBeTrue();
        var claimed = await remote.SaveOperationAsync(resumed.Value with { State = CacheOperationState.Running }, resumed.Value.Revision);
        claimed.IsSuccess.ShouldBeTrue();
        var last = await remote.InvalidateAsync(operation.Request with { Continuation = stopped.Outcome.Continuation });
        last.IsSuccess.ShouldBeTrue();
        last.Value.Completed.ShouldBeTrue();
        last.Value.AffectedCount.ShouldBe(130);
    }

    private static async Task<CacheProviderOperation> CreateRunningAsync(CacheRelationalTestStore fixture)
    {
        var request = new CacheProviderInvalidation("test", "items", CacheSelection.Pattern("*"), DateTimeOffset.UtcNow);
        var created = await fixture.Provider.SaveOperationAsync(new() { Id = Guid.NewGuid().ToString("N"), Collection = "items", Cutoff = request.Cutoff,
            Request = request, State = CacheOperationState.Pending });
        created.IsSuccess.ShouldBeTrue();
        var claimed = await fixture.Provider.SaveOperationAsync(created.Value with { State = CacheOperationState.Running }, created.Value.Revision);
        claimed.IsSuccess.ShouldBeTrue();

        return claimed.Value;
    }

    private sealed class MetadataPause : DbCommandInterceptor
    {
        private int armed;

        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Arm() => Interlocked.Exchange(ref this.armed, 1);

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("__Caching_Entries", StringComparison.Ordinal) && command.CommandText.Contains("MetadataJson", StringComparison.Ordinal) &&
                Interlocked.Exchange(ref this.armed, 0) == 1)
            {
                this.Entered.TrySetResult();
                await this.Release.Task.WaitAsync(cancellationToken);
            }

            return result;
        }
    }
}