// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Auditing;

using System.Collections.Immutable;
using Application.Auditing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

public class AuditMemoryTests
{
    private static readonly DateTimeOffset now = DateTimeOffset.Parse("2026-10-30T12:00:00Z");

    [Fact]
    public async Task Append_IdempotencyAndContentConflict_DoNotRepublishOrOverwrite()
    {
        var provider = Provider();
        var record = Record("first");
        var first = (await provider.AppendAsync(record)).Value;
        var duplicate = (await provider.AppendAsync(record)).Value;
        duplicate.State.ShouldBe("AlreadyPresent");
        duplicate.AcceptanceSequence.ShouldBe(first.AcceptanceSequence);
        (await provider.AppendAsync(record with { Key = "different" })).IsFailure.ShouldBeTrue();
        (await provider.FindAsync(record.AuditId)).Value.Key.ShouldBe("first");
        (await provider.OpenExtractionAsync(new() { Selection = Plan() })).Value.UpperPosition.ShouldBe(1);
    }

    [Fact]
    public async Task Extraction_FixedUpperIncludesLateEarlierEventOnlyInNextWindow()
    {
        var provider = Provider();
        var first = Record("first");
        await Add(provider, first);
        var cursor = (await provider.OpenExtractionAsync(new() { Selection = Plan(), BatchSize = 1 })).Value;
        var late = Record("late") with { StartedUtc = now.AddHours(-2) };
        await Add(provider, late);
        var batch = (await provider.ReadExtractionAsync(cursor)).Value;
        batch.Items.Select(i => i.Record.AuditId).ShouldBe([first.AuditId]);
        batch.Exhausted.ShouldBeTrue();
        batch.Cursor.AfterPosition.ShouldBe(cursor.UpperPosition);
        var next = (await provider.OpenExtractionAsync(new()
        {
            Selection = cursor.Selection, AfterPosition = batch.Cursor.AfterPosition,
            StoreId = cursor.StoreId, StoreEpoch = cursor.StoreEpoch, AvailabilityBaseline = cursor.AvailabilityBaseline
        })).Value;
        (await provider.ReadExtractionAsync(next)).Value.Items.Single().Record.AuditId.ShouldBe(late.AuditId);
    }

    [Fact]
    public async Task Extraction_BatchesNeverAdvancePastUnemittedMatchesAndEmptySelectionAdvances()
    {
        var provider = Provider();
        await Add(provider, Record("one"), Record("two"), Record("three"));
        var cursor = (await provider.OpenExtractionAsync(new() { Selection = Plan(), BatchSize = 1 })).Value;
        var seen = new List<Guid>();
        while (true)
        {
            var batch = (await provider.ReadExtractionAsync(cursor)).Value;
            seen.AddRange(batch.Items.Select(item => item.Record.AuditId));
            cursor = batch.Cursor;
            if (batch.Exhausted) break;
        }

        seen.Distinct().Count().ShouldBe(3);
        var empty = Plan(new() { Filters = [new("Key", FilterOperator.Equal, "absent")] });
        var emptyCursor = (await provider.OpenExtractionAsync(new() { Selection = empty })).Value;
        var noMatches = (await provider.ReadExtractionAsync(emptyCursor)).Value;
        noMatches.Items.ShouldBeEmpty();
        noMatches.Exhausted.ShouldBeTrue();
        noMatches.Cursor.AfterPosition.ShouldBe(emptyCursor.UpperPosition);
    }

    [Fact]
    public async Task Extraction_EncodedByteBoundsNeverSkipTheNextRecord()
    {
        var provider = Provider();
        var first = Record("one");
        await Add(provider, first, Record("two"));
        var encoded = AuditRecordCodec.Encode((await provider.FindAsync(first.AuditId)).Value).Length;
        var cursor = (await provider.OpenExtractionAsync(new() { Selection = Plan(), MaxBatchBytes = encoded + 32 })).Value;
        var batch = (await provider.ReadExtractionAsync(cursor)).Value;
        batch.Items.Single().Record.AuditId.ShouldBe(first.AuditId);
        batch.Exhausted.ShouldBeFalse();
        batch.Cursor.AfterPosition.ShouldBe(1);
        (await provider.ReadExtractionAsync(batch.Cursor)).Value.Items.Single().Position.ShouldBe(2);
        (await provider.ReadExtractionAsync(cursor with { MaxRecordBytes = 128 })).Errors.OfType<AuditError>().Single().Code.ShouldBe("AuditRecordTooLarge");
        cursor.AfterPosition.ShouldBe(0);
    }

    [Fact]
    public async Task Archive_ChangesDatasetMembershipAndInvalidatesActiveWindowButKeepsAllWindow()
    {
        var provider = Provider();
        var record = Record("retained");
        await Add(provider, record);
        var original = (await provider.FindAsync(record.AuditId)).Value;
        var active = (await provider.OpenExtractionAsync(new() { Selection = Plan() })).Value;
        var allPlan = Plan(new() { Filters = [new(FilterCustomType.EnumValues, new() { ["field"] = "StorageState", ["values"] = new[] { "Active", "Archived" } })] });
        allPlan.RequiresStableLifecycle.ShouldBeFalse();
        var all = (await provider.OpenExtractionAsync(new() { Selection = allPlan })).Value;
        (await provider.ArchiveBeforeAsync(now)).Value.ShouldBe(1);
        (await provider.ReadExtractionAsync(active)).Errors.OfType<AuditError>().Single().Code.ShouldBe("AuditLifecycleChanged");
        var archived = (await provider.ReadExtractionAsync(all)).Value.Items.Single();
        archived.StorageState.ShouldBe("Archived");
        archived.Position.ShouldBe(1);
        AuditRecordCodec.Encode(archived.Record).ShouldBe(AuditRecordCodec.Encode(original));
        (await provider.SearchAsync(Plan())).TotalCount.ShouldBe(0);
        (await provider.SearchAsync(allPlan)).TotalCount.ShouldBe(1);
        var stored = (await provider.FindStoredAsync(record.AuditId, new())).Value;
        stored.StorageState.ShouldBe("Archived");
    }

    [Fact]
    public async Task RemovalAndEviction_RejectOldWindowWithGapAndNewStoreWithEpochError()
    {
        var provider = Provider();
        await Add(provider, Record("one"), Record("two"));
        var cursor = (await provider.OpenExtractionAsync(new() { Selection = Plan() })).Value;
        await provider.ArchiveBeforeAsync(now);
        await provider.RemoveArchivedBeforeAsync(now);
        (await provider.ReadExtractionAsync(cursor)).Errors.OfType<AuditError>().Single().Code.ShouldBe("AuditHistoryGap");
        (await Provider().ReadExtractionAsync(cursor)).Errors.OfType<AuditError>().Single().Code.ShouldBe("AuditEpochChanged");
        var fresh = (await provider.OpenExtractionAsync(new() { Selection = Plan() })).Value;
        fresh.AvailabilityBaseline.ShouldBe(2);
        (await provider.ReadExtractionAsync(fresh)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Retention_UsesCompletedTotalAgeRoutesLateWritesToArchiveAndRejectsRemovedReplay()
    {
        var provider = Provider();
        var old = Record("old");
        await Add(provider, old);
        await provider.ArchiveBeforeAsync(now.AddMinutes(-20));
        var late = Record("late");
        await Add(provider, late);
        (await provider.FindStoredAsync(late.AuditId, new())).Value.StorageState.ShouldBe("Archived");
        await provider.RemoveArchivedBeforeAsync(now.AddMinutes(-20));
        (await provider.AppendAsync(old)).Errors.OfType<AuditError>().Single().Code.ShouldBe("AuditEvidenceExpired");
        var longRunning = Record("long-running") with { StartedUtc = now.AddDays(-2), CompletedUtc = now };
        await Add(provider, longRunning);
        (await provider.FindStoredAsync(longRunning.AuditId, new())).Value.StorageState.ShouldBe("Active");
        await provider.RemoveArchivedBeforeAsync(now.AddMinutes(-1));
        (await provider.AppendAsync(Record("old-replay"))).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task Capacity_CombinesArchiveAndActiveAndEvictsArchivedFirst()
    {
        var provider = Provider(2);
        var active = Record("active") with { CompletedUtc = now };
        var archive = Record("archive");
        await Add(provider, active, archive);
        await provider.ArchiveBeforeAsync(now.AddMinutes(-1));
        var cursor = (await provider.OpenExtractionAsync(new() { Selection = Plan() })).Value;
        await Add(provider, Record("new"));
        (await provider.FindAsync(active.AuditId)).IsSuccess.ShouldBeTrue();
        (await provider.FindAsync(archive.AuditId)).IsFailure.ShouldBeTrue();
        provider.EvictedRecords.ShouldBe(1);
        (await provider.ReadExtractionAsync(cursor)).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task Coordinator_BoundsSweepsAndSuppressesOnlyItsOwnOperations()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(now));
        services.AddAuditing().WithRetention(new() { ArchiveAfter = TimeSpan.FromMinutes(1), RemoveAfter = TimeSpan.FromMinutes(20), BatchSize = 1, MaxBatches = 1 });
        using var host = services.BuildServiceProvider();
        var provider = host.GetRequiredService<IAuditingStorageProvider>();
        // Appends predating the configured removal age are rejected immediately.
        (await provider.AppendAsync(Record("expired"))).IsFailure.ShouldBeTrue();
        var live = Record("live") with { CompletedUtc = now };
        await Add(provider, live);
        using var scope = host.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IAuditContext>();
        var root = context.BeginScope(o => o.Key("business"));
        (await scope.ServiceProvider.GetRequiredService<AuditRetentionCoordinator>().SweepAsync()).IsSuccess.ShouldBeTrue();
        context.Current.ShouldBeSameAs(root);
        root.Complete();
        await root.DisposeAsync();
        (await provider.FindAsync(root.AuditId)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Observation_DoesNotRepairMissingInstrumentationAndPreservesBusinessResultAndExceptions()
    {
        var provider = Provider();
        var harness = new AuditTestHarness(provider);
        var value = new object();
        var calls = 0;
        var observed = await harness.ObserveAsync(ct => { calls++; return Task.FromResult(value); }, () => []);
        calls.ShouldBe(1);
        observed.Value.ShouldBeSameAs(value);
        observed.Observation.AssertPresent(r => true).IsFailure.ShouldBeTrue();
        observed.Observation.AssertAbsent(r => true).IsSuccess.ShouldBeTrue();
        (await provider.OpenExtractionAsync(new() { Selection = Plan() })).Value.UpperPosition.ShouldBe(0);
        var error = new InvalidOperationException("business");
        var thrown = await Should.ThrowAsync<InvalidOperationException>(() => harness.ObserveAsync<object>(ct => throw error, () => []));
        thrown.ShouldBeSameAs(error);
    }

    [Fact]
    public async Task Observation_ReadsOriginalCaptureAndSecretFailuresContainNoValues()
    {
        var provider = Provider();
        var record = Record("root") with { Details = ImmutableDictionary<string, AuditValue>.Empty.Add("approved", AuditValue.From("test-secret-42")) };
        await Add(provider, record);
        var harness = new AuditTestHarness(provider);
        var observed = await harness.ObserveAsync(ct => Task.FromResult(42), () => [record.AuditId]);
        observed.Observation.AssertPresent(r => r.AuditId == record.AuditId).IsSuccess.ShouldBeTrue();
        var result = observed.Observation.AssertNoValue("test-secret-42");
        result.IsFailure.ShouldBeTrue();
        string.Join(";", result.Errors).ShouldNotContain("test-secret-42");
        (await harness.ReadEvidenceAsync(Enumerable.Range(0, 101).Select(i => Guid.NewGuid()).ToArray())).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Observation_PendingOrFailedFlushCannotProvePresenceOrAbsence()
    {
        var observation = new AuditObservation
        {
            Flush = Result<AuditFlushResult>.Success(new() { Pending = 1 }),
            Evidence = Result<ImmutableArray<AuditRecord>>.Success([Record("root")])
        };
        observation.AssertPresent(r => true).IsFailure.ShouldBeTrue();
        observation.AssertAbsent(r => false).IsFailure.ShouldBeTrue();
        (observation with { Flush = Result<AuditFlushResult>.Success(new() { Failed = 1 }) }).AssertAbsent(r => false).IsFailure.ShouldBeTrue();
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(10, 9)]
    [InlineData(10, 10)]
    public void Retention_InvalidTotalAgePolicyIsRejected(int archive, int remove)
    {
        Should.Throw<ArgumentException>(() => new AuditRetentionOptions { ArchiveAfter = TimeSpan.FromMinutes(archive), RemoveAfter = TimeSpan.FromMinutes(remove) }.Validate());
    }

    [Fact]
    public async Task SearchPage_FreezesLifecycleMetadataBeforeReturningLazyResultValues()
    {
        var provider = Provider();
        var resource = new AuditResourceReference("Customer", "42");
        await Add(provider, Record("root") with { Resources = [resource] });
        var page = await provider.SearchAsync(Plan());
        var history = await provider.GetResourceHistoryAsync(Plan(), resource);
        await provider.ArchiveBeforeAsync(now);
        page.Value.Single().StorageState.ShouldBe("Active");
        history.Value.Single().Record.StorageState.ShouldBe("Active");
    }

    [Fact]
    public async Task Observation_SelectorFailureCannotReplaceSuccessfulBusinessResult()
    {
        var value = new object();
        var observed = await new AuditTestHarness(Provider()).ObserveAsync(ct => Task.FromResult(value), () => throw new InvalidOperationException("test selector"));
        observed.Value.ShouldBeSameAs(value);
        observed.Observation.Evidence.IsFailure.ShouldBeTrue();
        observed.Observation.Flush.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Observation_UsesRealRecorderAndKeepsParallelTestOwnedIdentitiesSeparate()
    {
        var services = new ServiceCollection();
        services.AddAuditing();
        using var host = services.BuildServiceProvider();
        var provider = host.GetRequiredService<IAuditingStorageProvider>();
        var harness = new AuditTestHarness(provider);
        await Task.WhenAll(Enumerable.Range(0, 5).Select(async index =>
        {
            using var scope = host.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IAuditContext>();
            Guid id = default;
            var observed = await harness.ObserveAsync(ct => context.RunAsync(o => o.Key($"business.{index}"), (audit, token) =>
            {
                id = audit.AuditId;
                audit.SetDetail("password", AuditValue.From("test-only-secret"));
                return Task.FromResult(index);
            }, ct), () => [id]);
            observed.Value.ShouldBe(index);
            observed.Observation.AssertPresent(record => record.Key == $"business.{index}").IsSuccess.ShouldBeTrue();
            observed.Observation.Evidence.Value.Length.ShouldBe(1);
            observed.Observation.AssertNoValue("test-only-secret").IsSuccess.ShouldBeTrue();
        }));
    }

    private static InMemoryAuditStorageProvider Provider(int count = 1000) => new(new AuditingOptions { MemoryRecordLimit = count }, time: new FakeTimeProvider(now));
    private static AuditRecord Record(string key)
    {
        var id = Guid.NewGuid();
        return new() { AuditId = id, OriginAuditId = id, Key = key, StartedUtc = now.AddHours(-1), CompletedUtc = now.AddMinutes(-30) };
    }
    private static AuditSearchPlan Plan(FilterModel filter = null) => new AuditSearchPlanBuilder(new(), new FakeTimeProvider(now)).Build(filter).Value;
    private static async Task Add(IAuditingStorageProvider provider, params AuditRecord[] records)
    {
        foreach (var record in records) (await provider.AppendAsync(record)).IsSuccess.ShouldBeTrue();
    }
}
