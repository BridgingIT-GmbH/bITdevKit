// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Repositories.Bulk;

using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

public class EntityFrameworkEntityBulkInserterTests
{
    [Fact]
    public async Task InsertAsync_EmptyEntities_ReturnsZeroWithoutInvokingProvider()
    {
        // Arrange
        await using var context = CreateContext();
        var provider = new TestEntityBulkInsertProvider(context.Database.ProviderName, 1);
        var sut = CreateSut(context, [provider]);

        // Act
        var result = await sut.InsertAsync([]);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(0);
        provider.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task InsertAsync_MatchingProvider_UsesMatchingProviderAndReturnsInsertedCount()
    {
        // Arrange
        await using var context = CreateContext();
        var matchingProvider = new TestEntityBulkInsertProvider(context.Database.ProviderName, 2);
        var otherProvider = new TestEntityBulkInsertProvider("Other.Provider", 3);
        var sut = CreateSut(context, [otherProvider, matchingProvider]);

        // Act
        var result = await sut.InsertAsync([new DispatchEntity { Name = "Ada" }, new DispatchEntity { Name = "Grace" }]);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(2);
        matchingProvider.CallCount.ShouldBe(1);
        matchingProvider.LastContext.ShouldBeSameAs(context);
        matchingProvider.LastEntityCount.ShouldBe(2);
        otherProvider.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task InsertAsync_MissingProvider_ReturnsFailureWithProviderDetails()
    {
        // Arrange
        await using var context = CreateContext();
        var sut = CreateSut(context, [new TestEntityBulkInsertProvider("Other.Provider", 1)]);

        // Act
        var result = await sut.InsertAsync([new DispatchEntity()]);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(error =>
            error.Message.Contains(context.Database.ProviderName, StringComparison.Ordinal) &&
            error.Message.Contains("Other.Provider", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InsertAsync_DuplicateMatchingProviders_ReturnsFailureWithImplementationDetails()
    {
        // Arrange
        await using var context = CreateContext();
        var sut = CreateSut(context,
        [
            new TestEntityBulkInsertProvider(context.Database.ProviderName, 1),
            new TestEntityBulkInsertProvider(context.Database.ProviderName, 1)
        ]);

        // Act
        var result = await sut.InsertAsync([new DispatchEntity()]);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(error =>
            error.Message.Contains("multiple registered providers", StringComparison.Ordinal) &&
            error.Message.Contains(nameof(TestEntityBulkInsertProvider), StringComparison.Ordinal));
    }

    [Fact]
    public async Task InsertAsync_ProviderThrows_ReturnsFailure()
    {
        // Arrange
        await using var context = CreateContext();
        var provider = new TestEntityBulkInsertProvider(context.Database.ProviderName, 1)
        {
            ExceptionToThrow = new InvalidOperationException("Native provider failure.")
        };
        var sut = CreateSut(context, [provider]);

        // Act
        var result = await sut.InsertAsync([new DispatchEntity()]);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(error => error.Message.Contains("Native provider failure.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InsertAsync_ProviderCancels_RethrowsOperationCanceledException()
    {
        // Arrange
        await using var context = CreateContext();
        var provider = new TestEntityBulkInsertProvider(context.Database.ProviderName, 1)
        {
            ExceptionToThrow = new OperationCanceledException()
        };
        var sut = CreateSut(context, [provider]);

        // Act
        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await sut.InsertAsync([new DispatchEntity()]));
    }

    [Fact]
    public void WithBulkInsert_SingletonLifetime_ResolvesSameInserter()
    {
        // Arrange
        using var serviceProvider = CreateServiceProvider(ServiceLifetime.Singleton);

        // Act
        var first = serviceProvider.GetRequiredService<IEntityBulkInserter<DispatchEntity>>();
        var second = serviceProvider.GetRequiredService<IEntityBulkInserter<DispatchEntity>>();

        // Assert
        first.ShouldBeSameAs(second);
    }

    [Fact]
    public void WithBulkInsert_TransientLifetime_ResolvesDifferentInserters()
    {
        // Arrange
        using var serviceProvider = CreateServiceProvider(ServiceLifetime.Transient);

        // Act
        var first = serviceProvider.GetRequiredService<IEntityBulkInserter<DispatchEntity>>();
        var second = serviceProvider.GetRequiredService<IEntityBulkInserter<DispatchEntity>>();

        // Assert
        first.ShouldNotBeSameAs(second);
    }

    [Fact]
    public void WithBulkInsert_ScopedLifetime_ResolvesSameInserterPerScope()
    {
        // Arrange
        using var serviceProvider = CreateServiceProvider(ServiceLifetime.Scoped);
        using var firstScope = serviceProvider.CreateScope();
        using var secondScope = serviceProvider.CreateScope();

        // Act
        var first = firstScope.ServiceProvider.GetRequiredService<IEntityBulkInserter<DispatchEntity>>();
        var firstAgain = firstScope.ServiceProvider.GetRequiredService<IEntityBulkInserter<DispatchEntity>>();
        var second = secondScope.ServiceProvider.GetRequiredService<IEntityBulkInserter<DispatchEntity>>();

        // Assert
        first.ShouldBeSameAs(firstAgain);
        first.ShouldNotBeSameAs(second);
    }

    [Fact]
    public async Task WithBulkInsert_TestProviderRegistered_DispatchesWithoutSharedChanges()
    {
        // Arrange
        using var serviceProvider = CreateServiceProvider(ServiceLifetime.Scoped);
        using var scope = serviceProvider.CreateScope();
        var testProvider = scope.ServiceProvider
            .GetServices<IEntityBulkInsertProvider>()
            .Single()
            .ShouldBeOfType<TestEntityBulkInsertProvider>();
        var sut = scope.ServiceProvider.GetRequiredService<IEntityBulkInserter<DispatchEntity>>();

        // Act
        var result = await sut.InsertAsync([new DispatchEntity { Name = "Provider extension contract" }]);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(1);
        testProvider.CallCount.ShouldBe(1);
    }

    private static EntityFrameworkEntityBulkInserter<DispatchEntity, DispatchDbContext> CreateSut(
        DispatchDbContext context,
        IEnumerable<IEntityBulkInsertProvider> providers)
    {
        return new EntityFrameworkEntityBulkInserter<DispatchEntity, DispatchDbContext>(
            NullLoggerFactory.Instance,
            context,
            new EntityBulkInsertMappingBuilder<DispatchEntity>(),
            new EntityBulkInsertOptions(),
            providers);
    }

    private static ServiceProvider CreateServiceProvider(ServiceLifetime lifetime)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IEntityBulkInsertProvider>(new TestEntityBulkInsertProvider("Microsoft.EntityFrameworkCore.InMemory", 1));
        services.AddDbContext<DispatchDbContext>(
            options => options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")),
            lifetime,
            lifetime);
        services.AddEntityFrameworkRepository<DispatchEntity, DispatchDbContext>(lifetime)
            .WithBulkInsert();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static DispatchDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DispatchDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new DispatchDbContext(options);
    }

    private sealed class DispatchDbContext(DbContextOptions<DispatchDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DispatchEntity>(builder =>
            {
                builder.ToTable("DispatchEntities");
                builder.HasKey(entity => entity.Id);
                builder.Property(entity => entity.Id).ValueGeneratedOnAdd();
            });
        }
    }

    private sealed class DispatchEntity : Entity<Guid>
    {
        public string Name { get; set; }
    }

}
