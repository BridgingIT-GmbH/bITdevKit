// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.Broadcasting;

using BridgingIT.DevKit.Infrastructure.LiteDb;
using BridgingIT.DevKit.Tests.Caching;
using Microsoft.Extensions.DependencyInjection;

public class LiteDbBroadcastRegistryTests
{
    [Fact]
    public async Task UpsertAsync_RepeatedIdentity_ReplacesScopesAndReactivates()
    {
        // Arrange
        var options = new BroadcastingOptions { UnreachableFailureThreshold = 1 };
        await using var host = new RegistryHost(options);
        var sut = host.Store;
        var now = DateTimeOffset.UtcNow;
        await sut.UpsertAsync(new("node-a", null, ["Alpha"], now, now, null));
        await sut.RecordDeliveryAsync("node-a", false, "offline");

        // Act
        await sut.UpsertAsync(new("node-a", null, ["Beta", "beta"], now, now.AddSeconds(1), null));
        var registrations = await sut.ListAsync();

        // Assert
        registrations.Count.ShouldBe(1);
        registrations[0].IsActive.ShouldBeTrue();
        registrations[0].Scopes.ShouldBe(["Beta"]);
        registrations[0].ConsecutiveFailureCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetActiveAsync_MultipleMatchingScopes_ReturnsNodeOnce()
    {
        // Arrange
        await using var host = new RegistryHost();
        var sut = host.Store;
        var now = DateTimeOffset.UtcNow;
        await sut.UpsertAsync(new("node-a", null, ["Alpha", "Beta"], now, now, null));

        // Act
        var result = await sut.GetActiveAsync(["alpha", "BETA"]);

        // Assert
        result.Count.ShouldBe(1);
        result[0].NodeIdentity.ShouldBe("node-a");
    }

    [Fact]
    public async Task RecordDeliveryAsync_ThresholdReached_DeactivatesUntilSuccess()
    {
        // Arrange
        var options = new BroadcastingOptions { UnreachableFailureThreshold = 2 };
        await using var host = new RegistryHost(options);
        var sut = host.Store;
        var now = DateTimeOffset.UtcNow;
        await sut.UpsertAsync(new("node-a", null, ["Alpha"], now, now, null));

        // Act
        await sut.RecordDeliveryAsync("node-a", false, "offline");
        var afterFirstFailure = await sut.FindAsync("node-a");
        await sut.RecordDeliveryAsync("node-a", false, "offline");
        var afterSecondFailure = await sut.FindAsync("node-a");
        await sut.RecordDeliveryAsync("node-a", true, null);
        var afterSuccess = await sut.FindAsync("node-a");

        // Assert
        afterFirstFailure.IsActive.ShouldBeTrue();
        afterFirstFailure.ConsecutiveFailureCount.ShouldBe(1);
        afterSecondFailure.IsActive.ShouldBeFalse();
        afterSecondFailure.ConsecutiveFailureCount.ShouldBe(2);
        afterSuccess.IsActive.ShouldBeTrue();
        afterSuccess.ConsecutiveFailureCount.ShouldBe(0);
        afterSuccess.LastFailure.ShouldBeNull();
    }

    [Fact]
    public async Task ExpireLeasesAsync_ExpiredRegistration_MarksInactive()
    {
        // Arrange
        await using var host = new RegistryHost();
        var sut = host.Store;
        var now = DateTimeOffset.UtcNow;
        await sut.UpsertAsync(
            new("node-a", null, ["Alpha"], now, now, now.AddMinutes(1))
        );

        // Act
        await sut.ExpireLeasesAsync(now.AddMinutes(1));

        // Assert
        (await sut.FindAsync("node-a")).IsActive.ShouldBeFalse();
        (await sut.GetActiveAsync(["Alpha"])).ShouldBeEmpty();
    }

    [Fact]
    public async Task PersistedNodes_ReopenWithObservations_AndExpiredLeasesAreExcludedBeforeSweep()
    {
        var directory = Directory.CreateTempSubdirectory("bdk-litedb-registry-reopen-");
        var clock = new CacheContractClock();
        try
        {
            var file = Path.Combine(directory.FullName, "registry.db");
            await using (var host = new RegistryHost(file: file, clock: clock))
            {
                await host.Store.UpsertAsync(new("node-a", null, ["Ledger"], clock.GetUtcNow(), clock.GetUtcNow(), clock.GetUtcNow().AddMinutes(1)));
                await host.Store.RecordDeliveryAsync("node-a", true, null);
            }

            await using var reopened = new RegistryHost(file: file, clock: clock);
            reopened.Store.Capabilities.IsShared.ShouldBeFalse();
            reopened.Store.Capabilities.RequiresAdvertisedAddress.ShouldBeFalse();
            (await reopened.Store.FindAsync("NODE-A")).LastSuccessUtc.ShouldBe(clock.GetUtcNow());
            (await reopened.Store.GetActiveAsync(["ledger"])).Count.ShouldBe(1);
            clock.Advance(TimeSpan.FromMinutes(1));
            (await reopened.Store.GetActiveAsync(["ledger"])).ShouldBeEmpty();
            await reopened.Store.RenewLeaseAsync("node-a", clock.GetUtcNow().AddMinutes(1));
            (await reopened.Store.GetActiveAsync(["ledger"])).Count.ShouldBe(1);
            await reopened.Store.RemoveAsync("NODE-A");
            (await reopened.Store.ListAsync()).ShouldBeEmpty();
        }
        finally { directory.Delete(true); }
    }

    private sealed class RegistryHost : IAsyncDisposable
    {
        private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("bdk-litedb-registry-");
        private readonly ServiceProvider services;
        public RegistryHost(BroadcastingOptions options = null, string file = null, TimeProvider clock = null)
        {
            global::LiteDB.LiteDBPragmas.I_AM_AWARE_MY_DATABASE_BREAKS_WHEN_I_USE_THIS();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(clock ?? TimeProvider.System);
            services.AddLiteDbDatabase(o => o.ConnectionString = file ?? Path.Combine(this.directory.FullName, "registry.db"));
            services.AddBroadcasting().WithLiteDbRegistry();
            this.services = services.BuildServiceProvider();
            var native = new LiteDbFeatureOptions { CollectionPrefix = "broadcasting" };
            this.Store = options is null ? this.services.GetRequiredService<IBroadcastRegistryStore>() :
                new LiteDbBroadcastRegistryStore(new(this.services.GetRequiredService<IServiceScopeFactory>(), native), options, clock ?? TimeProvider.System);
        }

        public IBroadcastRegistryStore Store { get; }

        public async ValueTask DisposeAsync() { await this.services.DisposeAsync(); this.directory.Delete(true); }
    }
}
