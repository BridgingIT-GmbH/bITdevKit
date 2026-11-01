// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Auditing;

using System.Collections.Immutable;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Application.Auditing;
using Infrastructure.EntityFramework.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Auditing.Fixtures;
using BridgingIT.DevKit.Application.IntegrationTests.Auditing.Fixtures;

[Collection(nameof(AuditRelationalCollection))]
public sealed class EntityFrameworkAuditProviderContractTests(AuditRelationalFixture fixture)
{
    [Theory]
    [InlineData("SqlServer")]
    [InlineData("Postgres")]
    [InlineData("Sqlite")]
    public async Task AppendAsync_MaximumCapturedIdentities_PreservesExactValues(string backend)
    {
        var connection = await fixture.CreateDatabaseAsync(backend);
        using var host = await CreateAsync(backend, connection);
        var original = Record("identity-boundary") with
        {
            Actor = new() { UserId = new string('u', 1024) },
            InitiatingActor = new() { UserId = new string('i', 1024) },
            ModuleName = new string('m', 512)
        };
        var signer = host.Services.GetRequiredService<HmacSha256AuditIntegrityService>();
        var sealedRecord = (await signer.SealAsync(original)).Value;
        var provider = host.Services.GetRequiredService<IAuditingStorageProvider>();

        (await provider.AppendAsync(sealedRecord)).IsSuccess.ShouldBeTrue();
        var restored = await provider.FindAsync(original.AuditId);

        restored.IsSuccess.ShouldBeTrue();
        restored.Value.Actor.UserId.ShouldBe(original.Actor.UserId);
        restored.Value.InitiatingActor.UserId.ShouldBe(original.InitiatingActor.UserId);
        restored.Value.ModuleName.ShouldBe(original.ModuleName);
        (await signer.VerifyAsync(restored.Value)).Value.ShouldBe("Verified");
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("Postgres")]
    [InlineData("Sqlite")]
    public async Task Search_TruncationNotesInCurrentAndLegacyProjections_FiltersBeforeCount(string backend)
    {
        var connection = await fixture.CreateDatabaseAsync(backend);
        using var host = await CreateAsync(backend, connection);
        var signer = host.Services.GetRequiredService<HmacSha256AuditIntegrityService>();
        var provider = host.Services.GetRequiredService<IAuditingStorageProvider>();
        var original = Record("truncated") with
        {
            StartedUtc = DateTimeOffset.UtcNow.AddMinutes(-1), Completeness = AuditCompleteness.Incomplete,
            Notes = [new() { Code = "AuditRootIncomplete" }, new() { Code = "AuditValueTooLarge" }]
        };
        (await provider.AppendAsync((await signer.SealAsync(original)).Value)).IsSuccess.ShouldBeTrue();
        (await provider.AppendAsync((await signer.SealAsync(Record("complete") with { StartedUtc = original.StartedUtc })).Value)).IsSuccess.ShouldBeTrue();
        var plan = host.Services.GetRequiredService<AuditSearchPlanBuilder>().Build(new()
        {
            PageSize = 1, Filters = [new("HasTruncation", FilterOperator.Equal, true)]
        }).Value;

        for (var version = 2; version >= 1; version--)
        {
            if (version == 1)
            {
                await using var scope = host.Services.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<AuditMigrationContext>();
                var json = System.Text.Json.Nodes.JsonNode.Parse((await context.AuditRecords.SingleAsync(row => row.AuditId == original.AuditId)).SearchJson);
                json["version"] = 1;
                var flag = json["fields"][AuditSearchEncoding.FieldKey("HasTruncation")]["Boolean"];
                flag["exact"] = "0"; flag["folded"] = "0";
                using var document = System.Text.Json.JsonDocument.Parse(json.ToJsonString());
                var legacy = Encoding.UTF8.GetString(AuditCanonicalizerV1.Encode(document.RootElement));
                await context.AuditRecords.Where(row => row.AuditId == original.AuditId).ExecuteUpdateAsync(setters => setters.SetProperty(row => row.SearchJson, legacy));
            }

            var result = await provider.SearchAsync(plan);
            result.IsSuccess.ShouldBeTrue();
            result.TotalCount.ShouldBe(1);
            result.Value.Single().AuditId.ShouldBe(original.AuditId);
            var restored = await provider.FindAsync(original.AuditId);
            restored.IsSuccess.ShouldBeTrue();
            (await signer.VerifyAsync(restored.Value)).Value.ShouldBe("Verified");
        }
    }

    [Theory]
    [InlineData("SqlServer", "ndjson")]
    [InlineData("Postgres", "ndjson")]
    [InlineData("Sqlite", "ndjson")]
    [InlineData("SqlServer", "csv")]
    [InlineData("Postgres", "csv")]
    [InlineData("Sqlite", "csv")]
    public async Task Export_VerifiesOriginalGraphsSurvivesRestartAndInvalidatesFinalArchiveRace(string backend, string format)
    {
        var connection = await fixture.CreateDatabaseAsync(backend);
        var keys = new KeyRingState();
        AuditExportResume checkpoint;
        Guid store;
        using (var first = await CreateAsync(backend, connection, keys: keys))
        {
            var signer = first.Services.GetRequiredService<HmacSha256AuditIntegrityService>();
            var provider = first.Services.GetRequiredService<IAuditingStorageProvider>();
            var record = Record("customers.rename") with
            {
                Actions = Enumerable.Range(0, 80).Select(index => new AuditAction
                { Id = Guid.NewGuid(), Sequence = index, Key = "customers.save", DisplayName = new string('x', 500) }).ToImmutableArray()
            };
            (await provider.AppendAsync((await signer.SealAsync(record)).Value)).IsSuccess.ShouldBeTrue();
            await using var scope = first.Services.CreateAsyncScope();
            using var bytes = new MemoryStream();
            var completion = await (await scope.ServiceProvider.GetRequiredService<IAuditExportService>().OpenAsync(new() { Format = format })).Value.WriteAsync(bytes);
            completion.IsSuccess.ShouldBeTrue(string.Join(",", completion.Errors.Select(error => error is AuditExportFailureError failure ? failure.Completion.ReasonCode : error.ToString()))); completion.Value.VerificationCounts["Verified"].ShouldBe(1);
            checkpoint = completion.Value.Progress; store = checkpoint.StoreId;
        }

        using var restarted = await CreateAsync(backend, connection, migrate: false, keys: keys);
        var durable = restarted.Services.GetRequiredService<IAuditingStorageProvider>();
        var integrity = restarted.Services.GetRequiredService<HmacSha256AuditIntegrityService>();
        (await durable.AppendAsync((await integrity.SealAsync(Record("customers.late") with { StartedUtc = checkpoint.AsOfUtc.AddMinutes(-1) })).Value)).IsSuccess.ShouldBeTrue();
        await using var restartedScope = restarted.Services.CreateAsyncScope();
        var exports = restartedScope.ServiceProvider.GetRequiredService<IAuditExportService>();
        using var lateBytes = new MemoryStream();
        var late = await (await exports.OpenAsync(new() { Resume = checkpoint, Format = format })).Value.WriteAsync(lateBytes);
        late.IsSuccess.ShouldBeTrue(); late.Value.SourceRecordCount.ShouldBe(1); late.Value.Progress.StoreId.ShouldBe(store);
        var attempt = (await exports.OpenAsync(new() { Format = format })).Value;
        long frames = 0, count = 0;
        await foreach (var frame in attempt.ReadFramesAsync()) { using (frame) { frames++; count += frame.Bytes.Length; } }

        (await durable.ArchiveBeforeAsync(DateTimeOffset.UtcNow.AddMinutes(1))).IsSuccess.ShouldBeTrue();
        (await attempt.ConfirmAsync(new(NdjsonStreamStatus.Succeeded, true, true, frames, count, frames, count, false))).IsFailure.ShouldBeTrue();
        await attempt.DisposeAsync();
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("Postgres")]
    [InlineData("Sqlite")]
    public async Task QueryVectors_MatchMemoryAcrossUnicodeTypedDetailsAndChildBindings(string backend)
    {
        var connection = await fixture.CreateDatabaseAsync(backend);
        using var host = await CreateAsync(backend, connection);
        using var memory = new ServiceCollection().AddAuditing().WithSearchableDetail(new() { Name = "count", Kind = "Decimal" }).Services.BuildServiceProvider();
        var durable = host.Services.GetRequiredService<IAuditingStorageProvider>();
        var reference = memory.GetRequiredService<IAuditingStorageProvider>();
        var integrity = host.Services.GetRequiredService<HmacSha256AuditIntegrityService>();
        var now = DateTimeOffset.UtcNow;
        string[] labels = ["a", "A", "s", "ſ", "Ä", "ä", "Σ", "ς", "İ", "i", "𐐀", "𐐨", "\uE000", "😀", "|%_'\".\\", "last"];
        for (var index = 0; index < labels.Length; index++)
        {
            var id = Guid.Parse($"00000000-0000-0000-0000-{index + 1:D12}");
            var details = ImmutableDictionary<string, AuditValue>.Empty.Add("count", (index % 4) switch
            {
                0 => AuditValue.From(9_007_199_254_740_993m), 1 => AuditValue.From(-0.0000000000000000000000000001m),
                2 => AuditValue.From((string)null), _ => AuditValue.Policy("Redacted")
            });
            var record = new AuditRecord
            {
                AuditId = id, OriginAuditId = id, Key = labels[index], CorrelationId = labels[index], StartedUtc = now, CompletedUtc = now,
                DisplayName = index == 15 ? "" : index % 2 == 0 ? null : labels[index], Details = details,
                Actions = [new() { Id = Guid.NewGuid(), Key = "rename", Outcome = AuditOutcome.Failed, Sequence = 0 },
                    new() { Id = Guid.NewGuid(), Key = index % 2 == 0 ? "rename" : "save", Outcome = AuditOutcome.Succeeded, Sequence = 1 }],
                EntityChanges = [new() { Id = Guid.NewGuid(), EntityType = "Customer", EntityId = "C-1", PersistenceState = "Unknown", Properties = [new() { Path = "Name" }] },
                    new() { Id = Guid.NewGuid(), EntityType = "Customer", EntityId = "C-2", PersistenceState = "Committed", Properties = [new() { Path = "Address" }] }]
            };
            (await reference.AppendAsync(record)).IsSuccess.ShouldBeTrue();
            (await durable.AppendAsync((await integrity.SealAsync(record)).Value)).IsSuccess.ShouldBeTrue();
        }

        var planner = host.Services.GetRequiredService<AuditSearchPlanBuilder>();
        var vectors = new List<FilterModel>();
        foreach (var label in labels)
        {
            vectors.Add(new() { Filters = [new("Key", FilterOperator.Equal, label)] });
            vectors.Add(new() { Filters = [new("CorrelationId", FilterOperator.Equal, label)] });
            if (label.Length > 0) vectors.Add(new() { Filters = [new("Key", FilterOperator.Contains, label)] });
        }

        vectors.AddRange([
            new() { Filters = [new("Details.count", FilterOperator.GreaterThan, 9_007_199_254_740_992m)] },
            new() { Filters = [new("Details.count", FilterOperator.LessThan, 0m)] },
            new() { Filters = [new("Details.count", FilterOperator.NotEqual, 0m)] },
            new() { Filters = [new("Details.count", FilterOperator.IsNull, null)] },
            new() { Filters = [new("Details.count.Marker", FilterOperator.Equal, "Redacted")] },
            new() { Filters = [new("DisplayName", FilterOperator.IsEmpty, null)] },
            new() { Filters = [new("Actions", FilterOperator.Any, null) { Filters = [new("Key", FilterOperator.Equal, "rename"), new("Outcome", FilterOperator.Equal, "Succeeded")] }] },
            new() { Filters = [new("Actions", FilterOperator.All, null) { Filters = [new("Key", FilterOperator.Equal, "rename")] }] },
            new() { Filters = [new("Actions", FilterOperator.None, null) { Filters = [new("DisplayName", FilterOperator.Equal, "missing")] }] },
            new() { Filters = [new("EntityChanges", FilterOperator.Any, null) { Filters = [new("EntityType", FilterOperator.Equal, "Customer"), new("Properties", FilterOperator.Any, null) { Filters = [new("Path", FilterOperator.Equal, "Name")] }] }] },
            new() { Filters = [new("audit.action-match", [new FilterCriteria("Key", FilterOperator.Equal, "rename"), new FilterCriteria("Outcome", FilterOperator.Equal, "Succeeded")])] },
            new() { Page = 2, PageSize = 3, Orderings = [new() { Field = "Key", Direction = OrderDirection.Ascending }] },
            new() { PageSize = 200, Orderings = [new() { Field = "Details.count", Direction = OrderDirection.Descending }] },
            new() { PageSize = 250 },
            new() { PageSize = 500 }

        ]);
        foreach (var filter in vectors)
        {
            var built = planner.Build(filter, withRange: false);
            built.IsSuccess.ShouldBeTrue();
            var expected = await reference.SearchAsync(built.Value);
            expected.IsSuccess.ShouldBeTrue();
            var actual = await durable.SearchAsync(built.Value);
            actual.IsSuccess.ShouldBeTrue(string.Join(";", actual.Errors.Select(error => error.ToString())));
            actual.TotalCount.ShouldBe(expected.TotalCount, System.Text.Json.JsonSerializer.Serialize(filter));
            actual.Value.Select(item => item.AuditId).ShouldBe(expected.Value.Select(item => item.AuditId));
        }

        var selection = planner.Build(null, withRange: false).Value;
        foreach (var field in new[] { "Key", "DisplayName" })
        {
            var expected = (await reference.AggregateAsync(new() { Selection = selection, Field = field })).Value;
            var actual = await durable.AggregateAsync(new() { Selection = selection, Field = field });
            actual.IsSuccess.ShouldBeTrue();
            actual.Value.ShouldBe(expected);
        }

        (await durable.FindFacetsAsync(new() { Selection = selection, Prefix = "𐐨" })).Value
            .ShouldBe((await reference.FindFacetsAsync(new() { Selection = selection, Prefix = "𐐨" })).Value);
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("Postgres")]
    [InlineData("Sqlite")]
    public async Task BufferedHost_RetainsInFlightCapacityAndDrainsAfterProducerStop(string backend)
    {
        var connection = await fixture.CreateDatabaseAsync(backend);
        var gate = new WriteGate();
        var keys = new KeyRingState();
        using var host = await CreateAsync(backend, connection, buffered: true, gate: gate, keys: keys, stopProducer: true);
        await host.StartAsync();
        var provider = host.Services.GetRequiredService<IAuditingStorageProvider>();
        var integrity = host.Services.GetRequiredService<HmacSha256AuditIntegrityService>();
        var first = (await integrity.SealAsync(Record("first"))).Value;
        var accepted = await provider.AppendAsync(first);
        accepted.Value.State.ShouldBe("Queued");
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            (await provider.AppendAsync(first)).Value.ShouldBeSameAs(accepted.Value);
            keys.Active = "key-two";
            var second = (await integrity.SealAsync(Record("second"))).Value;
            (await provider.AppendAsync(second)).Value.State.ShouldBe("Queued");
            var rejected = await provider.AppendAsync((await integrity.SealAsync(Record("over-capacity"))).Value);
            rejected.Errors.OfType<AuditError>().Single().Code.ShouldBe("AuditQueueFull");
            await using var scope = host.Services.CreateAsyncScope();
            (await scope.ServiceProvider.GetRequiredService<AuditMigrationContext>().AuditRecords.CountAsync()).ShouldBe(0);
        }
        finally
        {
            gate.Release.TrySetResult();
        }

        var flushed = await provider.FlushAsync();
        flushed.IsSuccess.ShouldBeTrue();
        flushed.Value.Persisted.ShouldBe(2);
        var found = (await provider.FindAsync(first.AuditId)).Value;
        found.IntegrityProtection.KeyId.ShouldBe("key-one");
        found.IntegrityProtection.Tag.ShouldBe(first.IntegrityProtection.Tag);
        await host.StopAsync();
        host.Services.GetRequiredService<StopProducer>().Calls.ShouldBe(1);
        (await provider.FlushAsync()).Value.Persisted.ShouldBe(3);
        using var read = host.Services.CreateScope();
        (await read.ServiceProvider.GetRequiredService<IAuditQueryService>().SearchAsync()).Value.Select(item => item.Key).ShouldContain("producers.stop");
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("Postgres")]
    [InlineData("Sqlite")]
    public async Task BufferedConflict_RetainsHistoricalFailureAfterLaterSuccess(string backend)
    {
        var connection = await fixture.CreateDatabaseAsync(backend);
        using var host = await CreateAsync(backend, connection, buffered: true);
        await host.StartAsync();
        var provider = host.Services.GetRequiredService<IAuditingStorageProvider>();
        var integrity = host.Services.GetRequiredService<HmacSha256AuditIntegrityService>();
        var original = Record("original");
        (await provider.AppendAsync((await integrity.SealAsync(original)).Value)).IsSuccess.ShouldBeTrue();
        (await provider.FlushAsync()).IsSuccess.ShouldBeTrue();
        (await provider.AppendAsync((await integrity.SealAsync(original with { Key = "conflict" })).Value)).IsSuccess.ShouldBeTrue();
        var failed = await provider.FlushAsync();
        failed.IsFailure.ShouldBeTrue();
        failed.Errors.OfType<AuditFlushIncompleteError>().Single().FlushResult.Failed.ShouldBe(1);
        (await provider.AppendAsync((await integrity.SealAsync(Record("later"))).Value)).IsSuccess.ShouldBeTrue();
        var later = await provider.FlushAsync();
        later.IsFailure.ShouldBeTrue();
        var counts = later.Errors.OfType<AuditFlushIncompleteError>().Single().FlushResult;
        counts.Failed.ShouldBe(1);
        counts.Persisted.ShouldBe(2);
        counts.Pending.ShouldBe(0);
        host.Services.GetRequiredService<AuditRuntime>().SnapshotFailures()["AuditDeliveryFailed"].ShouldBe(1);
        await host.StopAsync();
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("Postgres")]
    [InlineData("Sqlite")]
    public async Task MissingSchema_FailsReadinessWithoutCreatingTablesOrFallingBack(string backend)
    {
        var connection = await fixture.CreateDatabaseAsync(backend);
        using var host = await CreateAsync(backend, connection, migrate: false);
        (await host.Services.GetRequiredService<Operations>().Readiness()).IsFailure.ShouldBeTrue();
        (await host.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync()).Entries["Auditing"].Status.ShouldBe(HealthStatus.Unhealthy);
        await Should.ThrowAsync<InvalidOperationException>(() => host.StartAsync());
        host.Services.GetRequiredService<IAuditingStorageProvider>().Capabilities.IsDurable.ShouldBeTrue();
        await using var scope = host.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuditMigrationContext>();
        (await context.Database.GetAppliedMigrationsAsync()).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("Postgres")]
    [InlineData("Sqlite")]
    public async Task Publication_WaitsForWholeGraphCommitAndEpochRotationInvalidatesOldWindows(string backend)
    {
        var connection = await fixture.CreateDatabaseAsync(backend);
        var gate = new WriteGate();
        using var host = await CreateAsync(backend, connection, gate: gate);
        var provider = host.Services.GetRequiredService<IAuditingStorageProvider>();
        var integrity = host.Services.GetRequiredService<HmacSha256AuditIntegrityService>();
        var record = Record("whole-graph") with
        {
            Actions = [new() { Id = Guid.NewGuid(), Key = "first" }, new() { Id = Guid.NewGuid(), Key = "second" }]
        };
        var append = provider.AppendAsync((await integrity.SealAsync(record)).Value);
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var plan = host.Services.GetRequiredService<AuditSearchPlanBuilder>().Build(null, withRange: false).Value;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var opening = Task.Run(() =>
        {
            started.SetResult();
            return provider.OpenExtractionAsync(new() { Selection = plan });
        });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            opening.IsCompleted.ShouldBeFalse();
        }
        finally
        {
            gate.Release.TrySetResult();
        }

        (await append).IsSuccess.ShouldBeTrue();
        var cursor = (await opening).Value;
        cursor.UpperPosition.ShouldBe(1);
        var batch = await provider.ReadExtractionAsync(cursor);
        batch.IsSuccess.ShouldBeTrue();
        batch.Value.Items.Single().Record.Actions.Length.ShouldBe(2);
        var operations = host.Services.GetRequiredService<Operations>();
        var rotated = await operations.Rotate(cursor.StoreEpoch);
        rotated.IsSuccess.ShouldBeTrue();
        rotated.Value.ShouldNotBe(cursor.StoreEpoch);
        (await provider.ReadExtractionAsync(cursor)).Errors.OfType<AuditError>().Single().Code.ShouldBe("AuditEpochChanged");
        (await operations.Rotate(cursor.StoreEpoch)).IsFailure.ShouldBeTrue();
        var current = (await provider.OpenExtractionAsync(new() { Selection = plan })).Value;
        current.StoreEpoch.ShouldBe(rotated.Value);
        current.StoreId.ShouldBe(cursor.StoreId);
        current.UpperPosition.ShouldBe(1);
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("Postgres")]
    [InlineData("Sqlite")]
    public async Task TamperedStoredGraph_IsRejectedBeforeDetailExposureOrDuplicateAcknowledgment(string backend)
    {
        var connection = await fixture.CreateDatabaseAsync(backend);
        using var host = await CreateAsync(backend, connection);
        var provider = host.Services.GetRequiredService<IAuditingStorageProvider>();
        var integrity = host.Services.GetRequiredService<HmacSha256AuditIntegrityService>();
        foreach (var mutation in new[] { "scalar", "source", "projection", "tag", "child" })
        {
            var original = (await integrity.SealAsync(Record("tampering." + mutation) with { Actions = [new() { Id = Guid.NewGuid(), Key = "original-child" }] })).Value;
            (await provider.AppendAsync(original)).IsSuccess.ShouldBeTrue();
            await using (var scope = host.Services.CreateAsyncScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AuditMigrationContext>();
                var row = await context.AuditRecords.SingleAsync(item => item.AuditId == original.AuditId);
                switch (mutation)
                {
                    case "scalar": row.Key = "forged"; break;
                    case "projection": row.SearchJson += " "; break;
                    case "source":
                        var source = System.Text.Json.Nodes.JsonNode.Parse(row.RootJson);
                        source["key"] = "forged";
                        row.RootJson = source.ToJsonString();
                        break;
                    case "tag":
                        var protection = System.Text.Json.Nodes.JsonNode.Parse(row.IntegrityJson);
                        protection["tag"] = Convert.ToBase64String(new byte[32]);
                        row.IntegrityJson = protection.ToJsonString();
                        break;
                    default:
                        var child = await context.AuditActions.SingleAsync(item => item.AuditId == original.AuditId);
                        var document = System.Text.Json.Nodes.JsonNode.Parse(child.DocumentJson);
                        document["key"] = "forged";
                        child.DocumentJson = document.ToJsonString();
                        break;
                }

                await context.SaveChangesAsync();
            }

            using var read = host.Services.CreateScope();
            (await read.ServiceProvider.GetRequiredService<IAuditQueryService>().FindAsync(original.AuditId)).IsFailure.ShouldBeTrue(mutation);
            (await provider.AppendAsync(original)).IsFailure.ShouldBeTrue(mutation);
            var opened = await read.ServiceProvider.GetRequiredService<IAuditExportService>().OpenAsync(new() { Filter = new() { Filters = [new("AuditId", FilterOperator.Equal, original.AuditId)] } });
            opened.IsSuccess.ShouldBeTrue();
            using var output = new MemoryStream();
            var exported = await opened.Value.WriteAsync(output);
            exported.IsFailure.ShouldBeTrue(mutation);
            exported.Errors.OfType<AuditExportFailureError>().Single().Completion.Progress.ShouldBeNull();
            var text = Encoding.UTF8.GetString(output.ToArray()); text.ShouldNotContain("forged"); text.ShouldNotContain("original-child");
        }
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("Postgres")]
    [InlineData("Sqlite")]
    public async Task Extraction_ByteLimitPreservesTheNextRecordAndRejectsAnOversizedEmptyBatch(string backend)
    {
        var connection = await fixture.CreateDatabaseAsync(backend);
        using var host = await CreateAsync(backend, connection);
        var provider = host.Services.GetRequiredService<IAuditingStorageProvider>();
        var integrity = host.Services.GetRequiredService<HmacSha256AuditIntegrityService>();
        var first = (await integrity.SealAsync(Record("first"))).Value;
        (await provider.AppendAsync(first)).IsSuccess.ShouldBeTrue();
        (await provider.AppendAsync((await integrity.SealAsync(Record("second"))).Value)).IsSuccess.ShouldBeTrue();
        var selection = host.Services.GetRequiredService<AuditSearchPlanBuilder>().Build(null, withRange: false).Value;
        var cursor = (await provider.OpenExtractionAsync(new() { Selection = selection, MaxBatchBytes = AuditRecordCodec.Encode(first).Length + 32 })).Value;
        var batch = await provider.ReadExtractionAsync(cursor);
        batch.IsSuccess.ShouldBeTrue();
        batch.Value.Items.Single().Record.AuditId.ShouldBe(first.AuditId);
        batch.Value.Exhausted.ShouldBeFalse();
        batch.Value.Cursor.AfterPosition.ShouldBe(1);
        (await provider.ReadExtractionAsync(batch.Value.Cursor)).Value.Items.Single().Position.ShouldBe(2);
        (await provider.ReadExtractionAsync(cursor with { MaxRecordBytes = 128 })).Errors.OfType<AuditError>().Single().Code.ShouldBe("AuditRecordTooLarge");
        cursor.AfterPosition.ShouldBe(0);
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("Postgres")]
    [InlineData("Sqlite")]
    public async Task MissingChildTable_WithControlStatePresent_StillFailsReadiness(string backend)
    {
        var connection = await fixture.CreateDatabaseAsync(backend);
        using var host = await CreateAsync(backend, connection);
        var health = host.Services.GetRequiredService<HealthCheckService>();
        var healthy = await health.CheckHealthAsync();
        healthy.Entries["Auditing"].Status.ShouldBe(HealthStatus.Healthy);
        healthy.Entries["Auditing"].Data["provider"].ShouldBe("EntityFramework");
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AuditMigrationContext>();
            await context.Database.OpenConnectionAsync();
            var dialect = new AuditSqlDialect(context);
            await using var command = dialect.Command("DROP TABLE " + dialect.Table<AuditActionEntity>());
            await command.ExecuteNonQueryAsync();
        }

        (await host.Services.GetRequiredService<Operations>().Readiness()).IsFailure.ShouldBeTrue();
        (await health.CheckHealthAsync()).Entries["Auditing"].Status.ShouldBe(HealthStatus.Unhealthy);
        await Should.ThrowAsync<InvalidOperationException>(() => host.StartAsync());
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("Postgres")]
    [InlineData("Sqlite")]
    public async Task HealthCheck_InitializedStore_ReportsReadyWithoutWritingEvidence(string backend)
    {
        var connection = await fixture.CreateDatabaseAsync(backend);
        using var host = await CreateAsync(backend, connection);

        var report = await host.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync();

        var entry = report.Entries["Auditing"];
        entry.Status.ShouldBe(HealthStatus.Healthy);
        entry.Data["provider"].ShouldBe("EntityFramework");
        entry.Data["durable"].ShouldBe(true);
        await using var scope = host.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuditMigrationContext>();
        (await context.AuditRecords.CountAsync()).ShouldBe(0);
        (await context.AuditActions.CountAsync()).ShouldBe(0);
        host.Services.GetRequiredService<AuditRuntime>().SnapshotObservations().ShouldBeEmpty();
    }

    private static AuditRecord Record(string key)
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        return new() { AuditId = id, OriginAuditId = id, Key = key, StartedUtc = now, CompletedUtc = now, Outcome = AuditOutcome.Succeeded };
    }

    public static Task<IHost> CreateAsync(string backend, Func<DbConnection> connection, bool buffered = false, bool migrate = true, WriteGate gate = null, KeyRingState keys = null, bool stopProducer = false)
    {
        using var source = connection();
        var text = source.ConnectionString;
        return backend switch
        {
            "SqlServer" => CreateAsync<SqlServerAuditMigrationContext>(options => options.UseSqlServer(text), buffered, migrate, gate, keys, stopProducer),
            "Postgres" => CreateAsync<PostgresAuditMigrationContext>(options => options.UseNpgsql(text), buffered, migrate, gate, keys, stopProducer),
            _ => CreateAsync<SqliteAuditMigrationContext>(options => options.UseSqlite(text), buffered, migrate, gate, keys, stopProducer)
        };
    }

    private static async Task<IHost> CreateAsync<TContext>(Action<DbContextOptionsBuilder> configure, bool buffered, bool migrate, WriteGate gate, KeyRingState keys, bool stopProducer)
        where TContext : AuditMigrationContext
    {
        var host = new HostBuilder().ConfigureServices(services =>
        {
            services.AddDbContext<TContext>(options => { configure(options); if (gate is not null) options.AddInterceptors(gate); });
            services.AddScoped<AuditMigrationContext>(provider => provider.GetRequiredService<TContext>());
            services.AddSingleton(keys ?? new());
            services.AddAuditing().WithSearchableDetail(new() { Name = "count", Kind = "Decimal" })
                .WithHmacSha256<ExternalKeys>().WithEntityFrameworkProvider<TContext>(options => options.Configure(value => value with
                { Buffered = buffered, BatchSize = 1, QueueRecordLimit = 2, RetryCount = 0 }));
            services.AddSingleton(provider => new Operations(
                () => provider.GetRequiredService<EntityFrameworkAuditStorageProvider<TContext>>().CheckReadinessAsync(),
                expected => provider.GetRequiredService<EntityFrameworkAuditStorageProvider<TContext>>().RotateStoreEpochAsync(expected)));
            if (stopProducer)
            {
                services.AddSingleton<StopProducer>();
                services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<StopProducer>());
            }
        }).Build();
        if (migrate)
        {
            await using var scope = host.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<TContext>().Database.MigrateAsync();
        }

        return host;
    }

    public sealed class KeyRingState
    {
        public string Active { get; set; } = "key-one";
    }

    public sealed record Operations(Func<Task<Result<AuditProviderCapabilities>>> Readiness, Func<Guid, Task<Result<Guid>>> Rotate);

    public sealed class ExternalKeys(KeyRingState state) : IAuditIntegrityKeyProvider
    {
        public Task<Result<AuditIntegrityKey>> ResolveActiveAsync(CancellationToken cancellationToken = default) => this.ResolveAsync(state.Active, cancellationToken);
        public Task<Result<AuditIntegrityKey>> ResolveAsync(string keyId, CancellationToken cancellationToken = default) => Task.FromResult(Result<AuditIntegrityKey>.Success(
            new(keyId, SHA256.HashData(Encoding.UTF8.GetBytes("fictional-test-key-material-" + keyId)))));
    }

    public sealed class WriteGate : SaveChangesInterceptor
    {
        private int calls;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref this.calls) == 1)
            {
                this.Entered.TrySetResult();
                await this.Release.Task.WaitAsync(cancellationToken);
            }

            return result;
        }
    }

    public sealed class StopProducer(IServiceScopeFactory scopes) : IHostedService
    {
        public int Calls { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            this.Calls++;
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IAuditContext>().RunAsync(builder => builder.Key("producers.stop"), (_, _) => Task.FromResult(1), cancellationToken);
        }
    }
}
