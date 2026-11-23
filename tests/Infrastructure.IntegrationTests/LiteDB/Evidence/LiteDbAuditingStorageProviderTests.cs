// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.Evidence;

using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

[IntegrationTest("Infrastructure")]
public class LiteDbAuditingStorageProviderTests
{
    [Fact]
    public async Task Append_PreservesWholeCanonicalGraph_WithIdempotenceAndDurableReceipts()
    {
        await using var host = new LiteDbEvidenceTestHost();
        var sut = host.Audits;
        var actionId = Guid.NewGuid();
        var source = Record("ledger.updated") with
        {
            Resources = [new("Ledger", "L-1")],
            Actions = [new() { Id = actionId, Key = "ledger.validate", Outcome = AuditOutcome.Succeeded, Resources = [new("Entry", "E-1")] }],
            EntityChanges = [new() { ActionId = actionId, EntityType = "Ledger", EntityId = "L-1", Operation = "Update" }]
        };
        var frozen = AuditRecordCodec.Freeze(source).Value;
        var first = await sut.AppendAsync(frozen);
        first.IsSuccess.ShouldBeTrue(Errors(first));
        first.Value.State.ShouldBe("Persisted");
        var duplicate = await sut.AppendAsync(source);
        duplicate.Value.State.ShouldBe("AlreadyPresent");
        var conflict = await sut.AppendAsync(source with { Key = "different" });
        Error(conflict, "AuditIdentityConflict");
        var stored = (await sut.FindAsync(source.AuditId)).Value;
        AuditRecordCodec.Encode(stored).ShouldBe(AuditRecordCodec.Encode(frozen));
        stored.Actions.Single().Resources.Single().Id.ShouldBe("E-1");
        stored.EntityChanges.Single().ActionId.ShouldBe(actionId);
        var cursor = (await sut.OpenExtractionAsync(new())).Value;
        cursor.UpperPosition.ShouldBe(1);
        var flush = (await sut.FlushAsync()).Value;
        flush.ReceiptEpoch.ShouldBe(first.Value.ReceiptEpoch);
        flush.Persisted.ShouldBe(2);
        flush.ThroughSequence.ShouldBe(duplicate.Value.AcceptanceSequence);
        sut.Capabilities.IsDurable.ShouldBeTrue();
        sut.Capabilities.IsShared.ShouldBeFalse();
        sut.Capabilities.Buffered.ShouldBeFalse();
    }

    [Fact]
    public async Task Readiness_IsReadOnlyUntilExplicitInitialization_AndHostedStartupInitializes()
    {
        await using var host = new LiteDbEvidenceTestHost();
        var sut = host.Audits;
        File.Exists(host.AuditFile).ShouldBeFalse();
        Error(await sut.CheckReadinessAsync(), "AuditStoreNotReady");
        File.Exists(host.AuditFile).ShouldBeFalse();
        foreach (var initializer in host.Services.GetServices<IHostedService>().OfType<LiteDbAuditingInitializationService>())
            await initializer.StartAsync(default);
        (await sut.CheckReadinessAsync()).IsSuccess.ShouldBeTrue();
        var before = (await sut.OpenExtractionAsync(new())).Value;
        (await sut.CheckReadinessAsync()).IsSuccess.ShouldBeTrue();
        var after = (await sut.OpenExtractionAsync(new())).Value;
        after.StoreId.ShouldBe(before.StoreId);
        after.UpperPosition.ShouldBe(0);
        after.LifecycleGeneration.ShouldBe(before.LifecycleGeneration);
    }

    [Fact]
    public async Task AuditCommit_CompletesDuringBusinessTransaction_AndSurvivesRollback()
    {
        await using var host = new LiteDbEvidenceTestHost(services => services.AddLiteDbRepository<HistoryAccount>());
        await using var scope = host.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGenericRepository<HistoryAccount>>();
        var transaction = scope.ServiceProvider.GetRequiredService<IRepositoryTransaction<HistoryAccount>>();
        var source = Record("ledger.attempted");
        var rollback = transaction.ExecuteScopedAsync(async () =>
        {
            await repository.InsertAsync(new HistoryAccount { Name = "rolled-back" });
            var append = await host.Audits.AppendAsync(source);
            append.IsSuccess.ShouldBeTrue(Errors(append));
            throw new InvalidOperationException("rollback business change");
        });
        await Should.ThrowAsync<InvalidOperationException>(() => rollback.WaitAsync(TimeSpan.FromSeconds(10)));
        (await repository.CountAsync()).ShouldBe(0);
        (await host.Audits.FindAsync(source.AuditId)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task InvalidHashOversizeAndCancellation_DoNotPublishPartialRecords()
    {
        await using var host = new LiteDbEvidenceTestHost();
        var sut = host.Audits;
        var frozen = AuditRecordCodec.Freeze(Record("ledger.updated")).Value;
        (await sut.AppendAsync(frozen with { Key = "tampered" })).IsFailure.ShouldBeTrue();
        var maxBytes = host.Services.GetRequiredService<AuditingOptions>().Limits.MaxRecordBytes;
        Error(await sut.AppendAsync(Record(new string('x', maxBytes + 1))), "AuditRecordTooLarge");
        await Should.ThrowAsync<OperationCanceledException>(() => sut.AppendAsync(Record("cancel"), new CancellationToken(true)));
        (await sut.OpenExtractionAsync(new())).Value.UpperPosition.ShouldBe(0);
    }

    [Fact]
    public async Task ScopedQueries_CountBeforePaging_AndApplyApprovedGroupsAndFacets()
    {
        await using var host = new LiteDbEvidenceTestHost();
        var sut = host.Audits;
        var first = Record("ledger.alpha") with { ModuleName = "A" };
        await sut.AppendAsync(first);
        await sut.AppendAsync(Record("ledger.beta") with { ModuleName = "A" });
        await sut.AppendAsync(Record("ledger.hidden") with { ModuleName = "B" });
        var plan = new AuditSearchPlan { Predicate = Module("A"), PageSize = 1, AsOfUtc = host.Clock.GetUtcNow() };
        var page = await sut.SearchAsync(plan);
        page.IsSuccess.ShouldBeTrue(Errors(page));
        page.TotalCount.ShouldBe(2);
        page.Value.Count().ShouldBe(1);
        (await sut.SearchAsync(plan with { Page = 2 })).Value.Single().AuditId.ShouldNotBe(page.Value.Single().AuditId);
        Error(await sut.FindAsync(first.AuditId, Module("B")), "AuditNotFound");
        (await sut.AggregateAsync(new() { Selection = plan, Field = "ModuleName" })).Value.Single().Count.ShouldBe(2);
        (await sut.FindFacetsAsync(new() { Selection = plan, Field = "Key", Prefix = "ledger." })).Value.ShouldBe(["ledger.alpha", "ledger.beta"]);
        Error(await sut.FindFacetsAsync(new() { Field = "Actor.Secret" }), "AuditQueryInvalid");
        Error(await sut.SearchAsync(plan with { Lifecycle = "unsupported" }), "AuditQueryInvalid");
    }

    [Fact]
    public async Task ResourceHistory_UsesExactResourceId_AndRelatedUsesExplicitLinks()
    {
        await using var host = new LiteDbEvidenceTestHost();
        var sut = host.Audits;
        var parent = Record("ledger.root");
        var child = Record("ledger.child") with
        {
            OriginAuditId = parent.AuditId, CausationAuditId = parent.AuditId,
            Actions = [new() { Id = Guid.NewGuid(), Key = "ledger.validate", Resources = [new("Ledger", "Ab")] }]
        };
        await sut.AppendAsync(parent);
        await sut.AppendAsync(child);
        await sut.AppendAsync(Record("ledger.other") with { Resources = [new("Ledger", "ab")] });
        var history = await sut.GetResourceHistoryAsync(new(), new("ledger", "Ab"));
        history.TotalCount.ShouldBe(1);
        history.Value.Single().MatchingActionCount.ShouldBe(1);
        var related = await sut.FindRelatedAsync(new() { OriginAuditId = parent.AuditId, AnchorAuditId = parent.AuditId });
        related.IsSuccess.ShouldBeTrue(Errors(related));
        related.Value.Single().AuditId.ShouldBe(child.AuditId);
    }

    [Fact]
    public async Task QueryService_DeniesFailedHostScope_AndEnforcesTrustedOuterScope()
    {
        await using (var denied = new LiteDbEvidenceTestHost(services => services.AddAuditing().WithReadScope((_, _) =>
            Task.FromResult(Result<AuditReadScope>.Failure(new AuditError("AuditReadScopeUnavailable"))))))
        {
            await denied.Audits.AppendAsync(Record("hidden"));
            await using var scope = denied.Services.CreateAsyncScope();
            (await scope.ServiceProvider.GetRequiredService<IAuditQueryService>().SearchAsync()).IsFailure.ShouldBeTrue();
        }

        await using var host = new LiteDbEvidenceTestHost(services => services.AddAuditing().WithReadScope((_, _) =>
            Task.FromResult(Result<AuditReadScope>.Success(new(new FilterModel { Filters = [new("ModuleName", FilterOperator.Equal, "A")] }, "module-a-v1")))));
        await host.Audits.AppendAsync(Record("allowed") with { ModuleName = "A" });
        await host.Audits.AppendAsync(Record("hidden") with { ModuleName = "B" });
        await using var read = host.Services.CreateAsyncScope();
        var sut = read.ServiceProvider.GetRequiredService<IAuditQueryService>();
        var result = await sut.SearchAsync();
        result.IsSuccess.ShouldBeTrue(Errors(result));
        result.Value.Single().Key.ShouldBe("allowed");
        (await sut.SearchAsync(new() { Filters = [new("ModuleName", FilterOperator.Equal, "B")] })).TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Extraction_ReopensSameFixedWindow_AndExcludesLaterAppends()
    {
        var directory = Directory.CreateTempSubdirectory("bdk-audit-reopen-");
        try
        {
            var file = Path.Combine(directory.FullName, "audit.db");
            AuditExtractionCursor cursor;
            Guid receiptEpoch;
            var first = Record("first");
            var second = Record("second");
            await using (var host = new LiteDbEvidenceTestHost(file: file))
            {
                receiptEpoch = (await host.Audits.AppendAsync(first)).Value.ReceiptEpoch;
                await host.Audits.AppendAsync(second);
                cursor = (await host.Audits.OpenExtractionAsync(new() { BatchSize = 1 })).Value;
                var batch = (await host.Audits.ReadExtractionAsync(cursor)).Value;
                batch.Items.Single().Record.AuditId.ShouldBe(first.AuditId);
                cursor = batch.Cursor;
                await host.Audits.AppendAsync(Record("later"));
            }

            await using (var host = new LiteDbEvidenceTestHost(file: file))
            {
                var sut = host.Audits;
                (await sut.CheckReadinessAsync()).IsSuccess.ShouldBeTrue();
                var batch = await sut.ReadExtractionAsync(cursor);
                batch.IsSuccess.ShouldBeTrue(Errors(batch));
                batch.Value.Items.Single().Record.AuditId.ShouldBe(second.AuditId);
                batch.Value.Exhausted.ShouldBeTrue();
                var next = (await sut.OpenExtractionAsync(new() { AfterPosition = 2, StoreId = cursor.StoreId, StoreEpoch = cursor.StoreEpoch, AvailabilityBaseline = cursor.AvailabilityBaseline })).Value;
                (await sut.ReadExtractionAsync(next)).Value.Items.Single().Record.Key.ShouldBe("later");
                sut.DeliveryStatus.ReceiptEpoch.ShouldNotBe(receiptEpoch);
            }
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public async Task Extraction_AdvancesUnmatchedBatches_AndWorksBeyondMaterializationBound()
    {
        await using var host = new LiteDbEvidenceTestHost(maxRows: 3);
        var sut = host.Audits;
        for (var i = 0; i < 7; i++) (await sut.AppendAsync(Record("event." + i) with { ModuleName = i == 6 ? "B" : "A" })).IsSuccess.ShouldBeTrue();
        (await sut.SearchAsync(new())).IsFailure.ShouldBeTrue();
        var cursor = (await sut.OpenExtractionAsync(new() { Selection = new() { Predicate = Module("B") }, BatchSize = 2 })).Value;
        var positions = new List<long>();
        var keys = new List<string>();
        for (var i = 0; i < 10; i++)
        {
            var batch = await sut.ReadExtractionAsync(cursor);
            batch.IsSuccess.ShouldBeTrue(Errors(batch));
            batch.Value.Cursor.AfterPosition.ShouldBeGreaterThan(cursor.AfterPosition);
            positions.Add(batch.Value.Cursor.AfterPosition);
            keys.AddRange(batch.Value.Items.Select(item => item.Record.Key));
            cursor = batch.Value.Cursor;
            if (batch.Value.Exhausted) break;
        }

        positions.ShouldBe([2L, 4L, 6L, 7L]);
        keys.ShouldBe(["event.6"]);
    }

    [Fact]
    public async Task Extraction_RespectsByteBudgetWithoutSkippingWholeGraphs()
    {
        await using var host = new LiteDbEvidenceTestHost();
        var sut = host.Audits;
        var first = AuditRecordCodec.Freeze(Record("first")).Value;
        await sut.AppendAsync(first);
        await sut.AppendAsync(Record("other"));
        var cursor = (await sut.OpenExtractionAsync(new() { MaxBatchBytes = AuditRecordCodec.Encode(first).Length + 20 })).Value;
        var batch = (await sut.ReadExtractionAsync(cursor)).Value;
        batch.Items.Single().Record.Key.ShouldBe("first");
        batch.Exhausted.ShouldBeFalse();
        batch.Cursor.AfterPosition.ShouldBe(1);
        var final = (await sut.ReadExtractionAsync(batch.Cursor)).Value;
        final.Items.Single().Record.Key.ShouldBe("other");
        final.Exhausted.ShouldBeTrue();
        Error(await sut.ReadExtractionAsync(cursor with { MaxRecordBytes = 128 }), "AuditRecordTooLarge");
    }

    [Fact]
    public async Task Retention_PreservesOriginalBytes_AndInvalidatesLifecycleAndHistoryWindows()
    {
        await using var host = new LiteDbEvidenceTestHost();
        var sut = host.Audits;
        var source = Record("old") with { CompletedUtc = host.Clock.GetUtcNow().AddDays(-30) };
        await sut.AppendAsync(source);
        var original = AuditRecordCodec.Encode((await sut.FindAsync(source.AuditId)).Value);
        var active = (await sut.OpenExtractionAsync(new())).Value;
        var all = (await sut.OpenExtractionAsync(new() { Selection = new() { Lifecycle = "All", RequiresStableLifecycle = false } })).Value;
        (await sut.ArchiveBeforeAsync(host.Clock.GetUtcNow().AddDays(-10))).Value.ShouldBe(1);
        AuditRecordCodec.Encode((await sut.FindAsync(source.AuditId)).Value).ShouldBe(original);
        (await sut.SearchAsync(new())).TotalCount.ShouldBe(0);
        (await sut.SearchAsync(new() { Lifecycle = "Archived" })).TotalCount.ShouldBe(1);
        Error(await sut.ReadExtractionAsync(active), "AuditLifecycleChanged");
        var emitted = (await sut.ReadExtractionAsync(all)).Value;
        (await sut.RemoveArchivedBeforeAsync(host.Clock.GetUtcNow().AddDays(-20))).Value.ShouldBe(1);
        Error(await sut.FindAsync(source.AuditId), "AuditNotFound");
        Error(await sut.ReadExtractionAsync(all), "AuditHistoryGap");
        Error(await sut.ReadExtractionAsync(emitted.Cursor with { RequiredAfterPosition = 0 }), "AuditHistoryGap");
        Error(await sut.AppendAsync(source), "AuditEvidenceExpired");
    }

    [Fact]
    public async Task RetentionCutoffs_SurviveReopen_AndArchiveDelayedEligibleAppends()
    {
        var directory = Directory.CreateTempSubdirectory("bdk-audit-retention-");
        try
        {
            var file = Path.Combine(directory.FullName, "audit.db");
            var now = new EvidenceClock().GetUtcNow();
            await using (var host = new LiteDbEvidenceTestHost(file: file))
            {
                await host.Audits.ArchiveBeforeAsync(now.AddDays(-10));
                await host.Audits.RemoveArchivedBeforeAsync(now.AddDays(-20));
            }

            await using (var host = new LiteDbEvidenceTestHost(file: file))
            {
                Error(await host.Audits.AppendAsync(Record("expired") with { CompletedUtc = now.AddDays(-30) }), "AuditEvidenceExpired");
                (await host.Audits.AppendAsync(Record("archive") with { CompletedUtc = now.AddDays(-15) })).IsSuccess.ShouldBeTrue();
                (await host.Audits.SearchAsync(new() { Lifecycle = "Archived" })).TotalCount.ShouldBe(1);
                (await host.Audits.SearchAsync(new())).TotalCount.ShouldBe(0);
            }
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public async Task EpochRotation_UsesCompareAndSet_AndInvalidatesPriorCursors()
    {
        await using var host = new LiteDbEvidenceTestHost();
        var sut = host.Audits;
        var before = (await sut.OpenExtractionAsync(new())).Value;
        Error(await sut.RotateStoreEpochAsync(Guid.NewGuid()), "AuditEpochChanged");
        var rotated = await sut.RotateStoreEpochAsync(before.StoreEpoch);
        rotated.IsSuccess.ShouldBeTrue();
        Error(await sut.ReadExtractionAsync(before), "AuditEpochChanged");
        var current = (await sut.OpenExtractionAsync(new())).Value;
        current.StoreId.ShouldBe(before.StoreId);
        current.StoreEpoch.ShouldBe(rotated.Value);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(1, 0)]
    public async Task InvalidCursor_IsRejected(long lifecycle, long baseline)
    {
        await using var host = new LiteDbEvidenceTestHost();
        var cursor = (await host.Audits.OpenExtractionAsync(new())).Value;
        Error(await host.Audits.ReadExtractionAsync(cursor with { LifecycleGeneration = lifecycle, AvailabilityBaseline = baseline }), "AuditCursorInvalid");
    }

    [Fact]
    public async Task Integrity_RequiresSignatures_VerifiesDuplicates_AndPreservesTagsThroughArchive()
    {
        var keys = new EvidenceKeyRing();
        await using var host = new LiteDbEvidenceTestHost(services =>
        {
            services.AddSingleton(keys);
            services.AddAuditing().WithHmacSha256<EvidenceKeys>();
        });
        var sut = host.Audits;
        Error(await sut.AppendAsync(Record("unsigned")), "AuditIntegrityMissingProtection");
        var signer = host.Services.GetRequiredService<HmacSha256AuditIntegrityService>();
        var signed = (await signer.SealAsync(Record("signed"))).Value;
        var appended = await sut.AppendAsync(signed);
        appended.IsSuccess.ShouldBeTrue(Errors(appended));
        (await sut.AppendAsync(signed)).Value.State.ShouldBe("AlreadyPresent");
        await sut.ArchiveBeforeAsync(host.Clock.GetUtcNow().AddSeconds(1));
        var stored = (await sut.FindAsync(signed.AuditId)).Value;
        AuditRecordCodec.Encode(stored).ShouldBe(AuditRecordCodec.Encode(signed));
        (await signer.VerifyAsync(stored)).Value.ShouldBe("Verified");
        keys.Available = false;
        (await sut.AppendAsync(signed)).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task NdjsonExport_UsesExistingTrustedScopeAndIntegrityPipeline()
    {
        await using var host = new LiteDbEvidenceTestHost(services =>
            services.AddAuditing().WithReadScope((_, _) => Task.FromResult(Result<AuditReadScope>.Success(AuditReadScope.Unrestricted("operator-v1")))).WithExports(o => o));
        await host.Audits.AppendAsync(Record("ledger.exported"));
        await using var scope = host.Services.CreateAsyncScope();
        var sut = scope.ServiceProvider.GetRequiredService<IAuditExportService>();
        var opened = await sut.OpenAsync(new() { Format = "ndjson" });
        opened.IsSuccess.ShouldBeTrue(Errors(opened));
        using var bytes = new MemoryStream();
        var completion = await opened.Value.WriteAsync(bytes);
        completion.IsSuccess.ShouldBeTrue(Errors(completion));
        Encoding.UTF8.GetString(bytes.ToArray()).ShouldContain("ledger.exported");
    }

    [Fact]
    public async Task OriginalAdditiveJson_SurvivesPublicationArchiveAndReopen()
    {
        var directory = Directory.CreateTempSubdirectory("bdk-audit-original-");
        try
        {
            var file = Path.Combine(directory.FullName, "audit.db");
            var frozen = AuditRecordCodec.Freeze(Record("future")).Value;
            var document = JsonNode.Parse(AuditRecordCodec.CanonicalDocument(frozen)).AsObject();
            document["futureExtension"] = new JsonObject { ["label"] = "retained-original" };
            using var body = JsonDocument.Parse(document.ToJsonString());
            document["contentHash"] = AuditCanonicalizerV1.Hash(AuditCanonicalizerV1.Encode(body.RootElement));
            document["integrityProtection"] = null;
            var decoded = AuditRecordCodec.Decode(Encoding.UTF8.GetBytes(document.ToJsonString()));
            decoded.IsSuccess.ShouldBeTrue(Errors(decoded));
            var expected = AuditRecordCodec.Encode(decoded.Value);
            await using (var host = new LiteDbEvidenceTestHost(file: file))
            {
                var append = await host.Audits.AppendAsync(decoded.Value);
                append.IsSuccess.ShouldBeTrue(Errors(append));
                await host.Audits.ArchiveBeforeAsync(host.Clock.GetUtcNow().AddSeconds(1));
            }

            await using (var host = new LiteDbEvidenceTestHost(file: file))
            {
                var stored = await host.Audits.FindStoredAsync(decoded.Value.AuditId, new());
                stored.IsSuccess.ShouldBeTrue(Errors(stored));
                stored.Value.StorageState.ShouldBe("Archived");
                AuditRecordCodec.Encode(stored.Value.Record).ShouldBe(expected);
            }
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public async Task Retention_UsesBoundedNativeBatches_BeyondDiagnosticQueryLimit()
    {
        await using var host = new LiteDbEvidenceTestHost(maxRows: 3);
        var sut = host.Audits;
        for (var i = 0; i < 7; i++) await sut.AppendAsync(Record("old." + i) with { CompletedUtc = host.Clock.GetUtcNow().AddDays(-30) });
        var cutoff = host.Clock.GetUtcNow().AddDays(-10);
        long archived = 0;
        long removed = 0;
        for (var i = 0; i < 4; i++) archived += (await sut.ArchiveBeforeAsync(cutoff)).Value;
        for (var i = 0; i < 4; i++) removed += (await sut.RemoveArchivedBeforeAsync(cutoff)).Value;
        archived.ShouldBe(7);
        removed.ShouldBe(7);
        (await sut.SearchAsync(new() { Lifecycle = "All" })).TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task AuditRuntime_FinalizesRootWithNestedActionThroughSelectedProvider()
    {
        await using var host = new LiteDbEvidenceTestHost();
        await using var scope = host.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IAuditContext>();
        var root = context.BeginScope(builder => builder.Key("ledger.post"));
        root.IsRecording.ShouldBeTrue();
        root.AddResource(new("Ledger", "L-1"));
        await using (var action = context.BeginScope(builder => builder.Key("ledger.validate")))
        {
            action.SetDetail("count", AuditValue.From(3L));
            action.Complete();
        }

        root.Complete();
        await root.DisposeAsync();
        var stored = await host.Audits.FindAsync(root.AuditId);
        stored.IsSuccess.ShouldBeTrue(Errors(stored));
        stored.Value.Outcome.ShouldBe(AuditOutcome.Succeeded);
        stored.Value.Resources.Single().Id.ShouldBe("L-1");
        stored.Value.Actions.Single().Key.ShouldBe("ledger.validate");
        host.Audits.DeliveryStatus.Persisted.ShouldBe(1);
    }

    [Fact]
    public async Task CorruptOriginalDocument_FailsClosedOnReadAndDuplicateAppend()
    {
        await using var host = new LiteDbEvidenceTestHost();
        var original = Record("ledger.original");
        await host.Audits.AppendAsync(original);
        await using var scope = host.Services.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredKeyedService<LiteDbDatabaseSession>("auditing");
        var mutation = await session.ExecuteAsync(async () =>
        {
            await session.NativeAsync(database =>
            {
                var collection = database.GetCollection<global::LiteDB.BsonDocument>("auditing_records");
                var row = collection.FindAll().Single();
                var payload = JsonNode.Parse(row["Payload"].AsString).AsObject();
                var contentKey = payload.First(pair => pair.Key.Equals("Content", StringComparison.OrdinalIgnoreCase)).Key;
                payload[contentKey] = Convert.ToBase64String(Encoding.UTF8.GetBytes("invalid original document"));
                row["Payload"] = payload.ToJsonString();
                collection.Update(row);
                return true;
            });
            return Result.Success();
        }, true);
        mutation.IsSuccess.ShouldBeTrue();
        Error(await host.Audits.FindAsync(original.AuditId), "AuditDocumentInvalid");
        Error(await host.Audits.AppendAsync(original), "AuditDocumentInvalid");
    }

    private static AuditRecord Record(string key)
    {
        var id = Guid.NewGuid();
        var now = new EvidenceClock().GetUtcNow();
        return new() { AuditId = id, OriginAuditId = id, Key = key, StartedUtc = now.AddMinutes(-1), CompletedUtc = now, Outcome = AuditOutcome.Succeeded };
    }

    private static AuditPredicate Module(string value) => new() { Kind = "Comparison", Field = "ModuleName", Value = new() { Kind = "String", Text = value } };

    private static string Errors(IResult result) => string.Join(";", result.Errors.Select(error => error.ToString()));

    private static void Error(IResult result, string code)
    {
        result.IsFailure.ShouldBeTrue();
        result.Errors.OfType<AuditError>().Select(error => error.Code).ShouldContain(code, Errors(result));
    }
}

/// <summary>Provides disposable external key copies and a controllable missing-key failure.</summary>
public sealed class EvidenceKeys(EvidenceKeyRing state) : IAuditIntegrityKeyProvider
{
    /// <inheritdoc />
    public Task<Result<AuditIntegrityKey>> ResolveActiveAsync(CancellationToken cancellationToken = default) => this.ResolveAsync("evidence-key", cancellationToken);

    /// <inheritdoc />
    public Task<Result<AuditIntegrityKey>> ResolveAsync(string keyId, CancellationToken cancellationToken = default) => Task.FromResult(state.Available
        ? Result<AuditIntegrityKey>.Success(new("evidence-key", state.Material))
        : Result<AuditIntegrityKey>.Failure(new AuditError("AuditIntegrityKeyUnavailable")));
}

/// <summary>Owns external test material independently of operation-scoped key-provider instances.</summary>
public sealed class EvidenceKeyRing
{
    /// <summary>Gets randomly generated disposable test key material.</summary>
    public byte[] Material { get; } = RandomNumberGenerator.GetBytes(32);

    /// <summary>Gets or sets whether external resolution is available.</summary>
    public bool Available { get; set; } = true;
}
