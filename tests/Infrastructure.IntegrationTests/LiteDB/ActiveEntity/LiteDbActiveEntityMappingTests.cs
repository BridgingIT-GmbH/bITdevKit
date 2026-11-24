// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.ActiveEntity;

using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Domain;
using Microsoft.Extensions.DependencyInjection;
using global::LiteDB;

[Collection(nameof(LiteDbActiveEntityTestCollection))]
public class LiteDbActiveEntityMappingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reopen_StoredValuesAndAuditState_PreserveDataWithoutPendingEvents(bool encrypted)
    {
        var directory = Directory.CreateTempSubdirectory("bdk-litedb-reopen-");
        var file = Path.Combine(directory.FullName, "ledger.db");
        var connection = encrypted ? $"Filename={file};Password=fictional-test-password" : file;
        var entity = new LedgerEntry
        {
            Name = "Entry", Amount = 12, Balance = 345.67m, Content = [0, 1, 255],
            RecordedAt = new DateTime(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc),
            Address = new LedgerAddress { City = "Fictional City" }
        };
        entity.AuditState.SetCreated("fictional-user", "Created for verification");
        entity.AuditState.SetUpdated("fictional-editor", "Verification");
        entity.RegisterDomainEvent(new LedgerEvent());
        try
        {
            await using (var host = new LiteDbActiveEntityTestHost(file: file, configureDatabase: options => options.ConnectionString = connection))
            {
                (await host.Provider.InsertAsync(entity)).IsSuccess.ShouldBeTrue();
            }

            using (var database = new LiteDatabase(connection))
            {
                database.GetCollection("ledger").FindById(entity.Id).ContainsKey("DomainEvents").ShouldBeFalse();
            }

            await using var reopened = new LiteDbActiveEntityTestHost(file: file, configureDatabase: options => options.ConnectionString = connection);
            var result = (await reopened.Provider.FindOneAsync(entity.Id)).Value;
            result.RecordedAt.ShouldBe(entity.RecordedAt);
            result.RecordedAt.Kind.ShouldBe(DateTimeKind.Utc);
            result.Balance.ShouldBe(entity.Balance);
            result.Content.ShouldBe(entity.Content);
            result.Address.City.ShouldBe(entity.Address.City);
            result.ConcurrencyVersion.ShouldBe(entity.ConcurrencyVersion);
            result.AuditState.CreatedBy.ShouldBe(entity.AuditState.CreatedBy);
            result.AuditState.UpdatedBy.ShouldBe(entity.AuditState.UpdatedBy);
            result.AuditState.CreatedDate.ShouldBe(entity.AuditState.CreatedDate, TimeSpan.FromMilliseconds(1));
            result.DomainEvents.ShouldBeEmpty();
            (await reopened.Provider.FindAllAsync(new FindOptions<LedgerEntry> { Include = new IncludeOption<LedgerEntry>(entry => entry.Address) })).IsSuccess.ShouldBeTrue();
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public async Task Includes_ConfiguredReference_ResolveThroughPublicMapper()
    {
        await using var host = new LiteDbActiveEntityTestHost(configureDatabase: options => options.ConfigureMapper = mapper =>
            mapper.Entity<LedgerEntry>().DbRef(entry => entry.Reference, "references"));
        var reference = new LedgerItem<Guid> { Name = "Related" };
        (await host.Get<LedgerItem<Guid>, Guid>().InsertAsync(reference)).IsSuccess.ShouldBeTrue();
        var entity = new LedgerEntry { Reference = reference };
        (await host.Provider.InsertAsync(entity)).IsSuccess.ShouldBeTrue();
        var result = await host.Provider.FindOneAsync(entity.Id, new FindOptions<LedgerEntry> { Include = new IncludeOption<LedgerEntry>(entry => entry.Reference) });
        result.IsSuccess.ShouldBeTrue();
        result.Value.Reference.Name.ShouldBe("Related");
        var pathResult = await host.Provider.FindAllAsync(new FindOptions<LedgerEntry> { Include = new IncludeOption<LedgerEntry>("Reference") });
        pathResult.Value.Single().Reference.Name.ShouldBe("Related");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NumericIdentities_ConcurrentScopesAndReopen_AllocateWithoutCollisions(bool useLong)
    {
        var directory = Directory.CreateTempSubdirectory("bdk-litedb-numeric-");
        var file = Path.Combine(directory.FullName, "identities.db");
        try
        {
            long maximum;
            await using (var host = new LiteDbActiveEntityTestHost(file: file))
            {
                var ids = useLong ? await InsertConcurrentAsync<long>(host) : await InsertConcurrentAsync<int>(host);
                ids.Distinct().Count().ShouldBe(8);
                maximum = ids.Max();
            }

            await using var reopened = new LiteDbActiveEntityTestHost(file: file);
            var next = useLong ? await InsertConcurrentAsync<long>(reopened) : await InsertConcurrentAsync<int>(reopened);
            next.Min().ShouldBeGreaterThan(maximum);
            next.Distinct().Count().ShouldBe(8);
        }
        finally { directory.Delete(true); }
    }

    private static Task<long[]> InsertConcurrentAsync<TId>(LiteDbActiveEntityTestHost host) =>
        Task.WhenAll(Enumerable.Range(1, 8).Select(async index =>
        {
            await using var scope = host.Services.CreateAsyncScope();
            var sut = scope.ServiceProvider.GetRequiredService<IActiveEntityEntityProvider<LedgerItem<TId>, TId>>();
            var entity = new LedgerItem<TId> { Name = index.ToString() };
            (await sut.InsertAsync(entity)).IsSuccess.ShouldBeTrue();
            return Convert.ToInt64(entity.Id);
        }));
}
