// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Broadcasting;

using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Broadcasting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public class BroadcastingEntityFrameworkTests
{
    [Fact]
    public void Model_WithoutFeatureSpecificConfiguration_UsesRequiredTablesAndKeys()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        var registrations = context.Model.FindEntityType(typeof(BroadcastNodeRegistrationEntity));
        var scopes = context.Model.FindEntityType(typeof(BroadcastNodeScopeEntity));

        // Assert
        registrations.ShouldNotBeNull();
        scopes.ShouldNotBeNull();
        typeof(BroadcastNodeRegistrationEntity)
            .GetCustomAttribute<TableAttribute>()
            ?.Name.ShouldBe("__Broadcasting_NodeRegistrations");
        typeof(BroadcastNodeScopeEntity)
            .GetCustomAttribute<TableAttribute>()
            ?.Name.ShouldBe("__Broadcasting_NodeScopes");
        registrations
            .FindProperty(nameof(BroadcastNodeRegistrationEntity.ConcurrencyVersion))
            .IsConcurrencyToken.ShouldBeTrue();
        scopes
            .FindPrimaryKey()
            .Properties.Select(x => x.Name)
            .ShouldBe([
                nameof(BroadcastNodeScopeEntity.NodeRegistrationId),
                nameof(BroadcastNodeScopeEntity.NormalizedScope),
            ]);
    }

    [Fact]
    public async Task RegistryStore_UpsertAndQuery_UsesOperationOwnedContexts()
    {
        // Arrange
        var services = new ServiceCollection();
        var databaseName = $"broadcasting-{Guid.NewGuid():N}";
        services.AddDbContext<TestBroadcastingContext>(options =>
            options.UseInMemoryDatabase(databaseName)
        );
        services
            .AddBroadcasting(options => options.Scopes("Alpha"))
            .WithEntityFrameworkRegistry<TestBroadcastingContext>();
        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true }
        );
        var sut = provider.GetRequiredService<IBroadcastRegistryStore>();
        var broadcastingOptions = provider.GetRequiredService<BroadcastingOptions>();
        sut.ShouldBeOfType<EntityFrameworkBroadcastRegistryStore<TestBroadcastingContext>>();
        var now = DateTimeOffset.UtcNow;

        // Act
        await sut.UpsertAsync(
            new(
                "node-a",
                new Uri("https://node-a/_bdk/api/broadcasting"),
                ["Alpha", "Beta"],
                now,
                now,
                null
            )
        );
        var all = await sut.ListAsync();
        var result = await sut.GetActiveAsync(["alpha"]);
        await sut.UpsertAsync(
            new(
                "node-a",
                new Uri("https://node-a/_bdk/api/broadcasting"),
                ["Alpha", "Gamma"],
                now,
                now.AddSeconds(1),
                null
            )
        );
        var updated = await sut.GetActiveAsync(["gamma"]);

        // Assert
        all.Count.ShouldBe(1);
        result.Count.ShouldBe(1);
        result[0].NodeIdentity.ShouldBe("node-a");
        result[0].Scopes.ShouldBe(["Alpha", "Beta"]);
        updated.Count.ShouldBe(1);
        updated[0].Scopes.ShouldBe(["Alpha", "Gamma"]);
        broadcastingOptions.WaitForDatabaseReady.ShouldBeTrue();
        broadcastingOptions.DatabaseReadyName.ShouldBe(nameof(TestBroadcastingContext));
    }

    [Fact]
    public async Task DisabledRuntime_EntityFrameworkProvider_DoesNotRequireDbContextRegistration()
    {
        // Arrange
        var services = new ServiceCollection();
        services
            .AddBroadcasting(options => options.Enabled(false))
            .WithEntityFrameworkRegistry<TestBroadcastingContext>();
        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true }
        );
        var sut = provider.GetRequiredService<IBroadcastService>();

        // Act
        var result = await sut.PublishAsync(new { Value = "ignored" }, ["not-required"]);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(error => error is BroadcastingDisabledError);
    }

    private static TestBroadcastingContext CreateContext() =>
        new(
            new DbContextOptionsBuilder<TestBroadcastingContext>()
                .UseInMemoryDatabase($"broadcasting-model-{Guid.NewGuid():N}")
                .Options
        );

    private sealed class TestBroadcastingContext(DbContextOptions<TestBroadcastingContext> options)
        : DbContext(options),
            IBroadcastingContext
    {
        public DbSet<BroadcastNodeRegistrationEntity> BroadcastNodeRegistrations { get; set; }

        public DbSet<BroadcastNodeScopeEntity> BroadcastNodeScopes { get; set; }
    }
}