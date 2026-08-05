// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.EntityFramework.Broadcasting;

using BridgingIT.DevKit.Infrastructure.EntityFramework.Broadcasting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public abstract class EntityFrameworkBroadcastRegistryStoreTestsBase
{
    protected abstract void ConfigureDatabase(DbContextOptionsBuilder options);

    [Fact]
    public async Task RegistryContract_RegistrationReachabilityLeaseAndRemoval_AreProviderIndependent()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<TestBroadcastingContext>(this.ConfigureDatabase);
        services
            .AddBroadcasting(options =>
                options.Scopes("Alpha").UnreachableFailureThreshold(2)
            )
            .WithEntityFrameworkRegistry<TestBroadcastingContext>();
        await using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true }
        );

        try
        {
            await using (var scope = provider.CreateAsyncScope())
            {
                await scope
                    .ServiceProvider.GetRequiredService<TestBroadcastingContext>()
                    .Database.EnsureCreatedAsync();
            }

            var sut = provider.GetRequiredService<IBroadcastRegistryStore>();
            var now = DateTimeOffset.UtcNow;

            // Act
            await sut.UpsertAsync(
                new(
                    "node-a",
                    new Uri("https://node-a/_bdk/api/broadcasting"),
                    ["Alpha", "alpha", "Beta"],
                    now,
                    now,
                    now.AddMinutes(3)
                )
            );
            await sut.UpsertAsync(
                new(
                    "node-a",
                    new Uri("https://node-a/_bdk/api/broadcasting"),
                    ["Alpha", "Gamma"],
                    now,
                    now.AddSeconds(1),
                    now.AddMinutes(3)
                )
            );
            await sut.RecordDeliveryAsync("node-a", false, "offline");
            var afterFirstFailure = await sut.FindAsync("NODE-A");
            await sut.RecordDeliveryAsync("node-a", false, "offline");
            var afterSecondFailure = await sut.FindAsync("node-a");
            await sut.UpsertAsync(
                new(
                    "node-a",
                    new Uri("https://node-a/_bdk/api/broadcasting"),
                    ["Alpha", "Gamma"],
                    now,
                    now.AddSeconds(2),
                    now.AddMinutes(3)
                )
            );
            await sut.ExpireLeasesAsync(now.AddMinutes(3));
            var afterLeaseExpiry = await sut.FindAsync("node-a");
            await sut.RemoveAsync("node-a");

            // Assert
            (await sut.GetActiveAsync(["beta"])).ShouldBeEmpty();
            afterFirstFailure.IsActive.ShouldBeTrue();
            afterFirstFailure.ConsecutiveFailureCount.ShouldBe(1);
            afterSecondFailure.IsActive.ShouldBeFalse();
            afterSecondFailure.ConsecutiveFailureCount.ShouldBe(2);
            afterLeaseExpiry.IsActive.ShouldBeFalse();
            (await sut.ListAsync()).ShouldBeEmpty();
        }
        finally
        {
            await using var scope = provider.CreateAsyncScope();
            await scope
                .ServiceProvider.GetRequiredService<TestBroadcastingContext>()
                .Database.EnsureDeletedAsync();
        }
    }

    protected sealed class TestBroadcastingContext(
        DbContextOptions<TestBroadcastingContext> options
    ) : DbContext(options), IBroadcastingContext
    {
        public DbSet<BroadcastNodeRegistrationEntity> BroadcastNodeRegistrations { get; set; }

        public DbSet<BroadcastNodeScopeEntity> BroadcastNodeScopes { get; set; }
    }
}
