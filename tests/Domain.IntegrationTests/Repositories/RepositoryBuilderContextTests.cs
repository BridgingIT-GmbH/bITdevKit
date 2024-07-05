// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.IntegrationTests.Repositories;

using BridgingIT.DevKit.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

[IntegrationTest("Infrastructure")]
public class RepositoryBuilderContextTests(ITestOutputHelper output) : TestsBase(output, s =>
        {
            s.AddMediatR();

            s.AddInMemoryRepository(
            new InMemoryContext<StubEntity>(new[]
                {
                    new StubEntity() { FirstName = "John" },
                    new StubEntity() { FirstName = "Mary" },
                }))
                .WithBehavior<RepositoryTracingBehavior<StubEntity>>()
                .WithBehavior<RepositoryLoggingBehavior<StubEntity>>()
                .WithBehavior<RepositoryDomainEventBehavior<StubEntity>>()
                .WithBehavior<RepositoryDomainEventPublisherBehavior<StubEntity>>();
        })
{
    [Fact]
    public void GetRepository_WhenRequested_ShouldNotBeNull()
    {
        // Arrange
        // Act
        var context = this.ServiceProvider.GetService<InMemoryContext<StubEntity>>();
        var sut = this.ServiceProvider.GetService<IGenericRepository<StubEntity>>();

        // Assert
        sut.ShouldNotBeNull();
        context.ShouldNotBeNull();
    }
}

[IntegrationTest("Infrastructure")]
public class RepositoryBuilderContextNoInMemoryContextTests(ITestOutputHelper output) : TestsBase(output, s => s
            .AddMediatR()
            .AddInMemoryRepository<StubEntity>()
                .WithBehavior<RepositoryTracingBehavior<StubEntity>>()
                .WithBehavior<RepositoryLoggingBehavior<StubEntity>>()
                .WithBehavior<RepositoryDomainEventBehavior<StubEntity>>()
                .WithBehavior<RepositoryDomainEventPublisherBehavior<StubEntity>>())
{
    [Fact]
    public void GetRepository_WhenRequested_ShouldNotBeNull()
    {
        // Arrange
        // Act
        var context = this.ServiceProvider.GetService<InMemoryContext<StubEntity>>();
        var sut = this.ServiceProvider.GetService<IGenericRepository<StubEntity>>();

        // Assert
        sut.ShouldNotBeNull();
        context.ShouldNotBeNull();
    }
}