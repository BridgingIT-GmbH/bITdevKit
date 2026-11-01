// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Auditing;

using System.Collections.Immutable;
using System.Data.Common;
using System.Text;
using Application.Auditing;
using Infrastructure.EntityFramework.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Auditing.Fixtures;
using BridgingIT.DevKit.Application.IntegrationTests.Auditing.Fixtures;

[Collection(nameof(AuditRelationalCollection))]
public sealed class EntityFrameworkAuditFoundationTests(AuditRelationalFixture fixture)
{
    [Theory]
    [InlineData("SqlServer")]
    [InlineData("Postgres")]
    [InlineData("Sqlite")]
    public async Task RealMigration_AppendReadAndProtectedDuplicate_UseIndependentWholeGraphs(string backend)
    {
        var connection = await fixture.CreateDatabaseAsync(backend);
        await using var host = await CreateAsync(backend, connection);
        var provider = host.GetRequiredService<IAuditingStorageProvider>();
        var integrity = host.GetRequiredService<HmacSha256AuditIntegrityService>();
        var id = Guid.NewGuid();
        var resource = new AuditResourceReference("Customer", "C-42");
        var now = DateTimeOffset.UtcNow;
        var record = new AuditRecord
        {
            AuditId = id, OriginAuditId = id, Key = "customers.rename", StartedUtc = now.AddMinutes(-1), CompletedUtc = now,
            Outcome = AuditOutcome.Succeeded, TotalActionCount = 2, HasFailedActions = true,
            Actor = new() { Kind = "Authenticated", UserId = "U-42", DisplayName = "Customer editor", IsAuthenticated = true },
            Resources = [resource], Details = ImmutableDictionary<string, AuditValue>.Empty.Add("count", AuditValue.From(9_007_199_254_740_993m)),
            Actions = [new() { Id = Guid.NewGuid(), Key = "customers.validate", Sequence = 10, Outcome = AuditOutcome.Failed, Resources = [resource] },
                new() { Id = Guid.NewGuid(), Key = "customers.save", Sequence = 1, Outcome = BridgingIT.DevKit.Common.AuditOutcome.Succeeded }],
            EntityChanges = [new() { Id = Guid.NewGuid(), EntityType = "Customer", EntityId = "C-42", PersistenceState = "Committed", Timestamp = now, Properties = [new() { Path = "Name", NewValue = AuditValue.From("Revised") }] }]
        };
        var sealedRecord = (await integrity.SealAsync(record)).Value;
        var appended = await provider.AppendAsync(sealedRecord);
        appended.IsSuccess.ShouldBeTrue(string.Join(";", appended.Errors.Select(error => error.ToString())));
        appended.Value.State.ShouldBe("Persisted");
        var found = await provider.FindAsync(id);
        found.IsSuccess.ShouldBeTrue(string.Join(";", found.Errors.Select(error => error.ToString())));
        AuditRecordCodec.Encode(found.Value).ShouldBe(AuditRecordCodec.Encode(sealedRecord));
        (await integrity.VerifyAsync(found.Value)).Value.ShouldBe("Verified");
        var duplicate = await provider.AppendAsync(sealedRecord);
        duplicate.IsSuccess.ShouldBeTrue(string.Join(";", duplicate.Errors.Select(error => error.ToString())));
        duplicate.Value.State.ShouldBe("AlreadyPresent");
        var conflict = await integrity.SealAsync(record with { Key = "different" });
        (await provider.AppendAsync(conflict.Value)).Errors.OfType<AuditError>().Single().Code.ShouldBe("AuditIdentityConflict");
        var stored = (await provider.FindStoredAsync(id, new())).Value;
        stored.Position.ShouldBe(1);
        stored.StorageState.ShouldBe("Active");
        for (var index = 0; index < 5; index++)
        {
            var next = Guid.NewGuid();
            var copy = record with { AuditId = next, OriginAuditId = next, Actions = record.Actions.Select(action => action with { Id = Guid.NewGuid() }).ToImmutableArray(), EntityChanges = record.EntityChanges.Select(change => change with { Id = Guid.NewGuid() }).ToImmutableArray() };
            (await provider.AppendAsync((await integrity.SealAsync(copy)).Value)).IsSuccess.ShouldBeTrue();
        }

        using var queriesScope = host.CreateScope();
        var queries = queriesScope.ServiceProvider.GetRequiredService<IAuditQueryService>();
        var filter = new FilterModel { PageSize = 2, Page = 2, Filters = [new("Details.count", FilterOperator.Equal, 9_007_199_254_740_993m), new("Actions", FilterOperator.Any, null) { Filters = [new("Key", FilterOperator.Equal, "CUSTOMERS.VALIDATE"), new("Outcome", FilterOperator.Equal, "failed")] }] };
        var page = await queries.SearchAsync(filter);
        page.IsSuccess.ShouldBeTrue(string.Join(";", page.Errors.Select(error => error.ToString())));
        page.TotalCount.ShouldBe(6);
        page.Value.Count().ShouldBe(2);
        filter.Filters[1].Filters[1].Value = "Succeeded";
        (await queries.SearchAsync(filter)).TotalCount.ShouldBe(0);
        var groups = await queries.AggregateAsync(null, "Key");
        groups.IsSuccess.ShouldBeTrue(string.Join(";", groups.Errors.Select(error => error.ToString())));
        groups.Value.Single().Count.ShouldBe(6);
        (await queries.FindFacetsAsync(null, "Key", "customers.r")).Value.ShouldBe(["customers.rename"]);
        var history = await queries.GetResourceHistoryAsync(resource, new() { PageSize = 1 });
        history.IsSuccess.ShouldBeTrue(string.Join(";", history.Errors.Select(error => error.ToString())));
        history.TotalCount.ShouldBe(6);
        history.Value.Single().MatchingActionCount.ShouldBe(1);
        var planner = host.GetRequiredService<AuditSearchPlanBuilder>();
        var activeCursor = (await provider.OpenExtractionAsync(new() { Selection = planner.Build(null).Value, BatchSize = 2 })).Value;
        var allSelection = planner.Build(new() { Filters = [new(FilterCustomType.EnumValues, new() { ["field"] = "StorageState", ["values"] = new[] { "Active", "Archived" } })] }).Value;
        allSelection.RequiresStableLifecycle.ShouldBeFalse();
        var allCursor = (await provider.OpenExtractionAsync(new() { Selection = allSelection, BatchSize = 2 })).Value;
        (await provider.ArchiveBeforeAsync(now.AddSeconds(1))).Value.ShouldBe(6);
        (await provider.ReadExtractionAsync(activeCursor)).Errors.OfType<AuditError>().Single().Code.ShouldBe("AuditLifecycleChanged");
        var lateId = Guid.NewGuid();
        var late = record with { AuditId = lateId, OriginAuditId = lateId, StartedUtc = now.AddHours(-2), Actions = [], EntityChanges = [] };
        (await provider.AppendAsync((await integrity.SealAsync(late)).Value)).IsSuccess.ShouldBeTrue();
        (await provider.FindStoredAsync(lateId, new())).Value.StorageState.ShouldBe("Archived");
        var seen = new List<Guid>();
        var window = allCursor;
        while (true)
        {
            var batch = await provider.ReadExtractionAsync(window);
            batch.IsSuccess.ShouldBeTrue(string.Join(";", batch.Errors.Select(error => error.ToString())));
            seen.AddRange(batch.Value.Items.Select(item => item.Record.AuditId));
            window = batch.Value.Cursor;
            if (batch.Value.Exhausted) break;
        }

        seen.Distinct().Count().ShouldBe(6);
        seen.ShouldNotContain(lateId);
        var nextWindow = (await provider.OpenExtractionAsync(new() { Selection = allSelection, AfterPosition = window.AfterPosition, StoreId = window.StoreId, StoreEpoch = window.StoreEpoch, AvailabilityBaseline = window.AvailabilityBaseline })).Value;
        (await provider.ReadExtractionAsync(nextWindow)).Value.Items.Single().Record.AuditId.ShouldBe(lateId);
        AuditRecordCodec.Encode((await provider.FindAsync(id)).Value).ShouldBe(AuditRecordCodec.Encode(sealedRecord));
        (await provider.RemoveArchivedBeforeAsync(now.AddSeconds(1))).Value.ShouldBe(7);
        (await provider.ReadExtractionAsync(allCursor)).Errors.OfType<AuditError>().Single().Code.ShouldBe("AuditHistoryGap");
        (await provider.AppendAsync(sealedRecord)).Errors.OfType<AuditError>().Single().Code.ShouldBe("AuditEvidenceExpired");
        var retainedOnly = (await provider.OpenExtractionAsync(new() { Selection = allSelection })).Value;
        retainedOnly.StoreId.ShouldBe(allCursor.StoreId);
        retainedOnly.UpperPosition.ShouldBe(7);
        retainedOnly.AvailabilityBaseline.ShouldBe(7);
        (await provider.FlushAsync()).Value.Pending.ShouldBe(0);
    }

    private static Task<ServiceProvider> CreateAsync(string backend, Func<DbConnection> connection) => backend switch
    {
        "SqlServer" => CreateAsync<SqlServerAuditMigrationContext>(options => { using var c = connection(); options.UseSqlServer(c.ConnectionString); }),
        "Postgres" => CreateAsync<PostgresAuditMigrationContext>(options => { using var c = connection(); options.UseNpgsql(c.ConnectionString); }),
        _ => CreateAsync<SqliteAuditMigrationContext>(options => { using var c = connection(); options.UseSqlite(c.ConnectionString); })
    };
    private static async Task<ServiceProvider> CreateAsync<TContext>(Action<DbContextOptionsBuilder> configure) where TContext : DbContext, IAuditingDbContext
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<TContext>(configure);
        services.AddAuditing().WithSearchableDetail(new() { Name = "count", Kind = "Decimal" }).WithHmacSha256<ExternalKeys>().WithEntityFrameworkProvider<TContext>(options => options.Inline());
        var host = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using (var scope = host.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<TContext>().Database.MigrateAsync();
        }

        var ready = await host.GetRequiredService<EntityFrameworkAuditStorageProvider<TContext>>().CheckReadinessAsync();
        ready.IsSuccess.ShouldBeTrue(string.Join(";", ready.Errors.Select(error => error.ToString())));
        return host;
    }
    public sealed class ExternalKeys : IAuditIntegrityKeyProvider
    {
        public Task<Result<AuditIntegrityKey>> ResolveActiveAsync(CancellationToken cancellationToken = default) => this.ResolveAsync("test-key", cancellationToken);
        public Task<Result<AuditIntegrityKey>> ResolveAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(Result<AuditIntegrityKey>.Success(new(id, Encoding.UTF8.GetBytes("fictional-test-key-material-32-bytes"))));
    }
}
