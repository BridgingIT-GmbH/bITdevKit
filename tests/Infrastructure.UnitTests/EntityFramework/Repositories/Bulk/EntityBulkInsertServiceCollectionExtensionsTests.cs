// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Repositories.Bulk;

using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Scrutor;

public class EntityBulkInsertServiceCollectionExtensionsTests
{
    [Fact]
    public void AddEntityFrameworkBulkInserter_ScopedDbContext_RegistersScopedTerminalAndMappingBuilder()
    {
        // Arrange
        var services = CreateServices(ServiceLifetime.Scoped);

        // Act
        var context = services.AddEntityFrameworkBulkInserter<RegistrationEntity, FirstDbContext>();

        // Assert
        context.Lifetime.ShouldBe(ServiceLifetime.Scoped);
        services.Single(descriptor => descriptor.ServiceType == typeof(IEntityBulkInserter<RegistrationEntity>))
            .Lifetime.ShouldBe(ServiceLifetime.Scoped);
        services.Single(descriptor => descriptor.ServiceType == typeof(EntityBulkInsertMappingBuilder<RegistrationEntity>))
            .Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddEntityFrameworkBulkInserter_TransientDbContext_RegistersTransientTerminalAndMappingBuilder()
    {
        // Arrange
        var services = CreateServices(ServiceLifetime.Transient);

        // Act
        var context = services.AddEntityFrameworkBulkInserter<RegistrationEntity, FirstDbContext>();

        // Assert
        context.Lifetime.ShouldBe(ServiceLifetime.Transient);
        services.Single(descriptor => descriptor.ServiceType == typeof(IEntityBulkInserter<RegistrationEntity>))
            .Lifetime.ShouldBe(ServiceLifetime.Transient);
        services.Single(descriptor => descriptor.ServiceType == typeof(EntityBulkInsertMappingBuilder<RegistrationEntity>))
            .Lifetime.ShouldBe(ServiceLifetime.Transient);
    }

    [Fact]
    public void AddEntityFrameworkBulkInserter_ScopedDbContext_ResolvesOneBulkInserterPerScope()
    {
        // Arrange
        var services = CreateServices(ServiceLifetime.Scoped);
        services.AddEntityFrameworkBulkInserter<RegistrationEntity, FirstDbContext>();
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        // Act
        var first = scope.ServiceProvider.GetRequiredService<IEntityBulkInserter<RegistrationEntity>>();
        var second = scope.ServiceProvider.GetRequiredService<IEntityBulkInserter<RegistrationEntity>>();

        // Assert
        first.ShouldBeSameAs(second);
    }

    [Fact]
    public void AddEntityFrameworkBulkInserter_TransientDbContext_ResolvesDistinctBulkInserters()
    {
        // Arrange
        var services = CreateServices(ServiceLifetime.Transient);
        services.AddEntityFrameworkBulkInserter<RegistrationEntity, FirstDbContext>();
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        // Act
        var first = scope.ServiceProvider.GetRequiredService<IEntityBulkInserter<RegistrationEntity>>();
        var second = scope.ServiceProvider.GetRequiredService<IEntityBulkInserter<RegistrationEntity>>();

        // Assert
        first.ShouldNotBeSameAs(second);
    }

    [Fact]
    public void AddEntityFrameworkBulkInserter_SingletonDbContext_ThrowsInvalidOperationException()
    {
        // Arrange
        var services = CreateServices(ServiceLifetime.Singleton);

        // Act
        var act = () => services.AddEntityFrameworkBulkInserter<RegistrationEntity, FirstDbContext>();

        // Assert
        act.ShouldThrow<InvalidOperationException>().Message.ShouldContain("cannot be registered as singleton");
    }

    [Fact]
    public void AddEntityFrameworkBulkInserter_SecondEntityRegistration_ThrowsInvalidOperationException()
    {
        // Arrange
        var services = CreateServices(ServiceLifetime.Scoped);
        services.AddDbContext<SecondDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));
        services.AddEntityFrameworkBulkInserter<RegistrationEntity, FirstDbContext>();

        // Act
        var act = () => services.AddEntityFrameworkBulkInserter<RegistrationEntity, SecondDbContext>();

        // Assert
        act.ShouldThrow<InvalidOperationException>().Message.ShouldContain("already registered");
    }

    [Fact]
    public void WithBehavior_RegisteredOnBulkInserterBuilder_UsesBulkInserterLifetime()
    {
        // Arrange
        var services = CreateServices(ServiceLifetime.Transient);
        var context = services.AddEntityFrameworkBulkInserter<RegistrationEntity, FirstDbContext>();

        // Act
        context.WithBehavior<RegistrationBehavior>();

        // Assert
        services.ShouldContain(descriptor =>
            descriptor.ServiceType is DecoratedType &&
            descriptor.ServiceType.ImplementsInterface(typeof(IEntityBulkInserter<RegistrationEntity>)) &&
            descriptor.Lifetime == ServiceLifetime.Transient);
    }

    [Fact]
    public void WithBehavior_ScopedBulkInserter_ResolvesTheDecoratorOncePerScope()
    {
        // Arrange
        var services = CreateServices(ServiceLifetime.Scoped);
        var context = services.AddEntityFrameworkBulkInserter<RegistrationEntity, FirstDbContext>();
        context.WithBehavior<RegistrationBehavior>();
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        // Act
        var first = scope.ServiceProvider.GetRequiredService<IEntityBulkInserter<RegistrationEntity>>();
        var second = scope.ServiceProvider.GetRequiredService<IEntityBulkInserter<RegistrationEntity>>();

        // Assert
        first.ShouldBeOfType<RegistrationBehavior>();
        first.ShouldBeSameAs(second);
    }

    [Fact]
    public void WithShadowValueProvider_PreservesRegistrationOrderAndRejectsDuplicates()
    {
        // Arrange
        var services = CreateServices(ServiceLifetime.Scoped);
        var context = services.AddEntityFrameworkBulkInserter<RegistrationEntity, FirstDbContext>();

        // Act
        context.WithShadowValueProvider<FirstShadowValueProvider>()
            .WithShadowValueProvider<SecondShadowValueProvider>();
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var providers = scope.ServiceProvider
            .GetServices<IEntityBulkInsertShadowValueProvider<RegistrationEntity>>()
            .ToArray();
        var duplicate = () => context.WithShadowValueProvider<FirstShadowValueProvider>();

        // Assert
        providers.Select(provider => provider.GetType()).ShouldBe([
            typeof(FirstShadowValueProvider),
            typeof(SecondShadowValueProvider),
        ]);
        duplicate.ShouldThrow<InvalidOperationException>().Message.ShouldContain("already registered");
    }

    private static ServiceCollection CreateServices(ServiceLifetime lifetime)
    {
        var services = new ServiceCollection();
        services.AddDbContext<FirstDbContext>(
            options => options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")),
            lifetime);

        return services;
    }

    private sealed class RegistrationEntity : Entity<Guid>;

    private sealed class FirstDbContext(DbContextOptions<FirstDbContext> options)
        : DbContext(options);

    private sealed class SecondDbContext(DbContextOptions<SecondDbContext> options)
        : DbContext(options);

    private sealed class RegistrationBehavior(IEntityBulkInserter<RegistrationEntity> inner)
        : IEntityBulkInserter<RegistrationEntity>
    {
        public Task<Result<long>> InsertAsync(
            IEnumerable<RegistrationEntity> entities,
            CancellationToken cancellationToken = default) =>
            inner.InsertAsync(entities, cancellationToken);
    }

    private sealed class FirstShadowValueProvider
        : IEntityBulkInsertShadowValueProvider<RegistrationEntity>
    {
        public bool TryGetValue(
            EntityBulkInsertShadowPropertyContext<RegistrationEntity> context,
            out object value)
        {
            value = "first";
            return true;
        }
    }

    private sealed class SecondShadowValueProvider
        : IEntityBulkInsertShadowValueProvider<RegistrationEntity>
    {
        public bool TryGetValue(
            EntityBulkInsertShadowPropertyContext<RegistrationEntity> context,
            out object value)
        {
            value = "second";
            return true;
        }
    }
}
