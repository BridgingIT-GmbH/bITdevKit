// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB;

using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using BridgingIT.DevKit.Infrastructure.LiteDb.Repositories;
using Microsoft.Extensions.DependencyInjection;

[IntegrationTest("Infrastructure")]
public class LiteDbRepositoryContractTests : IAsyncDisposable
{
    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("bdk-repository-");
    private readonly ServiceProvider services;
    private readonly AsyncServiceScope scope;

    public LiteDbRepositoryContractTests()
    {
        global::LiteDB.LiteDBPragmas.I_AM_AWARE_MY_DATABASE_BREAKS_WHEN_I_USE_THIS();
        var registrations = new ServiceCollection();
        registrations.AddLogging();
        registrations.AddLiteDbDatabase(o =>
        {
            o.ConnectionString = Path.Combine(this.directory.FullName, "records.db");
            o.ConfigureMapper = mapper =>
            {
                mapper.Entity<RepositoryRecord>().DbRef(x => x.Parent, "records");
                mapper.Entity<RepositoryRecord>().DbRef(x => x.Children, "records");
            };
        });
        registrations.AddLiteDbRepository<RepositoryRecord>(configure: o => o.CollectionName = "records");
        registrations.AddLiteDbEntityBulkInserter<RepositoryRecord>(configure: o => o.CollectionName = "records");
        this.services = registrations.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        this.scope = this.services.CreateAsyncScope();
    }

    [Fact]
    public async Task Transaction_AwaitAndSecondScope_CommitsOnlyOnCompletion()
    {
        var sut = this.scope.ServiceProvider.GetRequiredService<IGenericRepository<RepositoryRecord>>();
        var transaction = this.scope.ServiceProvider.GetRequiredService<IRepositoryTransaction<RepositoryRecord>>();
        await transaction.ExecuteScopedAsync(async () =>
        {
            await sut.InsertAsync(new RepositoryRecord { Name = "first" });
            await Task.Yield();
            await sut.InsertAsync(new RepositoryRecord { Name = "second" });
            (await sut.CountAsync()).ShouldBe(2);
        });
        await using var other = this.services.CreateAsyncScope();
        (await other.ServiceProvider.GetRequiredService<IGenericRepository<RepositoryRecord>>().CountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task Transaction_FailedDelegate_RollsBackAndRestoresVersion()
    {
        var sut = this.scope.ServiceProvider.GetRequiredService<IGenericRepository<RepositoryRecord>>();
        var entity = await sut.InsertAsync(new RepositoryRecord { Name = "before" });
        var version = entity.ConcurrencyVersion;
        var transaction = this.scope.ServiceProvider.GetRequiredService<IRepositoryTransaction<RepositoryRecord>>();
        await Should.ThrowAsync<InvalidOperationException>(async () => await transaction.ExecuteScopedAsync(async () =>
        {
            entity.Name = "after";
            await sut.UpdateAsync(entity);
            await Task.Yield();
            throw new InvalidOperationException("rollback");
        }));
        entity.ConcurrencyVersion.ShouldBe(version);
        (await sut.FindOneAsync(entity.Id)).Name.ShouldBe("before");
    }

    [Fact]
    public async Task UpdateAndDelete_StaleVersion_FailWithoutChangingStoredState()
    {
        var sut = this.scope.ServiceProvider.GetRequiredService<IGenericRepository<RepositoryRecord>>();
        var entity = await sut.InsertAsync(new RepositoryRecord { Name = "original" });
        var stale = await sut.FindOneAsync(entity.Id);
        entity.Name = "current";
        await sut.UpdateAsync(entity);
        stale.Name = "stale";
        await Should.ThrowAsync<LiteDbProviderException>(() => sut.UpdateAsync(stale));
        await Should.ThrowAsync<LiteDbProviderException>(() => sut.DeleteAsync(stale));
        (await sut.FindOneAsync(entity.Id)).Name.ShouldBe("current");
    }

    [Fact]
    public async Task SetOperations_FilterOrderAndPage_AffectOnlySelectedRows()
    {
        var sut = this.scope.ServiceProvider.GetRequiredService<IGenericRepository<RepositoryRecord>>();
        await sut.InsertSetAsync(Enumerable.Range(1, 5).Select(n => new RepositoryRecord { Name = "row", Amount = n }));
        var selected = new FindOptions<RepositoryRecord> { Order = new OrderOption<RepositoryRecord>(x => x.Amount), Skip = 1, Take = 2 };
        (await sut.UpdateSetAsync(new Specification<RepositoryRecord>(x => x.Amount >= 2), s => s.Set(x => x.Name, "changed"), selected)).ShouldBe(2);
        (await sut.FindAllAsync(new Specification<RepositoryRecord>(x => x.Name == "changed"))).Select(x => x.Amount).Order().ShouldBe([3, 4]);
        (await sut.DeleteSetAsync(new Specification<RepositoryRecord>(x => x.Name == "changed"))).ShouldBe(2);
        (await sut.DeleteSetAsync(new[] { new Specification<RepositoryRecord>(x => x.Amount == 1) })).ShouldBe(1);
        (await sut.UpdateSetAsync(s => s.Set(x => x.Name, "remaining"))).ShouldBe(2);
        (await sut.DeleteSetAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task BulkInsert_ConflictingBatch_IsAtomicAndPreservesExplicitVersions()
    {
        var sut = this.scope.ServiceProvider.GetRequiredService<IEntityBulkInserter<RepositoryRecord>>();
        var version = Guid.NewGuid();
        var entity = new RepositoryRecord { Id = Guid.NewGuid(), Name = "bulk", ConcurrencyVersion = version };
        (await sut.InsertAsync([entity])).Value.ShouldBe(1);
        entity.ConcurrencyVersion.ShouldBe(version);
        (await sut.InsertAsync([new RepositoryRecord { Name = "rolled back" }, entity])).IsFailure.ShouldBeTrue();
        var repository = this.scope.ServiceProvider.GetRequiredService<IGenericRepository<RepositoryRecord>>();
        (await repository.CountAsync()).ShouldBe(1);
        (await sut.InsertAsync([null])).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task Includes_ThenIncludeAndHierarchy_HydrateReferenceChainsAndCollections()
    {
        var sut = this.scope.ServiceProvider.GetRequiredService<IGenericRepository<RepositoryRecord>>();
        var root = await sut.InsertAsync(new RepositoryRecord { Name = "root" });
        var middle = await sut.InsertAsync(new RepositoryRecord { Name = "middle", Parent = root });
        var leaf = await sut.InsertAsync(new RepositoryRecord { Name = "leaf", Parent = middle, Children = [middle] });
        var include = new IncludeOption<RepositoryRecord>(x => x.Parent);
        include.ThenIncludes.Add(new ThenIncludeDescriptor { Expression = (System.Linq.Expressions.Expression<Func<RepositoryRecord, RepositoryRecord>>)(x => x.Parent) });
        var found = await sut.FindOneAsync(leaf.Id, new FindOptions<RepositoryRecord> { Include = include });
        found.Parent.Parent.Name.ShouldBe("root");
        found = await sut.FindOneAsync(leaf.Id, new FindOptions<RepositoryRecord> { Hierarchy = new HierarchyOption<RepositoryRecord>(x => x.Parent, 2) });
        found.Parent.Parent.Name.ShouldBe("root");
        found = await sut.FindOneAsync(leaf.Id, new FindOptions<RepositoryRecord> { Include = new IncludeOption<RepositoryRecord>("Children.Parent") });
        found.Children[0].Parent.Name.ShouldBe("root");
    }

    [Fact]
    public async Task Cancellation_PreCanceledCall_DoesNotWrite()
    {
        var sut = this.scope.ServiceProvider.GetRequiredService<IGenericRepository<RepositoryRecord>>();
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => sut.InsertAsync(new RepositoryRecord(), canceled.Token));
        (await sut.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task DirectContext_FailedInsertSet_RestoresAllCallerVersions()
    {
        using var context = new LiteDbContext(Path.Combine(this.directory.FullName, "direct.db"));
        var sut = new LiteDbGenericRepository<RepositoryRecord>(o => o.DbContext(context));
        var first = await sut.InsertAsync(new RepositoryRecord { Name = "first" });
        var second = await sut.InsertAsync(new RepositoryRecord { Name = "second" });
        var version = first.ConcurrencyVersion;
        second.ConcurrencyVersion = Guid.NewGuid();
        first.Name = "changed";
        await Should.ThrowAsync<LiteDbProviderException>(() => sut.InsertSetAsync([first, second]));
        first.ConcurrencyVersion.ShouldBe(version);
        (await sut.FindOneAsync(first.Id)).Name.ShouldBe("first");
    }

    [Fact]
    public Task TypedIntegerIdentity_NativeAllocation_RoundTripsScalarIdentity() => this.CheckIdentityAsync<ActiveEntity.IntegerLedgerEntryId>();

    [Fact]
    public Task TypedGuidIdentity_Allocation_RoundTripsScalarIdentity() => this.CheckIdentityAsync<ActiveEntity.TypedLedgerEntryId>();

    [Fact]
    public Task IntegerIdentity_NativeAllocation_RoundTripsScalarIdentity() => this.CheckIdentityAsync<int>();

    [Fact]
    public Task LongIdentity_NativeAllocation_RoundTripsScalarIdentity() => this.CheckIdentityAsync<long>();

    [Fact]
    public Task StringIdentity_Allocation_RoundTripsScalarIdentity() => this.CheckIdentityAsync<string>();

    private async Task CheckIdentityAsync<TId>()
    {
        var registrations = new ServiceCollection();
        registrations.AddLogging();
        registrations.AddLiteDbDatabase(o => o.ConnectionString = Path.Combine(this.directory.FullName, "identity.db"));
        registrations.AddLiteDbRepository<RepositoryValue<TId>>();
        await using var provider = registrations.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var sut = scope.ServiceProvider.GetRequiredService<IGenericRepository<RepositoryValue<TId>>>();
        var first = await sut.InsertAsync(new RepositoryValue<TId> { Name = "first" });
        var second = await sut.InsertAsync(new RepositoryValue<TId> { Name = "second" });
        first.Id.ShouldNotBe(second.Id);
        (await sut.FindOneAsync(first.Id)).Name.ShouldBe("first");
        (await sut.FindOneAsync(second.Id.ToString())).Name.ShouldBe("second");
    }

    public async ValueTask DisposeAsync()
    {
        await this.scope.DisposeAsync();
        await this.services.DisposeAsync();
        this.directory.Delete(true);
    }
}

public class RepositoryRecord : Entity<Guid>, IConcurrency
{
    public string Name { get; set; }

    public int Amount { get; set; }

    public Guid ConcurrencyVersion { get; set; }

    public RepositoryRecord Parent { get; set; }

    public List<RepositoryRecord> Children { get; set; } = [];
}

public class RepositoryValue<TId> : Entity<TId>
{
    public string Name { get; set; }
}
