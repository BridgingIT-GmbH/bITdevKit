// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.Durable;

using BridgingIT.DevKit.Infrastructure.LiteDb;
using Microsoft.Extensions.DependencyInjection;

[IntegrationTest("Infrastructure")]
public class LiteDbFeatureDatabaseTests
{
    [Fact]
    public async Task Command_FailedAfterMultipleWrites_RollsBackEveryRecord()
    {
        await using var host = new LiteDbDurableTestHost();
        var sut = CreateDatabase(host);
        var records = sut.Collection<string, DurableProbe>("rows");
        await Should.ThrowAsync<InvalidOperationException>(() => sut.ExecuteAsync<int>(() =>
        {
            records["first"] = new() { Value = "one" };
            records["second"] = new() { Value = "two" };
            return Task.FromException<int>(new InvalidOperationException("rollback"));
        }));
        (await sut.ExecuteAsync(() => Task.FromResult(records.Count))).ShouldBe(0);
    }

    [Fact]
    public async Task Command_CancellationBeforeCommit_RollsBackAndAllowsNextCommand()
    {
        await using var host = new LiteDbDurableTestHost();
        var sut = CreateDatabase(host);
        var records = sut.Collection<string, DurableProbe>("rows");
        using var canceled = new CancellationTokenSource();
        await Should.ThrowAsync<OperationCanceledException>(() => sut.ExecuteAsync(() =>
        {
            records["first"] = new() { Value = "uncommitted" };
            canceled.Cancel();
            return Task.FromResult(1);
        }, canceled.Token));
        (await sut.ExecuteAsync(() => Task.FromResult(records.Count))).ShouldBe(0);
    }

    [Fact]
    public async Task Values_AreDetachedBetweenCommandsAndTrackMutationsOnlyWithinCommand()
    {
        await using var host = new LiteDbDurableTestHost();
        var sut = CreateDatabase(host);
        var records = sut.Collection<string, DurableProbe>("rows");
        var caller = new DurableProbe { Value = "stored" };
        await sut.ExecuteAsync(() => { records["first"] = caller; return Task.CompletedTask; });
        caller.Value = "caller mutation";
        var loaded = await sut.ExecuteAsync(() => Task.FromResult(records["first"]));
        loaded.Value.ShouldBe("stored");
        loaded.Value = "detached mutation";
        await sut.ExecuteAsync(() => { records["first"].Value = "native update"; return Task.CompletedTask; });
        (await sut.ExecuteAsync(() => Task.FromResult(records["first"].Value))).ShouldBe("native update");
    }

    [Fact]
    public async Task Command_LocalBounds_FailWithoutCommittingPartialWrites()
    {
        await using var host = new LiteDbDurableTestHost();
        var sut = CreateDatabase(host, 1);
        var records = sut.Collection<string, DurableProbe>("rows");
        await Should.ThrowAsync<LiteDbProviderException>(() => sut.ExecuteAsync(() =>
        {
            records["first"] = new() { Value = "one" };
            records["second"] = new() { Value = "two" };
            return Task.CompletedTask;
        }));
        (await sut.ExecuteAsync(() => Task.FromResult(records.Count))).ShouldBe(0);
    }

    [Fact]
    public async Task Command_AsynchronousCallback_CannotContinueUsingNativeRecords()
    {
        await using var host = new LiteDbDurableTestHost();
        var sut = CreateDatabase(host);
        var records = sut.Collection<string, DurableProbe>("rows");
        var continueCallback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<int> pending = null;
        await Should.ThrowAsync<InvalidOperationException>(() => sut.ExecuteAsync(() =>
        {
            pending = ContinueAsync();
            return pending;
        }));
        continueCallback.SetResult();
        await Should.ThrowAsync<InvalidOperationException>(async () => await pending);
        (await sut.ExecuteAsync(() => Task.FromResult(records.Count))).ShouldBe(0);

        async Task<int> ContinueAsync()
        {
            await continueCallback.Task;
            records["invalid"] = new();
            return 1;
        }
    }

    [Fact]
    public async Task PropertyBag_RepeatedCommands_PreserveTypedNestedValues()
    {
        await using var host = new LiteDbDurableTestHost();
        var sut = CreateDatabase(host);
        var records = sut.Collection<string, PropertyBag>("bags");
        var original = new PropertyBag();
        original.Set("count", 5);
        original.Set("model", new DurableProbe { Value = "typed" });
        await sut.ExecuteAsync(() => { records["first"] = original; return Task.CompletedTask; });
        for (var i = 0; i < 40; i++)
        {
            var restored = await sut.ExecuteAsync(() => Task.FromResult(records["first"]));
            restored.Get("count").ShouldBeOfType<int>().ShouldBe(5);
            restored.Get("model").ShouldBeOfType<DurableProbe>().Value.ShouldBe("typed");
        }
    }

    private static LiteDbFeatureDatabase CreateDatabase(LiteDbDurableTestHost host, int max = 10000) => new(host.Services.GetRequiredService<IServiceScopeFactory>(),
        new LiteDbFeatureOptions { CollectionPrefix = "probe", MaxMaterializedRows = max });
}

public class DurableProbe
{
    public string Value { get; set; }
}
