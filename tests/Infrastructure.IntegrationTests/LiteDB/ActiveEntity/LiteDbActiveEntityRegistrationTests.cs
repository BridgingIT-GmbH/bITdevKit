// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.ActiveEntity;

using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using Microsoft.Extensions.DependencyInjection;

[Collection(nameof(LiteDbActiveEntityTestCollection))]
public class LiteDbActiveEntityRegistrationTests
{
    [Fact]
    public void Registration_DuplicateNamesFilesAndSharedMode_AreRejected()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bdk-{Guid.NewGuid():N}.db");
        var services = new ServiceCollection();
        services.AddLiteDbDatabase(options => options.ConnectionString = path);
        Should.Throw<ArgumentException>(() => services.AddLiteDbDatabase(options => options.ConnectionString = path + ".other"));
        Should.Throw<ArgumentException>(() => services.AddLiteDbDatabase("other", options => options.ConnectionString = path));
        Should.Throw<ArgumentException>(() => new ServiceCollection().AddLiteDbDatabase(options => options.ConnectionString = $"Filename={path};Connection=Shared"));
        Should.Throw<ArgumentException>(() => new ServiceCollection().AddLiteDbDatabase(options => options.ConnectionString = ""));
    }

    [Fact]
    public void ProviderRegistration_InvalidNamesCollectionsAndLimits_AreRejected()
    {
        var services = new ServiceCollection();
        services.AddLiteDbDatabase(options => options.ConnectionString = ":memory:");
        Should.Throw<InvalidOperationException>(() => services.AddActiveEntity(configuration =>
            configuration.For<LedgerEntry, Guid>().UseLiteDbProvider(options => options.DatabaseName = "missing")));
        Should.Throw<ArgumentException>(() => services.AddActiveEntity(configuration =>
            configuration.For<LedgerEntry, Guid>().UseLiteDbProvider(options => options.CollectionName = "$system")));
        Should.Throw<ArgumentOutOfRangeException>(() => services.AddActiveEntity(configuration =>
            configuration.For<LedgerEntry, Guid>().UseLiteDbProvider(options => options.MaxMaterializedRows = 0)));
        Should.Throw<ArgumentException>(() => services.AddActiveEntity(configuration =>
        {
            configuration.For<LedgerEntry, Guid>().UseLiteDbProvider(options => options.CollectionName = "same");
            configuration.For<LedgerItem<int>, int>().UseLiteDbProvider(options => options.CollectionName = "SAME");
        }));
    }

    [Fact]
    public async Task DatabaseInitialization_MapperFailure_CanRetryWithoutLeakingWorker()
    {
        global::LiteDB.LiteDBPragmas.I_AM_AWARE_MY_DATABASE_BREAKS_WHEN_I_USE_THIS();
        var attempts = 0;
        var services = new ServiceCollection();
        services.AddLiteDbDatabase(options =>
        {
            options.ConnectionString = ":memory:";
            options.ConfigureMapper = _ =>
            {
                if (Interlocked.Increment(ref attempts) == 1) { throw new InvalidOperationException("First initialization fails."); }
            };
        });
        services.AddActiveEntity(configuration => configuration.For<LedgerEntry, Guid>().UseLiteDbProvider());
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        Should.Throw<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<IActiveEntityEntityProvider<LedgerEntry, Guid>>());
        var sut = scope.ServiceProvider.GetRequiredService<IActiveEntityEntityProvider<LedgerEntry, Guid>>();
        (await sut.InsertAsync(new LedgerEntry())).IsSuccess.ShouldBeTrue();
        attempts.ShouldBe(2);
    }

    [Fact]
    public async Task NamedDatabases_EqualIdentitiesAndMixedBackends_RemainIsolatedAndDiscoverable()
    {
        global::LiteDB.LiteDBPragmas.I_AM_AWARE_MY_DATABASE_BREAKS_WHEN_I_USE_THIS();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLiteDbDatabase(options => options.ConnectionString = ":memory:");
        services.AddLiteDbDatabase("archive", options => options.ConnectionString = ":memory:");
        services.AddActiveEntity(configuration =>
        {
            configuration.For<LedgerEntry, Guid>().UseLiteDbProvider(options => options.CollectionName = "items");
            configuration.For<LedgerItem<Guid>, Guid>().UseLiteDbProvider(options => { options.CollectionName = "items"; options.DatabaseName = "archive"; });
            configuration.For<LedgerItem<int>, int>().UseInMemoryProvider();
        });
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var id = Guid.NewGuid();
        var sut = scope.ServiceProvider.GetRequiredService<IActiveEntityEntityProvider<LedgerEntry, Guid>>();
        var archive = scope.ServiceProvider.GetRequiredService<IActiveEntityEntityProvider<LedgerItem<Guid>, Guid>>();
        (await sut.InsertAsync(new LedgerEntry { Id = id, Name = "Current" })).IsSuccess.ShouldBeTrue();
        (await archive.InsertAsync(new LedgerItem<Guid> { Id = id, Name = "Archived" })).IsSuccess.ShouldBeTrue();
        (await sut.FindOneAsync(id)).Value.Name.ShouldBe("Current");
        (await archive.FindOneAsync(id)).Value.Name.ShouldBe("Archived");
        (await scope.ServiceProvider.GetRequiredService<IActiveEntityEntityProvider<LedgerItem<int>, int>>().CountAsync()).Value.ShouldBe(0);
        provider.GetRequiredService<ActiveEntityRegistry>().Contains(typeof(LedgerEntry)).ShouldBeTrue();
    }
}
