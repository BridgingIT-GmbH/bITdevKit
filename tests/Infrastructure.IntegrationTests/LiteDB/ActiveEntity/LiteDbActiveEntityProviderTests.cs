// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.ActiveEntity;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;

[Collection(nameof(LiteDbActiveEntityTestCollection))]
public class LiteDbActiveEntityProviderTests
{
    [Fact]
    public async Task Writes_CrudAndUpsert_ReturnExpectedActions()
    {
        await using var host = new LiteDbActiveEntityTestHost();
        var sut = host.Provider;
        var entity = new LedgerEntry { Name = "Entry", Amount = 4 };
        (await sut.InsertAsync(entity)).IsSuccess.ShouldBeTrue();
        entity.Id.ShouldNotBe(Guid.Empty);
        entity.ConcurrencyVersion.ShouldNotBe(Guid.Empty);
        (await sut.InsertAsync(entity)).HasError<ConflictError>().ShouldBeTrue();
        entity.Amount = 6;
        (await sut.UpdateAsync(entity)).IsSuccess.ShouldBeTrue();
        (await sut.FindOneAsync(entity.Id)).Value.Amount.ShouldBe(6);
        (await sut.UpsertAsync(entity)).Value.action.ShouldBe(RepositoryActionResult.Updated);
        var another = new LedgerEntry { Id = Guid.NewGuid(), Name = "Other" };
        (await sut.UpsertAsync(another)).Value.action.ShouldBe(RepositoryActionResult.Inserted);
        (await sut.DeleteAsync(entity)).IsSuccess.ShouldBeTrue();
        (await sut.FindOneAsync(entity.Id)).HasError<NotFoundError>().ShouldBeTrue();
        (await sut.CountAsync()).Value.ShouldBe(1);
    }

    [Fact]
    public async Task UpdateAsync_MissingEntity_DoesNotInsert()
    {
        await using var host = new LiteDbActiveEntityTestHost();
        (await host.Provider.UpdateAsync(new LedgerEntry { Id = Guid.NewGuid() })).HasError<NotFoundError>().ShouldBeTrue();
        (await host.Provider.CountAsync()).Value.ShouldBe(0);
    }

    [Fact]
    public async Task WriteAsync_NullEntities_ReturnFailures()
    {
        await using var host = new LiteDbActiveEntityTestHost();
        var sut = host.Provider;
        (await sut.InsertAsync(null)).HasError<InvalidInputError>().ShouldBeTrue();
        (await sut.UpdateAsync(null)).IsFailure.ShouldBeTrue();
        (await sut.UpsertAsync(null)).IsFailure.ShouldBeTrue();
        (await sut.DeleteAsync(null)).IsFailure.ShouldBeTrue();
        (await sut.CountAsync()).Value.ShouldBe(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid-guid")]
    public async Task FindOneAsync_InvalidIdentity_ReturnsFailure(string id)
    {
        await using var host = new LiteDbActiveEntityTestHost();
        (await host.Provider.FindOneAsync((object)id)).IsFailure.ShouldBeTrue();
        (await host.Provider.ExistsAsync((object)id)).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateAndDelete_StaleVersion_PreserveStoredRecordAndCallerVersion()
    {
        await using var host = new LiteDbActiveEntityTestHost();
        var sut = host.Provider;
        var entity = (await sut.InsertAsync(new LedgerEntry { Name = "Initial" })).Value;
        var stale = (await sut.FindOneAsync(entity.Id)).Value;
        var originalVersion = stale.ConcurrencyVersion;
        entity.Name = "Current";
        (await sut.UpdateAsync(entity)).IsSuccess.ShouldBeTrue();
        stale.Name = "Stale";
        (await sut.UpdateAsync(stale)).HasError<ConcurrencyError>().ShouldBeTrue();
        stale.ConcurrencyVersion.ShouldBe(originalVersion);
        (await sut.DeleteAsync(stale)).HasError<ConcurrencyError>().ShouldBeTrue();
        (await sut.FindOneAsync(entity.Id)).Value.Name.ShouldBe("Current");
    }

    [Fact]
    public async Task UpdateAsync_ConcurrencyDisabled_AllowsStaleVersion()
    {
        await using var host = new LiteDbActiveEntityTestHost(options => options.EnableOptimisticConcurrency = false);
        var sut = host.Provider;
        var entity = (await sut.InsertAsync(new LedgerEntry())).Value;
        var stale = (await sut.FindOneAsync(entity.Id)).Value;
        (await sut.UpdateAsync(entity)).IsSuccess.ShouldBeTrue();
        (await sut.UpdateAsync(stale)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task InsertAsync_AfterCallbackFailure_RollsBackAndRestoresVersion()
    {
        await using var host = new LiteDbActiveEntityTestHost();
        var entity = new LedgerEntry();
        var result = await host.Provider.InsertAsync(entity, new ActiveEntityCallbackOptions<LedgerEntry, Guid>
        {
            AfterInsertAsync = (_, _) => Task.FromResult(Result.Failure(new ValidationError("Rejected")))
        });
        result.HasError<ValidationError>().ShouldBeTrue();
        entity.ConcurrencyVersion.ShouldBe(Guid.Empty);
        (await host.Provider.CountAsync()).Value.ShouldBe(0);
    }

    [Fact]
    public async Task InsertAsync_CallbackOrderAndAwaitedReentry_RunOnceWithoutDeadlock()
    {
        await using var host = new LiteDbActiveEntityTestHost();
        var stages = new List<string>();
        var result = await host.Provider.InsertAsync(new LedgerEntry(), new ActiveEntityCallbackOptions<LedgerEntry, Guid>
        {
            BeforeUpsertAsync = async (provider, _) =>
            {
                stages.Add("before-upsert");
                await Task.Yield();
                (await provider.CountAsync()).Value.ShouldBe(0);
                return Result.Success();
            },
            BeforeInsertAsync = (_, _) => { stages.Add("before-insert"); return Task.FromResult(Result.Success()); },
            AfterInsertAsync = async (provider, _) =>
            {
                stages.Add("after-insert");
                (await provider.CountAsync()).Value.ShouldBe(1);
                return Result.Success();
            },
            AfterUpsertAsync = (_, _) => { stages.Add("after-upsert"); return Task.FromResult(Result.Success()); }
        }).WaitAsync(TimeSpan.FromSeconds(10));
        result.IsSuccess.ShouldBeTrue();
        stages.ShouldBe(["before-upsert", "before-insert", "after-insert", "after-upsert"]);
    }

    [Fact]
    public async Task Callbacks_FailedResultWithOnlyMessage_PreservesMessageAndRollsBack()
    {
        await using var host = new LiteDbActiveEntityTestHost();
        var result = await host.Provider.InsertAsync(new LedgerEntry(), new ActiveEntityCallbackOptions<LedgerEntry, Guid>
        {
            AfterInsertAsync = (_, _) => Task.FromResult(Result.Failure("The fictional policy rejected this entry."))
        });
        result.IsFailure.ShouldBeTrue();
        result.Messages.ShouldContain("The fictional policy rejected this entry.");
        (await host.Provider.CountAsync()).Value.ShouldBe(0);
    }

    [Fact]
    public async Task InsertAsync_CustomIdentifierAndVersionGenerators_AreHonored()
    {
        var id = Guid.NewGuid();
        var version = Guid.NewGuid();
        await using var host = new LiteDbActiveEntityTestHost(options =>
        {
            options.IdGenerator = new FixedIdentifier(id);
            options.VersionGenerator = () => version;
        });
        var entity = new LedgerEntry();
        (await host.Provider.InsertAsync(entity)).IsSuccess.ShouldBeTrue();
        entity.Id.ShouldBe(id);
        entity.ConcurrencyVersion.ShouldBe(version);
        (await host.Provider.InsertAsync(new LedgerEntry())).HasError<ConflictError>().ShouldBeTrue();
    }

    [Fact]
    public async Task NestedCallback_FailedReadIgnoredByCaller_StillPreventsCommit()
    {
        await using var host = new LiteDbActiveEntityTestHost();
        var result = await host.Provider.InsertAsync(new LedgerEntry(), new ActiveEntityCallbackOptions<LedgerEntry, Guid>
        {
            AfterInsertAsync = async (provider, _) =>
            {
                (await provider.FindOneAsync("invalid-guid")).IsFailure.ShouldBeTrue();
                return Result.Success();
            }
        });
        result.IsFailure.ShouldBeTrue();
        (await host.Provider.CountAsync()).Value.ShouldBe(0);
    }

    private sealed class FixedIdentifier(Guid id) : IEntityIdGenerator<LedgerEntry>
    {
        public bool IsNew(object value) => value is null || value is Guid guid && guid == Guid.Empty;

        public void SetNew(LedgerEntry entity) => entity.Id = id;
    }

    [Fact]
    public async Task Identifiers_PrimitiveAndGeneratedTypedIds_RoundTrip()
    {
        await using var host = new LiteDbActiveEntityTestHost();
        await VerifyIdentityAsync(host.Get<LedgerItem<Guid>, Guid>(), new LedgerItem<Guid>());
        await VerifyIdentityAsync(host.Get<LedgerItem<string>, string>(), new LedgerItem<string>());
        await VerifyIdentityAsync(host.Get<LedgerItem<int>, int>(), new LedgerItem<int>());
        await VerifyIdentityAsync(host.Get<LedgerItem<long>, long>(), new LedgerItem<long>());
        await VerifyIdentityAsync(host.Get<TypedLedgerEntry, TypedLedgerEntryId>(), new TypedLedgerEntry());
        await VerifyIdentityAsync(host.Get<IntegerLedgerEntry, IntegerLedgerEntryId>(), new IntegerLedgerEntry());
        await VerifyIdentityAsync(host.Get<LongLedgerEntry, LongLedgerEntryId>(), new LongLedgerEntry());
        await VerifyIdentityAsync(host.Get<StringLedgerEntry, StringLedgerEntryId>(), new StringLedgerEntry());
    }

    private static async Task VerifyIdentityAsync<TEntity, TId>(IActiveEntityEntityProvider<TEntity, TId> sut, TEntity entity)
        where TEntity : class, IEntity
    {
        var result = await sut.InsertAsync(entity);
        result.IsSuccess.ShouldBeTrue(string.Join(",", result.Errors.Select(e => e.Message)));
        (await sut.FindOneAsync(entity.Id)).Value.Id.ShouldBe(entity.Id);
        (await sut.FindOneAsync(entity.Id.ToString())).Value.Id.ShouldBe(entity.Id);
        (await sut.FindAllIdsAsync()).Value.ShouldContain((TId)entity.Id);
        (await sut.FindAllIdsPagedAsync()).Value.ShouldContain((TId)entity.Id);
    }
}
