// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.ChangeHistory;

using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public class ChangeHistoryServiceCollectionExtensionsTests
{
    [Fact]
    public void AddChangeHistory_WithConfiguration_ReturnsBuilderContext()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var sut = services.AddChangeHistory(options => options.Track<StubEntity>());

        // Assert
        sut.Services.ShouldBeSameAs(services);
        sut.Options.ShouldBeSameAs(services.Single(
            descriptor => descriptor.ServiceType == typeof(ChangeHistoryOptions)).ImplementationInstance);
        sut.Options.GetEntityOptions(typeof(StubEntity)).ShouldNotBeNull();
    }

    [Fact]
    public void WithReadAuthorizer_WithTypedAuthorizer_RegistersScopedService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var sut = services.AddChangeHistory()
            .WithReadAuthorizer<StubDbContext, StubReadAuthorizer>();

        // Assert
        sut.Services.ShouldBeSameAs(services);
        var descriptor = services.Single(
            service => service.ServiceType == typeof(IChangeHistoryReadAuthorizer<StubDbContext>));
        descriptor.ImplementationType.ShouldBe(typeof(StubReadAuthorizer));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void WithRestoreRequestAuthorizer_WithTypedAuthorizer_RegistersScopedService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var sut = services.AddChangeHistory(options => options.Track<StubEntity>())
            .WithRestoreRequestAuthorizer<StubEntity, StubDbContext, StubRestoreRequestAuthorizer>();

        // Assert
        sut.Services.ShouldBeSameAs(services);
        var descriptor = services.Single(
            service => service.ServiceType == typeof(IChangeHistoryRestoreRequestAuthorizer<StubEntity, StubDbContext>));
        descriptor.ImplementationType.ShouldBe(typeof(StubRestoreRequestAuthorizer));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddChangeHistory_WithEntityRestoreAuthorizer_RegistersConcreteScopedService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddChangeHistory(options => options
            .Track<StubEntity>()
            .UseRestoreAuthorizer<StubRestoreAuthorizer>());

        // Assert
        var descriptor = services.Single(service => service.ServiceType == typeof(StubRestoreAuthorizer));
        descriptor.ImplementationType.ShouldBe(typeof(StubRestoreAuthorizer));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddChangeHistory_WithPreRegisteredEntityRestoreAuthorizer_PreservesRegistration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<StubRestoreAuthorizer>();

        // Act
        services.AddChangeHistory(options => options
            .Track<StubEntity>()
            .UseRestoreAuthorizer<StubRestoreAuthorizer>());

        // Assert
        var descriptor = services.Single(service => service.ServiceType == typeof(StubRestoreAuthorizer));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    private sealed class StubEntity : Entity<Guid>
    {
    }

    private sealed class StubDbContext : DbContext
    {
    }

    private sealed class StubReadAuthorizer : IChangeHistoryReadAuthorizer<StubDbContext>
    {
        public Task<Result> AuthorizeAsync(
            ChangeHistoryReadAuthorizationContext context,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());
    }

    private sealed class StubRestoreRequestAuthorizer : IChangeHistoryRestoreRequestAuthorizer<StubEntity, StubDbContext>
    {
        public Task<Result> AuthorizeAsync(
            ChangeHistoryRestoreRequestAuthorizationContext context,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());
    }

    private sealed class StubRestoreAuthorizer : IChangeHistoryRestoreAuthorizer<StubEntity>
    {
        public Task<Result> AuthorizeAsync(
            StubEntity entity,
            ChangeHistoryRestoreAuthorizationContext context,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());
    }
}
