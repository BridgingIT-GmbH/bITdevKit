// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Auditing;

using System.Data.Common;
using Application.Auditing;
using Microsoft.Extensions.DependencyInjection;
using BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Auditing.Fixtures;

[Collection(nameof(AuditRelationalCollection))]
public sealed class AuditRestoreTests(AuditRelationalFixture fixture)
{
    [Theory]
    [InlineData("SqlServer")]
    [InlineData("Postgres")]
    [InlineData("Sqlite")]
    public async Task NativeBackupRestore_PreservesArchivedEvidenceAndRetainedKeysAndRequiresEpochRotation(string backend)
    {
        var sourceFactory = await fixture.CreateDatabaseAsync(backend);
        var keys = new EntityFrameworkAuditProviderContractTests.KeyRingState();
        var now = DateTimeOffset.UtcNow; var id = Guid.NewGuid();
        var original = new AuditRecord
        {
            AuditId = id, OriginAuditId = id, Key = "customers.rename", DisplayName = "Rename archived customer", StartedUtc = now, CompletedUtc = now, Outcome = AuditOutcome.Succeeded,
            Actor = new() { Kind = "User", UserId = "removed-user", DisplayName = "Former customer editor", IsAuthenticated = true },
            Resources = [new("Customer", "removed-customer", "Original customer label")],
            Details = System.Collections.Immutable.ImmutableDictionary<string, AuditValue>.Empty.Add("classification", AuditValue.Policy("Redacted")),
            EntityChanges = [new() { EntityType = "Customer", EntityId = "removed-customer", Operation = "Update", PersistenceState = "Committed", CopyMode = "ReferenceOnly", SourceStoreKey = "CustomerPortal", SourceRowIds = ["removed-source-row"] }]
        };
        AuditExportResume checkpoint; string hash;
        using (var source = await EntityFrameworkAuditProviderContractTests.CreateAsync(backend, sourceFactory, keys: keys))
        {
            var provider = source.Services.GetRequiredService<IAuditingStorageProvider>();
            var sealedRecord = (await source.Services.GetRequiredService<HmacSha256AuditIntegrityService>().SealAsync(original)).Value;
            (await provider.AppendAsync(sealedRecord)).IsSuccess.ShouldBeTrue(); hash = sealedRecord.ContentHash;
            await RemoveLiveTablesAsync(sourceFactory);
            (await provider.ArchiveBeforeAsync(now.AddMinutes(1))).IsSuccess.ShouldBeTrue();
            await using var read = source.Services.CreateAsyncScope(); using var output = new MemoryStream();
            var exported = await (await read.ServiceProvider.GetRequiredService<IAuditExportService>().OpenAsync(new() { Filter = new() { Filters = [new("StorageState", FilterOperator.IsNotNull, null)] } })).Value.WriteAsync(output);
            exported.IsSuccess.ShouldBeTrue(); checkpoint = exported.Value.Progress;
        }

        var restoredFactory = await fixture.BackupRestoreCopyAsync(backend, sourceFactory);
        keys.Active = "key-two";
        using var restored = await EntityFrameworkAuditProviderContractTests.CreateAsync(backend, restoredFactory, migrate: false, keys: keys);
        var store = restored.Services.GetRequiredService<IAuditingStorageProvider>();
        var operations = restored.Services.GetRequiredService<EntityFrameworkAuditProviderContractTests.Operations>();
        var rotated = await operations.Rotate(checkpoint.StoreEpoch); rotated.IsSuccess.ShouldBeTrue(); rotated.Value.ShouldNotBe(checkpoint.StoreEpoch);
        await using var scope = restored.Services.CreateAsyncScope();
        var details = await scope.ServiceProvider.GetRequiredService<IAuditQueryService>().FindAsync(id); details.IsSuccess.ShouldBeTrue();
        details.Value.StorageState.ShouldBe("Archived"); details.Value.IntegrityStatus.ShouldBe("Verified");
        details.Value.Record.Actor.UserId.ShouldBe("removed-user"); details.Value.Record.Actor.DisplayName.ShouldBe("Former customer editor");
        details.Value.Record.Resources.Single().DisplayName.ShouldBe("Original customer label"); details.Value.Record.StartedUtc.ShouldBe(now);
        details.Value.Record.Details["classification"].Marker.ShouldBe("Redacted"); details.Value.Record.EntityChanges.Single().SourceRowIds.ShouldBe(["removed-source-row"]);
        details.Value.Record.ContentHash.ShouldBe(hash); details.Value.Record.IntegrityProtection.KeyId.ShouldBe("key-one");
        (await scope.ServiceProvider.GetRequiredService<IAuditExportService>().OpenAsync(new() { Resume = checkpoint, Filter = new() { Filters = [new("StorageState", FilterOperator.IsNotNull, null)] } })).IsFailure.ShouldBeTrue();
        var current = (await store.OpenExtractionAsync(new() { Selection = restored.Services.GetRequiredService<AuditSearchPlanBuilder>().Build(new() { Filters = [new("StorageState", FilterOperator.IsNotNull, null)] }, withRange: false).Value })).Value;
        current.StoreId.ShouldBe(checkpoint.StoreId); current.StoreEpoch.ShouldBe(rotated.Value); current.UpperPosition.ShouldBe(checkpoint.UpperPosition);
        var second = (await restored.Services.GetRequiredService<HmacSha256AuditIntegrityService>().SealAsync(original with { AuditId = Guid.NewGuid(), ContentHash = null, IntegrityProtection = null, EntityChanges = [] })).Value;
        second.IntegrityProtection.KeyId.ShouldBe("key-two"); (await store.AppendAsync(second)).IsSuccess.ShouldBeTrue();
    }
    private static async Task RemoveLiveTablesAsync(Func<DbConnection> factory)
    {
        await using var connection = factory(); await connection.OpenAsync(); await using var command = connection.CreateCommand();
        foreach (var table in new[] { "CustomerPortalActors", "CustomerPortalCustomers", "CustomerPortalSourceHistory" })
        {
            command.CommandText = $"CREATE TABLE {table} (Id varchar(64) PRIMARY KEY)"; await command.ExecuteNonQueryAsync();
            var id = table.EndsWith("Actors", StringComparison.Ordinal) ? "removed-user" : table.EndsWith("Customers", StringComparison.Ordinal) ? "removed-customer" : "removed-source-row";
            command.CommandText = $"INSERT INTO {table} (Id) VALUES ('{id}')"; await command.ExecuteNonQueryAsync();
            command.CommandText = $"DROP TABLE {table}"; await command.ExecuteNonQueryAsync();
        }
    }
}
