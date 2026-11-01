// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Auditing;

using System.Data.Common;
using System.Text;
using Application.Auditing;
using Application.Entities;
using Domain.Repositories;
using Domain.Model;
using Infrastructure.EntityFramework;
using Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Auditing.Fixtures;
using BridgingIT.DevKit.Application.IntegrationTests.Auditing.Fixtures;

[Collection(nameof(AuditRelationalCollection))]
public sealed class ChangeHistoryAuditBridgeTests(AuditRelationalFixture fixture)
{
    [Theory]
    [InlineData("SqlServer")]
    [InlineData("Postgres")]
    [InlineData("Sqlite")]
    public async Task RepositoryHistory_ConfirmedSnapshotSurvivesSourceDeletion(string backend)
    {
        await using var host = await this.CreateAsync(backend);
        await using var services = host.CreateAsyncScope();
        var source = services.ServiceProvider.GetRequiredService<SourceHistoryMigrationContext>();
        var customer = new HistoryCustomer { Id = Guid.NewGuid(), Name = "before", Code = "customer-1" };
        source.Customers.Add(customer);
        await source.SaveChangesAsync();
        source.ChangeTracker.Clear();
        customer.Name = "after";
        var audit = services.ServiceProvider.GetRequiredService<IAuditContext>();
        var root = audit.BeginScope(builder => builder.Key("customers.rename"));
        var history = new RepositoryChangeHistoryBehavior<HistoryCustomer, SourceHistoryMigrationContext>(NullLoggerFactory.Instance, source,
            new EntityFrameworkRepositoryWrapper<HistoryCustomer, SourceHistoryMigrationContext>(NullLoggerFactory.Instance, source),
            services.ServiceProvider.GetRequiredService<ChangeHistoryOptions>());
        var sut = new RepositoryAuditingBehavior<HistoryCustomer>(history, audit);
        (await sut.UpdateAsync(customer)).ShouldBeSameAs(customer);
        root.Complete();
        await root.DisposeAsync();
        var stored = await Read(host, root.AuditId);
        var group = stored.EntityChanges.ShouldHaveSingleItem();
        group.PersistenceState.ShouldBe("Committed");
        group.SourceStoreKey.ShouldBe("customers");
        group.ActionId.ShouldBe(stored.Actions.ShouldHaveSingleItem().Id);
        var property = group.Properties.Single(item => item.Path == "Name");
        property.OldValue.Data.GetString().ShouldBe("before");
        property.NewValue.Data.GetString().ShouldBe("after");
        group.SourceRowIds.ShouldNotBeEmpty();
        await source.History.ExecuteDeleteAsync();
        var retained = await Read(host, root.AuditId);
        retained.EntityChanges.ShouldBe(stored.EntityChanges);
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("Postgres")]
    [InlineData("Sqlite")]
    public async Task Savepoints_OnlySurvivingRowsBecomeCommitted(string backend)
    {
        await using var host = await this.CreateAsync(backend);
        await using var services = host.CreateAsyncScope();
        var source = services.ServiceProvider.GetRequiredService<SourceHistoryMigrationContext>();
        var root = services.ServiceProvider.GetRequiredService<IAuditContext>().BeginScope(builder => builder.Key("customers.savepoint"));
        await using var transaction = await source.Database.BeginTransactionAsync();
        var retained = Row("Name", "before", "kept");
        source.History.Add(retained);
        await source.SaveChangesAsync();
        await transaction.CreateSavepointAsync("customer_saved");
        source.History.Add(Row("Name", "kept", "rolled-back-private-value"));
        await source.SaveChangesAsync();
        await transaction.RollbackToSavepointAsync("customer_saved");
        await transaction.CommitAsync();
        root.Complete();
        await root.DisposeAsync();
        var stored = await Read(host, root.AuditId);
        var group = stored.EntityChanges.ShouldHaveSingleItem();
        group.PersistenceState.ShouldBe("Committed");
        group.SourceRowIds.ShouldBe([retained.Id.ToString("D")]);
        group.Properties.Single().NewValue.Data.GetString().ShouldBe("kept");
        Encoding.UTF8.GetString(AuditRecordCodec.Encode(stored)).ShouldNotContain("rolled-back-private-value");
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("Postgres")]
    [InlineData("Sqlite")]
    public async Task RollbackAndRetry_DiscardFirstAttemptAndKeepDistinctCommittedOccurrence(string backend)
    {
        await using var host = await this.CreateAsync(backend);
        await using var services = host.CreateAsyncScope();
        var source = services.ServiceProvider.GetRequiredService<SourceHistoryMigrationContext>();
        var root = services.ServiceProvider.GetRequiredService<IAuditContext>().BeginScope(builder => builder.Key("customers.retry"));
        await using (var failed = await source.Database.BeginTransactionAsync())
        {
            source.History.Add(Row("Name", null, "discarded-private-value"));
            await source.SaveChangesAsync();
            await failed.RollbackAsync();
        }

        source.ChangeTracker.Clear();
        await using (var successful = await source.Database.BeginTransactionAsync())
        {
            source.History.Add(Row("Name", null, "retained"));
            await source.SaveChangesAsync();
            await successful.CommitAsync();
        }

        root.Complete();
        await root.DisposeAsync();
        var stored = await Read(host, root.AuditId);
        stored.EntityChanges.ShouldHaveSingleItem().Properties.Single().NewValue.Data.GetString().ShouldBe("retained");
        Encoding.UTF8.GetString(AuditRecordCodec.Encode(stored)).ShouldNotContain("discarded-private-value");
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("Postgres")]
    [InlineData("Sqlite")]
    public async Task RootClosesBeforeTransaction_UnknownReferencesNeverRevealValuesOrMutateLater(string backend)
    {
        await using var host = await this.CreateAsync(backend);
        await using var services = host.CreateAsyncScope();
        var source = services.ServiceProvider.GetRequiredService<SourceHistoryMigrationContext>();
        var root = services.ServiceProvider.GetRequiredService<IAuditContext>().BeginScope(builder => builder.Key("customers.unresolved"));
        await using var transaction = await source.Database.BeginTransactionAsync();
        var row = Row("Name", "private-before", "private-after");
        source.History.Add(row);
        await source.SaveChangesAsync();
        root.Complete();
        await root.DisposeAsync();
        var stored = await Read(host, root.AuditId);
        var group = stored.EntityChanges.ShouldHaveSingleItem();
        group.PersistenceState.ShouldBe("Unknown");
        group.SourceRowIds.ShouldBe([row.Id.ToString("D")]);
        group.Properties.Single().OldValue.ShouldBeNull();
        group.Properties.Single().NewValue.ShouldBeNull();
        stored.Notes.ShouldContain(note => note.Code == "AuditSourceTransactionUnknown");
        await transaction.CommitAsync();
        AuditRecordCodec.Encode(await Read(host, root.AuditId)).ShouldBe(AuditRecordCodec.Encode(stored));
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("Postgres")]
    [InlineData("Sqlite")]
    public async Task Autocommit_SourcePoliciesAndAuditExclusionsArePreserved(string backend)
    {
        await using var host = await this.CreateAsync(backend);
        await using var services = host.CreateAsyncScope();
        var source = services.ServiceProvider.GetRequiredService<SourceHistoryMigrationContext>();
        var root = services.ServiceProvider.GetRequiredService<IAuditContext>().BeginScope(builder => builder.Key("customers.policy"));
        source.History.AddRange(Row("Email", null, "private@example.test"), Row("Name", null, "***REDACTED***"), withHash());
        await source.SaveChangesAsync();
        root.Complete();
        await root.DisposeAsync();
        var properties = (await Read(host, root.AuditId)).EntityChanges.SelectMany(group => group.Properties).ToArray();
        properties.Single(item => item.Path == "Email").NewValue.Marker.ShouldBe("Excluded");
        properties.Single(item => item.Path == "Name").NewValue.Marker.ShouldBe("Redacted");
        properties.Single(item => item.Path == "ExternalId").NewValue.Marker.ShouldBe("Hashed");
        (await source.History.CountAsync()).ShouldBe(3);

        ChangeHistoryEntry withHash()
        {
            var row = Row("ExternalId", null, null);
            row.NewValueHash = new string('a', 64);
            return row;
        }
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("Postgres")]
    [InlineData("Sqlite")]
    public async Task ReferenceOnlyAndOmittedBridge_DoNotEnableOrChangeSourceCapture(string backend)
    {
        foreach (var selected in new[] { false, true })
        {
            await using var host = await this.CreateAsync(backend, selected, AuditChangeHistoryMode.ReferenceOnly);
            await using var services = host.CreateAsyncScope();
            var source = services.ServiceProvider.GetRequiredService<SourceHistoryMigrationContext>();
            var root = services.ServiceProvider.GetRequiredService<IAuditContext>().BeginScope(builder => builder.Key("customers.references"));
            source.History.Add(Row("Name", "before", "after"));
            // Also prove the synchronous interception path without blocking on asynchronous work.
            source.SaveChanges();
            root.Complete();
            await root.DisposeAsync();
            var stored = await Read(host, root.AuditId);
            (await source.History.CountAsync()).ShouldBe(1);
            if (!selected) stored.EntityChanges.ShouldBeEmpty();
            else
            {
                var group = stored.EntityChanges.ShouldHaveSingleItem();
                group.PersistenceState.ShouldBe("Committed");
                group.Properties.Single().OldValue.ShouldBeNull();
                group.Properties.Single().NewValue.ShouldBeNull();
            }
        }
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("Postgres")]
    [InlineData("Sqlite")]
    public async Task FailedSave_FollowedByCommitCannotPromoteRejectedValuesForExistingRowId(string backend)
    {
        await using var host = await this.CreateAsync(backend);
        await using var services = host.CreateAsyncScope();
        var source = services.ServiceProvider.GetRequiredService<SourceHistoryMigrationContext>();
        var existing = Row("Name", null, "original");
        source.History.Add(existing);
        await source.SaveChangesAsync();
        source.ChangeTracker.Clear();
        var root = services.ServiceProvider.GetRequiredService<IAuditContext>().BeginScope(builder => builder.Key("customers.failed-save"));
        await using var transaction = await source.Database.BeginTransactionAsync();
        var duplicate = Row("Name", null, "rejected-private-value");
        duplicate.Id = existing.Id;
        source.History.Add(duplicate);
        await Should.ThrowAsync<DbUpdateException>(() => source.SaveChangesAsync());
        source.ChangeTracker.Clear();
        source.History.Add(Row("Name", null, "retained"));
        await source.SaveChangesAsync();
        await transaction.CommitAsync();
        root.Complete();
        await root.DisposeAsync();
        var stored = await Read(host, root.AuditId);
        stored.EntityChanges.ShouldHaveSingleItem().Properties.Single().NewValue.Data.GetString().ShouldBe("retained");
        Encoding.UTF8.GetString(AuditRecordCodec.Encode(stored)).ShouldNotContain("rejected-private-value");
        (await source.History.CountAsync()).ShouldBe(2);
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("Postgres")]
    [InlineData("Sqlite")]
    public async Task NativeBulk_HonorsProviderSupportAndDoesNotInventPropertyDeltas(string backend)
    {
        await using var host = await this.CreateAsync(backend);
        await using var services = host.CreateAsyncScope();
        var source = services.ServiceProvider.GetRequiredService<SourceHistoryMigrationContext>();
        var root = services.ServiceProvider.GetRequiredService<IAuditContext>().BeginScope(builder => builder.Key("customers.bulk"));
        var input = new[] { new HistoryCustomer { Id = Guid.NewGuid(), Name = "first private", Code = "first" }, new HistoryCustomer { Id = Guid.NewGuid(), Name = "second private", Code = "second" } };
        var inserted = await services.ServiceProvider.GetRequiredService<IEntityBulkInserter<HistoryCustomer>>().InsertAsync(input);
        if (backend != "SqlServer")
        {
            // Existing PostgreSQL and SQLite native bulk providers are explicit placeholders.
            inserted.IsFailure.ShouldBeTrue();
            root.Complete(AuditOutcome.Rejected);
            await root.DisposeAsync();
            (await source.Customers.CountAsync()).ShouldBe(0);
            var rejected = await Read(host, root.AuditId);
            rejected.EntityChanges.ShouldBeEmpty();
            rejected.Actions.Single().Outcome.ShouldBe(AuditOutcome.Rejected);
            return;
        }

        inserted.IsSuccess.ShouldBeTrue(string.Join(";", inserted.Errors));
        inserted.Value.ShouldBe(2);
        root.Complete();
        await root.DisposeAsync();
        (await source.Customers.CountAsync()).ShouldBe(2);
        var stored = await Read(host, root.AuditId);
        var group = stored.EntityChanges.ShouldHaveSingleItem();
        group.PersistenceState.ShouldBe("Committed");
        group.CaptureStatus.ShouldBe("Summary");
        group.CaptureSource.ShouldBe("NativeBulkInsert");
        group.AffectedEntityCount.ShouldBe(2);
        group.BulkOperationId.ShouldNotBeNull();
        group.Properties.ShouldBeEmpty();
        group.ActionId.ShouldBe(stored.Actions.ShouldHaveSingleItem().Id);
        Encoding.UTF8.GetString(AuditRecordCodec.Encode(stored)).ShouldNotContain("private");
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("Postgres")]
    [InlineData("Sqlite")]
    public async Task LaterFailedSaveWithoutHistory_DoesNotDiscardEarlierSuccessfulSaveInSameTransaction(string backend)
    {
        await using var host = await this.CreateAsync(backend);
        await using var services = host.CreateAsyncScope();
        var source = services.ServiceProvider.GetRequiredService<SourceHistoryMigrationContext>();
        source.Customers.Add(new() { Id = Guid.NewGuid(), Code = "same-code" });
        await source.SaveChangesAsync();
        var root = services.ServiceProvider.GetRequiredService<IAuditContext>().BeginScope(builder => builder.Key("customers.previous-save"));
        await using var transaction = await source.Database.BeginTransactionAsync();
        source.History.Add(Row("Name", null, "retained"));
        await source.SaveChangesAsync();
        source.Customers.Add(new() { Id = Guid.NewGuid(), Code = "same-code" });
        await Should.ThrowAsync<DbUpdateException>(() => source.SaveChangesAsync());
        source.ChangeTracker.Clear();
        await transaction.CommitAsync();
        root.Complete();
        await root.DisposeAsync();
        (await Read(host, root.AuditId)).EntityChanges.ShouldHaveSingleItem().Properties.Single().NewValue.Data.GetString().ShouldBe("retained");
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("Postgres")]
    [InlineData("Sqlite")]
    public async Task SourceSummaryAndAbsentAuditing_PreserveIndependentSourceBehavior(string backend)
    {
        await using var host = await this.CreateAsync(backend);
        await using var services = host.CreateAsyncScope();
        var source = services.ServiceProvider.GetRequiredService<SourceHistoryMigrationContext>();
        var root = services.ServiceProvider.GetRequiredService<IAuditContext>().BeginScope(builder => builder.Key("customers.summary"));
        var summary = Row("__ChangeHistoryBulkInsertSummary", null, "source-summary-metadata");
        summary.PathKind = "Summary";
        summary.CaptureStatus = "Summary";
        summary.AffectedEntityCount = 42;
        summary.BulkOperationId = Guid.NewGuid();
        source.History.Add(summary);
        await source.SaveChangesAsync();
        root.Complete();
        await root.DisposeAsync();
        var evidence = (await Read(host, root.AuditId)).EntityChanges.ShouldHaveSingleItem();
        evidence.Properties.ShouldBeEmpty();
        evidence.AffectedEntityCount.ShouldBe(42);
        evidence.CaptureStatus.ShouldBe("Summary");
        evidence.PersistenceState.ShouldBe("Committed");

        // A separate host can retain the selected bridge without registering Auditing.
        using var connection = (await fixture.CreateDatabaseAsync(backend))();
        await using var noAudit = backend switch
        {
            "SqlServer" => await CreateAsync<SqlServerSourceHistoryContext>(options => options.UseSqlServer(connection.ConnectionString), true, AuditChangeHistoryMode.Snapshot, false),
            "Postgres" => await CreateAsync<PostgresSourceHistoryContext>(options => options.UseNpgsql(connection.ConnectionString), true, AuditChangeHistoryMode.Snapshot, false),
            _ => await CreateAsync<SqliteSourceHistoryContext>(options => options.UseSqlite(connection.ConnectionString), true, AuditChangeHistoryMode.Snapshot, false)
        };
        await using var noAuditScope = noAudit.CreateAsyncScope();
        var independent = noAuditScope.ServiceProvider.GetRequiredService<SourceHistoryMigrationContext>();
        independent.History.Add(Row("Name", "before", "after"));
        await independent.SaveChangesAsync();
        (await independent.History.CountAsync()).ShouldBe(1);
        noAudit.GetService<AuditRuntime>().ShouldBeNull();
    }

    private async Task<ServiceProvider> CreateAsync(string backend, bool bridge = true, AuditChangeHistoryMode mode = AuditChangeHistoryMode.Snapshot)
    {
        using var connection = (await fixture.CreateDatabaseAsync(backend))();
        var text = connection.ConnectionString;
        return backend switch
        {
            "SqlServer" => await CreateAsync<SqlServerSourceHistoryContext>(options => options.UseSqlServer(text), bridge, mode),
            "Postgres" => await CreateAsync<PostgresSourceHistoryContext>(options => options.UseNpgsql(text), bridge, mode),
            _ => await CreateAsync<SqliteSourceHistoryContext>(options => options.UseSqlite(text), bridge, mode)
        };
    }
    private static async Task<ServiceProvider> CreateAsync<TContext>(Action<DbContextOptionsBuilder> configure, bool bridge, AuditChangeHistoryMode mode, bool auditing = true) where TContext : SourceHistoryMigrationContext
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (auditing) services.AddAuditing();
        services.AddDbContext<TContext>(configure);
        services.AddScoped<SourceHistoryMigrationContext>(provider => provider.GetRequiredService<TContext>());
        var history = services.AddChangeHistory(options => options.Track<HistoryCustomer>()
            .CaptureDirectMutations(ChangeHistoryCaptureStrategy.RepositorySnapshot, ChangeHistoryCaptureMode.Required).CaptureBulkInserts());
        services.AddSingleton<IEntityBulkInsertProvider, SqlServerEntityBulkInsertProvider>();
        services.AddSingleton<IEntityBulkInsertProvider, PostgresEntityBulkInsertProvider>();
        services.AddSingleton<IEntityBulkInsertProvider, SqliteEntityBulkInsertProvider>();
        services.AddEntityFrameworkBulkInserter<HistoryCustomer, TContext>()
            .WithBehavior<EntityBulkInserterChangeHistoryBehavior<HistoryCustomer, TContext>>().WithAuditing();
        if (bridge) history.WithAuditing<TContext>(options => options with { SourceStoreKey = "customers", Mode = mode });
        var host = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        await using var scope = host.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<TContext>().Database.MigrateAsync();
        return host;
    }
    private static async Task<AuditRecord> Read(ServiceProvider host, Guid id)
    {
        var found = await host.GetRequiredService<IAuditingStorageProvider>().FindAsync(id);
        found.IsSuccess.ShouldBeTrue();
        return found.Value;
    }
    private static ChangeHistoryEntry Row(string property, string before, string after)
    {
        var now = DateTimeOffset.UtcNow;
        return new()
        {
            Id = Guid.NewGuid(), ChangeSetId = Guid.NewGuid(), EntityType = nameof(HistoryCustomer), EntityClrType = typeof(HistoryCustomer).AssemblyQualifiedName,
            EntityId = Guid.NewGuid().ToString("D"), PropertyName = property, PropertyPath = property, PathKind = "Scalar",
            OldValue = before is null ? null : System.Text.Json.JsonSerializer.Serialize(before),
            NewValue = after is null ? null : System.Text.Json.JsonSerializer.Serialize(after),
            Operation = "Update", CaptureStrategy = "RepositorySnapshot", CaptureSource = "RepositorySnapshot", CaptureStatus = "Captured", ChangedDate = now, ChangedDateTicks = now.UtcTicks
        };
    }
}
