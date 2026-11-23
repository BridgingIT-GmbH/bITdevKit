// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB;

using BridgingIT.DevKit.Application.Entities;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using BridgingIT.DevKit.Infrastructure.LiteDb.Repositories;
using Microsoft.Extensions.DependencyInjection;

[IntegrationTest("Infrastructure")]
public class LiteDbChangeHistoryTests
{
    [Fact]
    public async Task Capture_QueryFiltersAndGroupedPaging_PreserveAllRowsAndHideValuesWhenRequested()
    {
        await using var host = CreateHost();
        var first = await host.Repository.InsertAsync(new HistoryAccount { Name = "before", Balance = 10, SecretToken = "private-value" });
        first.Name = "after";
        first.Balance = 20;
        await host.Repository.UpdateAsync(first);
        var sut = Service(host);
        var result = await sut.FindAllAsync(new() { EntityId = first.Id.ToString(), Operation = "Update", IncludeValues = false, PageSize = 1 });
        result.IsSuccess.ShouldBeTrue(Errors(result));
        result.Value.TotalCount.ShouldBe(2);
        result.Value.Values.Single().OldValue.ShouldBeNull();
        result.Value.Values.Single().NewValue.ShouldBeNull();
        var grouped = await sut.FindAllChangeSetsAsync(new() { EntityId = first.Id.ToString(), Operation = "Update", PageSize = 1 });
        grouped.IsSuccess.ShouldBeTrue(Errors(grouped));
        grouped.Value.Values.Single().Rows.Count.ShouldBe(2);
        var one = await sut.FindOneChangeSetAsync(new() { ChangeSetId = grouped.Value.Values.Single().ChangeSetId, EntityId = first.Id.ToString() });
        one.Value.Rows.Select(r => r.PropertyName).Order().ShouldBe(["Balance", "Name"]);
        var secrets = await sut.FindAllAsync(new() { PropertyName = "SecretToken" });
        secrets.Value.Values.Single().NewValue.ShouldBeNull();
        secrets.Value.Values.Single().NewValueHash.ShouldNotBeNullOrEmpty();
        secrets.Value.Values.Single().IsRestoreable.ShouldBeFalse();
    }

    [Fact]
    public async Task Restore_ExpectedVersionAndAudit_AtomicallyRestoresAndRecordsProvenance()
    {
        await using var host = CreateHost();
        var entity = await host.Repository.InsertAsync(new HistoryAccount { Name = "before", Balance = 10 });
        entity.Name = "after";
        await host.Repository.UpdateAsync(entity);
        var sut = Service(host);
        var rows = await sut.FindAllAsync(new() { EntityId = entity.Id.ToString(), Operation = "Update" });
        var setId = rows.Value.Values.Single().ChangeSetId;
        var stale = await sut.RestoreAsync(new(entity.Id, setId, "undo", Guid.NewGuid()));
        stale.IsFailure.ShouldBeTrue();
        (await host.Repository.FindOneAsync(entity.Id)).Name.ShouldBe("after");
        var restored = await sut.RestoreAsync(new(entity.Id, setId, "undo", entity.ConcurrencyVersion));
        restored.IsSuccess.ShouldBeTrue(Errors(restored));
        (await host.Repository.FindOneAsync(entity.Id)).Name.ShouldBe("before");
        var audit = await sut.FindOneChangeSetAsync(new() { ChangeSetId = restored.Value.RestoredChangeSetId });
        audit.IsSuccess.ShouldBeTrue(Errors(audit));
        audit.Value.Operation.ShouldBe("Restore");
        audit.Value.Rows.Single().Reason.ShouldBe("undo");
        audit.Value.Rows.Single().Properties.ShouldContain(setId.ToString());
    }

    [Fact]
    public async Task Restore_DeniedPolicy_DoesNotRunDomainLogicOrChangeHistory()
    {
        var called = false;
        await using var host = new LiteDbFeatureTestHost(s =>
        {
            s.AddLiteDbChangeHistory(o =>
            {
                o.RestoreAuthorizationPolicy = "history.restore";
                o.Track<HistoryAccount>().CaptureChanges().AllowRestore(x => x.Name).UseDomainMethod((entity, value) =>
                {
                    called = true;
                    entity.Name = value;
                    return Result.Success();
                });
            });
        });
        var entity = await host.Repository.InsertAsync(new HistoryAccount { Name = "before" });
        entity.Name = "after";
        await host.Repository.UpdateAsync(entity);
        var sut = Service(host);
        var history = await sut.FindAllAsync(new() { Operation = "Update" });
        var result = await sut.RestoreAsync(new(entity.Id, history.Value.Values.Single().ChangeSetId));
        result.IsFailure.ShouldBeTrue();
        called.ShouldBeFalse();
        (await host.Repository.FindOneAsync(entity.Id)).Name.ShouldBe("after");
    }

    [Fact]
    public async Task Read_DeniedPolicy_ReturnsNoValues()
    {
        await using var host = CreateHost(o => o.ReadAuthorizationPolicy = "history.read");
        await host.Repository.InsertAsync(new HistoryAccount { Name = "protected" });
        var sut = Service(host);
        (await sut.FindAllAsync(new())).IsFailure.ShouldBeTrue();
        (await sut.FindAllChangeSetsAsync(new())).IsFailure.ShouldBeTrue();
        (await sut.FindOneChangeSetAsync(new() { ChangeSetId = Guid.NewGuid() })).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task Capture_FailedRequiredPolicy_RollsBackEntityOutboxAndHistory()
    {
        await using var host = new LiteDbFeatureTestHost(s =>
        {
            s.AddLiteDbDomainEventOutbox();
            s.AddLiteDbChangeHistory(o =>
            {
                o.MaxStoredValueLength = 8;
                o.OversizedValuePolicy = ChangeHistoryOversizedValuePolicy.Reject;
                o.Track<HistoryAccount>().CaptureChanges();
            });
        });
        var entity = new HistoryAccount { Name = "too-long-to-store" };
        entity.DomainEvents.Register(new AccountRenamed { Name = entity.Name });
        await Should.ThrowAsync<LiteDbProviderException>(() => host.Repository.InsertAsync(entity));
        (await host.Repository.CountAsync()).ShouldBe(0);
        (await host.NativeAsync(db => db.GetCollection<global::LiteDB.BsonDocument>("change_history").Count())).ShouldBe(0);
        (await host.NativeAsync(db => db.GetCollection<LiteDbOutboxDomainEvent>("domain_event_outbox").Count())).ShouldBe(0);
        entity.DomainEvents.GetAll().Count().ShouldBe(1);
    }

    [Fact]
    public async Task Transaction_Rollback_LeavesNoHistoryAndRetainsPendingChangeSet()
    {
        await using var host = CreateHost();
        var entity = await host.Repository.InsertAsync(new HistoryAccount { Name = "before" });
        entity.Change().Set(x => x.Name, "after").Apply().IsSuccess.ShouldBeTrue();
        var transaction = host.Scope.ServiceProvider.GetRequiredService<IRepositoryTransaction<HistoryAccount>>();
        await Should.ThrowAsync<InvalidOperationException>(async () => await transaction.ExecuteScopedAsync(async () =>
        {
            await host.Repository.UpdateAsync(entity);
            throw new InvalidOperationException("rollback");
        }));
        EntityChangeHistoryAccessor.GetPendingChangeSets(entity).Count.ShouldBe(1);
        (await Service(host).FindAllAsync(new() { Operation = "Update" })).Value.TotalCount.ShouldBe(0);
        await host.Repository.UpdateAsync(entity);
        EntityChangeHistoryAccessor.GetPendingChangeSets(entity).ShouldBeEmpty();
        (await Service(host).FindAllAsync(new() { Operation = "Update" })).Value.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task UpdateSet_RequiredBound_RollsBackEntireSelection()
    {
        await using var host = CreateHost(o => o.GetEntityOptions(typeof(HistoryAccount)).UpdateSetMaxAffectedRows = 1);
        await host.Repository.InsertSetAsync([new HistoryAccount { Name = "first" }, new HistoryAccount { Name = "second" }]);
        await Should.ThrowAsync<LiteDbProviderException>(() => host.Repository.UpdateSetAsync(s => s.Set(x => x.Name, "changed")));
        (await host.Repository.FindAllAsync()).Select(e => e.Name).Order().ShouldBe(["first", "second"]);
        (await Service(host).FindAllAsync(new() { CaptureSource = "UpdateSet" })).Value.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task BulkInsertion_ExplicitHistorySummary_DoesNotCaptureValuesOrEnableOtherBehaviors()
    {
        await using var host = CreateHost(o => o.GetEntityOptions(typeof(HistoryAccount)).BulkInsertCaptureMode = ChangeHistoryBulkInsertCaptureMode.Summary);
        var sut = host.Scope.ServiceProvider.GetRequiredService<IEntityBulkInserter<HistoryAccount>>();
        var result = await sut.InsertAsync([new HistoryAccount { Name = "first" }, new HistoryAccount { Name = "second" }]);
        result.IsSuccess.ShouldBeTrue(Errors(result));
        var history = await Service(host).FindAllAsync(new() { CaptureSource = "NativeBulkInsert" });
        history.Value.Values.Single().CaptureStatus.ShouldBe("Summary");
        history.Value.Values.Single().AffectedEntityCount.ShouldBe(2);
        history.Value.Values.Single().IsRestoreable.ShouldBeFalse();
    }

    [Fact]
    public async Task Capture_OwnedAndIdentifiedCollection_ProtectsNestedSecretsAndUsesStablePaths()
    {
        await using var host = CreateHost(o => o.Track<HistoryAccount>()
            .CaptureOwned(x => x.Address).CaptureCollection(x => x.Contacts, x => x.Id));
        var entity = await host.Repository.InsertAsync(new HistoryAccount
        {
            Name = "test", Address = new HistoryAddress { City = "Old" },
            Contacts = [new HistoryContact { Id = "contact-1", Name = "Ada", SecretToken = "nested-private-value" }]
        });
        entity.Address.City = "New";
        entity.Contacts[0].Name = "Grace";
        await host.Repository.UpdateAsync(entity);
        var history = await Service(host).FindAllAsync(new() { Operation = "Update" });
        history.Value.Values.Select(r => r.PropertyPath).Order().ShouldBe(["Address.City", "Contacts[contact-1].Name"]);
        var secret = await Service(host).FindAllAsync(new() { PropertyName = "Contacts[contact-1].SecretToken" });
        secret.Value.Values.Single().NewValue.ShouldBeNull();
        secret.Value.Values.Single().NewValueHash.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Restore_DomainMethodFailure_DiscardsAllDetachedChanges()
    {
        await using var host = new LiteDbFeatureTestHost(s => s.AddLiteDbChangeHistory(o => o.Track<HistoryAccount>().CaptureChanges()
            .AllowRestore(x => x.Name).UseDomainMethod((entity, value) =>
            {
                entity.Name = value;
                return Result.Failure(new ValidationError("The domain rejected this restore."));
            })));
        var entity = await host.Repository.InsertAsync(new HistoryAccount { Name = "before" });
        entity.Name = "after";
        await host.Repository.UpdateAsync(entity);
        var sut = Service(host);
        var selected = (await sut.FindAllAsync(new() { Operation = "Update" })).Value.Values.Single();
        var result = await sut.RestoreAsync(new(entity.Id, selected.ChangeSetId));
        result.IsFailure.ShouldBeTrue();
        (await host.Repository.FindOneAsync(entity.Id)).Name.ShouldBe("after");
        (await sut.FindAllAsync(new() { Operation = "Restore" })).Value.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Restore_PointInTime_UndoesSelectedAndLaterChanges()
    {
        await using var host = CreateHost();
        var entity = await host.Repository.InsertAsync(new HistoryAccount { Name = "before", Balance = 10 });
        entity.Name = "middle";
        await host.Repository.UpdateAsync(entity);
        var sut = Service(host);
        var selected = (await sut.FindAllAsync(new() { Operation = "Update" })).Value.Values.Single();
        entity.Name = "latest";
        entity.Balance = 30;
        await host.Repository.UpdateAsync(entity);
        var result = await sut.RestoreAsync(new(entity.Id, selected.ChangeSetId, RestoreMode: ChangeHistoryRestoreMode.PointInTime));
        result.IsSuccess.ShouldBeTrue(Errors(result));
        var restored = await host.Repository.FindOneAsync(entity.Id);
        restored.Name.ShouldBe("before");
        restored.Balance.ShouldBe(10);
    }

    [Fact]
    public async Task Restore_ConfiguredOwnedPlan_UsesDomainContractAndRecordsOwnedChanges()
    {
        await using var host = new LiteDbFeatureTestHost(s =>
        {
            s.AddScoped<AddressRestorePlan>();
            s.AddLiteDbChangeHistory(o =>
            {
                o.Track<HistoryAccount>().CaptureChanges().CaptureOwned(x => x.Address);
                var path = o.GetEntityOptions(typeof(HistoryAccount)).CapturePaths.Single();
                path.RestorePlanType = typeof(AddressRestorePlan);
                path.RestorePlanName = "address";
            });
        });
        var entity = await host.Repository.InsertAsync(new HistoryAccount { Address = new HistoryAddress { City = "Old" } });
        entity.Address.City = "New";
        await host.Repository.UpdateAsync(entity);
        var sut = Service(host);
        var selected = (await sut.FindAllAsync(new() { Operation = "Update" })).Value.Values.Single();
        selected.IsRestoreable.ShouldBeTrue();
        var result = await sut.RestoreAsync(new(entity.Id, selected.ChangeSetId, "move back"));
        result.IsSuccess.ShouldBeTrue(Errors(result));
        (await host.Repository.FindOneAsync(entity.Id)).Address.City.ShouldBe("Old");
        (await sut.FindOneChangeSetAsync(new() { ChangeSetId = result.Value.RestoredChangeSetId })).Value.Rows.Single().PathKind.ShouldBe("Owned");
    }

    [Fact]
    public async Task Query_BoundedGrouping_ReturnsFailureWithoutTruncation()
    {
        await using var host = CreateHost(o => o.MaxMaterializedRows = 1);
        await host.Repository.InsertAsync(new HistoryAccount { Name = "first", Balance = 1 });
        var sut = Service(host);
        (await sut.FindAllAsync(new() { PageSize = 1 })).Value.TotalCount.ShouldBe(2);
        var grouped = await sut.FindAllChangeSetsAsync(new());
        grouped.IsFailure.ShouldBeTrue();
        grouped.Errors.Any(e => e is InvalidOperationError).ShouldBeTrue();
    }

    private static LiteDbFeatureTestHost CreateHost(Action<LiteDbChangeHistoryOptions> configure = null) => new(s => s.AddLiteDbChangeHistory(o =>
    {
        o.Track<HistoryAccount>().CaptureChanges(updateSetMode: ChangeHistoryCaptureMode.Required)
            .AllowRestoreUsingValidatedSetters(x => new { x.Name, x.Balance });
        configure?.Invoke(o);
    }));

    private static IChangeHistoryService<HistoryAccount, LiteDbContext> Service(LiteDbFeatureTestHost host) =>
        host.Scope.ServiceProvider.GetRequiredService<IChangeHistoryService<HistoryAccount, LiteDbContext>>();

    private static string Errors(IResult result) => string.Join("; ", result.Errors.Select(e => e.Message));
}

public sealed class AddressRestorePlan : IChangeHistoryGraphRestorePlan<HistoryAccount>
{
    public Task<Result> RestoreAsync(HistoryAccount entity, IReadOnlyList<ChangeHistoryGraphRestoreValue> values, CancellationToken cancellationToken = default)
    {
        entity.Address ??= new HistoryAddress();
        entity.Address.City = (string)values.Single(v => v.PropertyPath == "Address.City").Value;
        return Task.FromResult(Result.Success());
    }
}
